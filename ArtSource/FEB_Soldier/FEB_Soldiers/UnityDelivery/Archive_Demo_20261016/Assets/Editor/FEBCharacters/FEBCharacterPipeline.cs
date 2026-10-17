using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MonteCastello.FEB;

namespace MonteCastello.EditorTools
{
    // Scoped, repeatable import pipeline for the FEB character delivery.
    [InitializeOnLoad]
    public static class FEBCharacterPipeline
    {
        const string Root = "Assets/Art/Characters/FEB_Soldiers";
        const string ScenePath = "Assets/Scenes/FEB_Character_Test.unity";
        static readonly string Delivery = Path.GetFullPath("ArtSource/FEB_Soldier/FEB_Soldiers/UnityDelivery");
        static readonly string Request = Path.Combine(Delivery, "unity_command.txt");
        static bool busy;
        static double nextPoll;
        [Serializable] class MaterialEntry { public string name, texture, sha256; public float roughness, metallic; public bool alphaClip; }
        [Serializable] class MaterialManifest { public MaterialEntry[] materials; }
        [Serializable] class Status { public string state, detail, utc; }
        [Serializable] class Validation { public bool passed; public List<string> checks = new(); public List<string> failures = new(); }
        static Validation playReport;
        static int phase;
        static float phaseStart;
        static Vector3 startPosition;
        static Quaternion[] legStart;
        static float walkDistance;

        static FEBCharacterPipeline()
        {
            EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged += OnPlayState;
        }
        static void StatusFile(string state, string detail)
        {
            Directory.CreateDirectory(Delivery);
            File.WriteAllText(Path.Combine(Delivery, "unity_status.json"), JsonUtility.ToJson(new Status { state = state, detail = detail, utc = DateTime.UtcNow.ToString("O") }, true));
        }
        static void Poll()
        {
            if (EditorApplication.isPlaying && SessionState.GetBool("FEB.PlayTest", false))
            {
                try { TickPlayTest(); } catch (Exception e) { FinishPlayTest(e.ToString()); }
            }
            if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            if (!File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
            busy = true;
            try
            {
                switch (command)
                {
                    case "build": Build(); break;
                    case "validate": Validate(); break;
                    case "prefabs_only": PreparePrefabsOnly(); break;
                    case "inspect": InspectScene(); break;
                    case "scene": BuildScene(); AssetDatabase.SaveAssets(); StatusFile("scene_ready", "Cena de teste salva"); break;
                    case "animations":
                        var sourceAvatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath(1)).OfType<Avatar>().First();
                        foreach (var clipName in new[] { "Idle", "Walk", "Run" }) ImportClip(clipName, sourceAvatar);
                        AssetDatabase.SaveAssets(); StatusFile("animations_updated", "Clipes reimportados"); break;
                    case "playtest": StartPlayTest(); break;
                    case "show": OpenTestScene(); EditorApplication.isPlaying = true; StatusFile("preview", "Cena de teste em execução"); break;
                    case "stop": SessionState.SetBool("FEB.PlayTest", false); EditorApplication.isPlaying = false; StatusFile("stopped", "Modo de edição"); break;
                    default: throw new InvalidOperationException("Comando desconhecido: " + command);
                }
            }
            catch (Exception e) { StatusFile("error", e.ToString()); Debug.LogException(e); }
            finally { busy = false; }
        }

        [MenuItem("Tools/FEB/1 - Preparar personagens e cena")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Saia do Play antes de importar.");
            StatusFile("building", "Importando FBX e texturas");
            foreach (string folder in new[] { "Models", "Animations", "Textures", "Materials", "Prefabs" }) Directory.CreateDirectory(Root + "/" + folder);
            foreach (string folder in new[] { "Models", "Animations", "Textures" })
                foreach (string source in Directory.GetFiles(Path.Combine(Delivery, folder)))
                    if (new[] { ".fbx", ".png" }.Contains(Path.GetExtension(source).ToLowerInvariant())) File.Copy(source, Root + "/" + folder + "/" + Path.GetFileName(source), true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var manifest = JsonUtility.FromJson<MaterialManifest>(File.ReadAllText(Path.Combine(Delivery, "materials_manifest.json")));
            var materials = BuildMaterials(manifest);
            var avatars = new Avatar[6];
            for (int i = 1; i <= 6; i++)
            {
                StatusFile("building", "Configurando Humanoid " + i + "/6");
                avatars[i - 1] = ImportModel(i, materials);
            }
            var clips = new AnimationClip[3]; string[] names = { "Idle", "Walk", "Run" };
            for (int i = 0; i < 3; i++) clips[i] = ImportClip(names[i], avatars[0]);
            var controller = BuildController(clips);
            for (int i = 1; i <= 6; i++) BuildPrefab(i, avatars[i - 1], controller);
            BuildScene(); AssetDatabase.SaveAssets();
            Validate();
        }

        static Dictionary<string, Material> BuildMaterials(MaterialManifest manifest)
        {
            var result = new Dictionary<string, Material>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit indisponível");
            foreach (var entry in manifest.materials)
            {
                string texPath = Root + "/Textures/" + entry.texture;
                var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
                ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; ti.maxTextureSize = 4096;
                ti.mipmapEnabled = true; ti.alphaIsTransparency = false;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.filterMode = FilterMode.Trilinear; ti.anisoLevel = 4; ti.SaveAndReimport();
                string matPath = Root + "/Materials/" + entry.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = shader;
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath)); mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 1 - entry.roughness); mat.SetFloat("_Metallic", entry.metallic);
                mat.SetFloat("_Surface", 0); mat.SetFloat("_AlphaClip", 0); mat.SetFloat("_ZWrite", 1);
                mat.SetFloat("_SrcBlend", 1); mat.SetFloat("_DstBlend", 0);
                mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.DisableKeyword("_ALPHATEST_ON"); mat.renderQueue = -1;
                EditorUtility.SetDirty(mat); result[entry.name] = mat;
            }
            return result;
        }

        static string ModelPath(int i) => Root + $"/Models/FEB_Soldado_{i:00}.fbx";
        static string PrefabPath(int i) => Root + $"/Prefabs/FEB_Soldado_{i:00}.prefab";
        static Avatar ImportModel(int i, Dictionary<string, Material> materials)
        {
            string path = ModelPath(i);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importCameras = false; importer.importLights = false; importer.addCollider = false;
            importer.globalScale = 1; importer.useFileScale = true; importer.optimizeGameObjects = false;
            importer.skinWeights = ModelImporterSkinWeights.Custom; importer.maxBonesPerVertex = 4;
            foreach (var pair in materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var transforms = model.GetComponentsInChildren<Transform>(true);
            var mappings = new List<HumanBone>();
            void Map(string humanName, string bone)
            {
                var humanEnum = (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), humanName);
                if (!transforms.Any(t => t.name == bone)) throw new Exception("Osso ausente: " + bone);
                mappings.Add(new HumanBone { humanName = HumanTrait.BoneName[(int)humanEnum], boneName = bone, limit = new HumanLimit { useDefaultValues = true } });
            }
            Map("Hips", "hips"); Map("Spine", "spine"); Map("Chest", "chest"); Map("UpperChest", "upper_chest"); Map("Neck", "neck"); Map("Head", "head");
            foreach (var side in new[] { "Left", "Right" })
            {
                string suffix = side == "Left" ? ".L" : ".R";
                string[] hn = { "Shoulder", "UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg", "Foot", "Toes" };
                string[] bn = { "clavicle", "upper_arm", "forearm", "hand", "thigh", "shin", "foot", "toe" };
                for (int k = 0; k < hn.Length; k++) Map(side + hn[k], bn[k] + suffix);
                string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
                string[] source = { "thumb", "index", "middle", "ring", "pinky" };
                string[] segments = { "Proximal", "Intermediate", "Distal" };
                for (int f = 0; f < 5; f++) for (int k = 0; k < 3; k++) Map(side + fingers[f] + segments[k], source[f] + $"_{k + 1:00}" + suffix);
            }
            importer.humanDescription = new HumanDescription {
                human = mappings.ToArray(),
                skeleton = transforms.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                armStretch = .05f, legStretch = .05f, upperArmTwist = .5f, lowerArmTwist = .5f,
                upperLegTwist = .5f, lowerLegTwist = .5f, feetSpacing = 0, hasTranslationDoF = false
            };
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
            if (!avatar || !avatar.isValid || !avatar.isHuman) throw new Exception("Avatar Humanoid inválido: " + path);
            return avatar;
        }

        static AnimationClip ImportClip(string name, Avatar avatar)
        {
            string path = Root + "/Animations/FEB_" + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = true; importer.animationType = ModelImporterAnimationType.Human;
            // Animation-only FBXs must retain the same Armature parent as the skinned model.
            importer.preserveHierarchy = true;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; importer.sourceAvatar = avatar;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = true; importer.globalScale = 1; importer.useFileScale = true;
            var settings = importer.defaultClipAnimations;
            if (settings.Length == 0) throw new Exception("FBX sem animação: " + name);
            var clip = settings[0]; clip.name = "FEB_" + name;
            clip.loopTime = true; clip.loopPose = true;
            clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
            clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true; clip.keepOriginalPositionXZ = true;
            importer.clipAnimations = new[] { clip }; importer.SaveAndReimport();
            var animation = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (!animation || !animation.humanMotion) throw new Exception("Animação Humanoid inválida: " + name);
            return animation;
        }

        static AnimatorController BuildController(AnimationClip[] clips)
        {
            string path = Root + "/Animations/FEB_Locomotion.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller) return controller;
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var tree = new BlendTree { name = "FEB_Locomotion", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(clips[0], 0); tree.AddChild(clips[1], 1.2f); tree.AddChild(clips[2], 3.4f);
            var machine = controller.layers[0].stateMachine;
            var state = machine.AddState("Locomotion"); state.motion = tree; state.writeDefaultValues = false; machine.defaultState = state;
            EditorUtility.SetDirty(controller); return controller;
        }

        static void BuildPrefab(int i, Avatar avatar, AnimatorController controller)
        {
            var root = new GameObject($"FEB_Soldado_{i:00}");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(i)), root.transform);
                var animator = model.GetComponent<Animator>(); if (!animator) animator = model.AddComponent<Animator>();
                animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var cc = root.AddComponent<CharacterController>(); cc.height = 1.82f; cc.radius = .24f; cc.center = new Vector3(0, .91f, 0);
                cc.stepOffset = .28f; cc.skinWidth = .025f; cc.slopeLimit = 45; cc.minMoveDistance = 0;
                var motor = root.AddComponent<FEBCharacterMotor>(); motor.animator = animator;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(i));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void BuildScene()
        {
            var previous = SceneManager.GetActiveScene();
            if (previous.path == ScenePath && previous.isDirty) throw new Exception("Salve suas alterações na cena FEB antes de recriá-la.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, previous.path == ScenePath ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Terreno de teste";
            ground.transform.position = new Vector3(0, -.1f, 0); ground.transform.localScale = new Vector3(24, .2f, 22);
            string groundMatPath = Root + "/Materials/FEB_TestGround.mat";
            var groundMat = AssetDatabase.LoadAssetAtPath<Material>(groundMatPath);
            if (!groundMat) { groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(groundMat, groundMatPath); }
            groundMat.SetColor("_BaseColor", new Color(.21f, .235f, .22f)); groundMat.SetFloat("_Smoothness", .1f); ground.GetComponent<Renderer>().sharedMaterial = groundMat;
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 3.4f, 11.5f); camera.transform.LookAt(new Vector3(0, 1.05f, 0));
            camera.fieldOfView = 36; camera.nearClipPlane = .05f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f, .09f, .105f);
            var sun = new GameObject("Luz principal", typeof(Light)).GetComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 2.2f;
            sun.transform.rotation = Quaternion.Euler(42, -35, 0); sun.shadows = LightShadows.Soft;
            var fill = new GameObject("Luz de preenchimento", typeof(Light)).GetComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .55f; fill.transform.rotation = Quaternion.Euler(65, 150, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.45f, .48f, .53f);
            var soldiers = new FEBCharacterMotor[6];
            for (int i = 1; i <= 6; i++)
            {
                var soldier = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(i)), scene);
                soldier.transform.position = new Vector3((3.5f - i) * 1.65f, .03f, 0); soldiers[i - 1] = soldier.GetComponent<FEBCharacterMotor>();
                var label = new GameObject($"Identificação {i:00}", typeof(TextMesh)).GetComponent<TextMesh>();
                label.text = $"FEB {i:00}"; label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                label.fontSize = 48; label.characterSize = .035f; label.color = new Color(.86f, .85f, .69f);
                label.transform.position = new Vector3(soldier.transform.position.x, 2.08f, 0); label.transform.rotation = Quaternion.Euler(0, 180, 0);
            }
            var showcase = new GameObject("Controle da demonstração FEB", typeof(FEBCharacterShowcase)).GetComponent<FEBCharacterShowcase>();
            showcase.soldiers = soldiers; showcase.showcaseCamera = camera;
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (previous.IsValid() && !previous.isDirty) EditorSceneManager.CloseScene(previous, true);
            if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath)) EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            if (SceneView.lastActiveSceneView) { SceneView.lastActiveSceneView.in2DMode = false; SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1, 0), Quaternion.Euler(10, 180, 0), 6); }
        }

        [MenuItem("Tools/FEB/2 - Validar importação")]
        public static void Validate()
        {
            var report = new Validation();
            void Check(bool valid, string text) { (valid ? report.checks : report.failures).Add(text); }
            for (int i = 1; i <= 6; i++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(i));
                var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath(i)).OfType<Avatar>().FirstOrDefault();
                Check(avatar && avatar.isHuman && avatar.isValid, $"Soldado {i:00}: Avatar Humanoid válido");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(i));
                Check(prefab && prefab.GetComponent<FEBCharacterMotor>() && prefab.GetComponent<CharacterController>(), $"Soldado {i:00}: prefab com controle e colisão");
                var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
                Check(renderers.Length == 1 && renderers.All(r => r.bones.All(b => b) && r.sharedMaterials.All(m => m && m.shader.name == "Universal Render Pipeline/Lit" && m.GetTexture("_BaseMap"))), $"Soldado {i:00}: skin, ossos e materiais URP vinculados");
                var bounds = renderers[0].bounds; Check(bounds.size.y > 1.6f && bounds.size.y < 2.1f, $"Soldado {i:00}: altura {bounds.size.y:F3} m");
            }
            foreach (string name in new[] { "Idle", "Walk", "Run" })
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Root + "/Animations/FEB_" + name + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                Check(clip.humanMotion && clip.isLooping && clip.length > .5f, $"{name}: Humanoid, loop, duração {clip.length:F3}s");
            }
            var manifest = JsonUtility.FromJson<MaterialManifest>(File.ReadAllText(Path.Combine(Delivery, "materials_manifest.json")));
            foreach (var entry in manifest.materials)
            {
                using var sha = SHA256.Create(); string hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Root + "/Textures/" + entry.texture))).Replace("-", "").ToLowerInvariant();
                Check(hash == entry.sha256, "Textura preservada: " + entry.texture);
            }
            report.passed = report.failures.Count == 0;
            File.WriteAllText(Path.Combine(Delivery, "unity_validation.json"), JsonUtility.ToJson(report, true));
            StatusFile(report.passed ? "validated" : "validation_failed", $"{report.checks.Count} verificações aprovadas; {report.failures.Count} falhas");
        }

        static void PreparePrefabsOnly()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Aguarde sair do Play Mode.");
            var report = new Validation();
            void Check(bool value, string message) { (value ? report.checks : report.failures).Add(message); }
            for (int i = 1; i <= 6; i++)
            {
                string path = PrefabPath(i);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var motor in root.GetComponentsInChildren<FEBCharacterMotor>(true)) UnityEngine.Object.DestroyImmediate(motor);
                    foreach (var cc in root.GetComponentsInChildren<CharacterController>(true)) UnityEngine.Object.DestroyImmediate(cc);
                    foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                    {
                        animator.runtimeAnimatorController = null;
                        animator.applyRootMotion = false;
                        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animators = prefab.GetComponentsInChildren<Animator>(true);
                Check(animators.Length == 1 && animators[0].avatar && animators[0].avatar.isHuman && animators[0].avatar.isValid, $"{i:00}: Animator com Avatar Humanoid válido");
                Check(animators.All(a => a.runtimeAnimatorController == null), $"{i:00}: sem Animator Controller associado");
                Check(prefab.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 && prefab.GetComponentsInChildren<CharacterController>(true).Length == 0 && prefab.GetComponentsInChildren<Animation>(true).Length == 0, $"{i:00}: sem scripts, controle de movimento ou animação legada");
                Check(prefab.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), $"{i:00}: sem componentes ausentes");
                var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Check(skins.Length == 1 && skins[0].bones.Length >= 52 && skins[0].bones.All(b => b) && skins[0].sharedMesh && skins[0].sharedMaterials.All(m => m && m.shader.name == "Universal Render Pipeline/Lit" && m.GetTexture("_BaseMap")), $"{i:00}: malha, ossos, materiais URP e texturas vinculados");
                Check(!((ModelImporter)AssetImporter.GetAtPath(ModelPath(i))).importAnimation && !AssetDatabase.LoadAllAssetsAtPath(ModelPath(i)).OfType<AnimationClip>().Any(), $"{i:00}: FBX sem clipes de animação importados");
                var demoDependencies = AssetDatabase.GetDependencies(path).Where(p => p.StartsWith("Assets/Scripts/FEB/") || p.StartsWith("Assets/Editor/FEBCharacters/") || p.EndsWith(".controller") || p.EndsWith(".anim") || p.Contains("/Animations/")).ToArray();
                Check(demoDependencies.Length == 0, $"{i:00}: prefab independente da demonstração " + string.Join(", ", demoDependencies));
            }
            AssetDatabase.SaveAssets();
            var active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
            {
                if (active.isDirty) EditorSceneManager.SaveScene(active);
                EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity");
            }
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToArray();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(1));
            EditorGUIUtility.PingObject(Selection.activeObject);
            report.passed = report.failures.Count == 0;
            File.WriteAllText(Path.Combine(Delivery, "prefabs_only_validation.json"), JsonUtility.ToJson(report, true));
            StatusFile(report.passed ? "prefabs_only_ready" : "prefabs_only_failed", $"{report.checks.Count} verificações aprovadas; {report.failures.Count} falhas");
        }

        static void InspectScene()
        {
            var lines = new List<string>();
            foreach (var path in new[] { ModelPath(1), Root + "/Animations/FEB_Idle.fbx" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); lines.Add("ASSET " + path);
                foreach (var t in asset.GetComponentsInChildren<Transform>().Take(7)) lines.Add(t.name + " parent=" + t.parent?.name + " pos=" + t.localPosition + " rot=" + t.localEulerAngles + " scale=" + t.localScale);
                var a = asset.GetComponent<Animator>(); if(a) lines.Add("Avatar humanScale=" + a.humanScale);
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("RootT") || b.propertyName.StartsWith("RootQ")))
                    { var curve = AnimationUtility.GetEditorCurve(clip, binding); lines.Add(binding.propertyName + " " + curve.Evaluate(0) + " -> " + curve.Evaluate(.5f)); }
            }
            foreach (var motor in UnityEngine.Object.FindObjectsByType<FEBCharacterMotor>(FindObjectsSortMode.None))
            {
                lines.Add(motor.name + " root " + motor.transform.position);
                foreach (var t in motor.GetComponentsInChildren<Transform>(true).Where(t => t == motor.animator.transform || t.name == "hips" || t.name == "Root" || t.name.Contains("Mesh")))
                    lines.Add(t.name + " world=" + t.position + " local=" + t.localPosition + " scale=" + t.lossyScale + " rotation=" + t.eulerAngles + " active=" + t.gameObject.activeInHierarchy);
                foreach (var r in motor.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var mesh = new Mesh(); r.BakeMesh(mesh);
                    lines.Add("Renderer " + r.name + " enabled=" + r.enabled + " forceOff=" + r.forceRenderingOff + " visible=" + r.isVisible + " bounds=" + r.bounds + " localBounds=" + r.localBounds + " baked=" + mesh.bounds + " rootBone=" + r.rootBone.name + " vertex=" + mesh.vertices[0]);
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }
            File.WriteAllLines(Path.Combine(Delivery, "scene_inspection.txt"), lines);
            Capture(Camera.main, "FEB_Unity_Inspection");
            StatusFile("inspected", "Diagnóstico da cena salvo");
        }

        static void OpenTestScene()
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return;
            if (SceneManager.GetActiveScene().isDirty) throw new Exception("Salve a cena aberta antes de executar o teste FEB.");
            EditorSceneManager.OpenScene(ScenePath);
        }
        [MenuItem("Tools/FEB/3 - Testar animações e movimento")]
        public static void StartPlayTest()
        {
            OpenTestScene(); SessionState.SetBool("FEB.PlayTest", true); StatusFile("playtest", "Iniciando teste em Play Mode");
            if (EditorApplication.isPlaying) InitPlayTest(); else EditorApplication.isPlaying = true;
        }
        static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("FEB.PlayTest", false)) InitPlayTest();
        }
        static void InitPlayTest()
        {
            playReport = new Validation(); phase = 0; phaseStart = Time.realtimeSinceStartup;
            Application.logMessageReceived += OnPlayLog;
        }
        static void OnPlayLog(string condition, string trace, LogType type)
        {
            if (playReport != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) playReport.failures.Add(condition);
        }
        static void PlayCheck(bool value, string message) { (value ? playReport.checks : playReport.failures).Add(message); }
        static void TickPlayTest()
        {
            if (playReport == null) return;
            var show = UnityEngine.Object.FindFirstObjectByType<FEBCharacterShowcase>();
            if (!show) throw new Exception("Demonstração FEB ausente");
            float elapsed = Time.realtimeSinceStartup - phaseStart;
            if (elapsed < 1.2f) return;
            if (phase <= 2)
            {
                foreach (var motor in show.soldiers)
                {
                    var animator = motor.animator;
                    PlayCheck(animator.isHuman && animator.isInitialized, motor.name + ": Animator ativo, modo " + phase);
                    PlayCheck(motor.IsGrounded, motor.name + ": apoiado no chão, modo " + phase);
                    var renderer = animator.GetComponentInChildren<SkinnedMeshRenderer>();
                    var baked = new Mesh(); renderer.BakeMesh(baked);
                    var worldVertices = baked.vertices.Select(v => renderer.transform.TransformPoint(v)).ToArray();
                    float bottom = worldVertices.Min(v => v.y), top = worldVertices.Max(v => v.y);
                    UnityEngine.Object.DestroyImmediate(baked);
                    PlayCheck(top - bottom > 1.2f && top - bottom < 2.2f, motor.name + ": malha deformada dentro dos limites, modo " + phase);
                    PlayCheck(bottom > -.06f && bottom < .14f, motor.name + $": sola a {bottom:F3} m do chão, modo " + phase);
                    float hipHeight = animator.GetBoneTransform(HumanBodyBones.Hips).position.y - motor.transform.position.y;
                    PlayCheck(hipHeight > .65f && hipHeight < 1.15f, motor.name + $": quadril na altura correta ({hipHeight:F2} m), modo " + phase);
                    if (phase > 0) PlayCheck(Quaternion.Angle(legStart[Array.IndexOf(show.soldiers, motor)], animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation) > 2, motor.name + ": pernas animadas, modo " + phase);
                }
                Capture(show.showcaseCamera, "FEB_Unity_" + new[] { "Idle", "Walk", "Run" }[phase]);
                if (phase < 2)
                {
                    legStart = show.soldiers.Select(m => m.animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation).ToArray();
                    show.SetMovementMode(phase + 1);
                }
                else { show.SetMovementMode(0); show.SelectSoldier(0); show.SetControl(true); }
            }
            else if (phase == 3)
            {
                startPosition = show.soldiers[0].transform.position;
                show.soldiers[0].SetSimulatedInput(Vector2.up, false);
            }
            else if (phase == 4)
            {
                walkDistance = Vector3.ProjectOnPlane(show.soldiers[0].transform.position - startPosition, Vector3.up).magnitude;
                PlayCheck(walkDistance > .9f && walkDistance < 2.3f, $"Controle WASD: caminhada deslocou {walkDistance:F2} m");
                startPosition = show.soldiers[0].transform.position;
                show.soldiers[0].SetSimulatedInput(Vector2.up, true);
            }
            else if (phase == 5)
            {
                float distance = Vector3.ProjectOnPlane(show.soldiers[0].transform.position - startPosition, Vector3.up).magnitude;
                PlayCheck(distance > walkDistance * 2 && distance < 5.8f, $"Shift: corrida deslocou {distance:F2} m");
                PlayCheck(show.soldiers[0].IsGrounded, "Controle: colisão com chão durante corrida");
                Capture(show.showcaseCamera, "FEB_Unity_Control"); show.soldiers[0].ClearSimulatedInput(); show.ResetAll(); FinishPlayTest(null); return;
            }
            phase++; phaseStart = Time.realtimeSinceStartup;
        }
        static void Capture(Camera camera, string name)
        {
            var rt = new RenderTexture(1920, 1080, 24); var old = camera.targetTexture; var active = RenderTexture.active;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply(); File.WriteAllBytes(Path.Combine(Delivery, "Previews", name + ".png"), texture.EncodeToPNG()); }
            finally { camera.targetTexture = old; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
        static void FinishPlayTest(string error)
        {
            if (playReport == null) playReport = new Validation();
            if (error != null) playReport.failures.Add(error);
            playReport.passed = playReport.failures.Count == 0;
            Application.logMessageReceived -= OnPlayLog;
            File.WriteAllText(Path.Combine(Delivery, "unity_playtest.json"), JsonUtility.ToJson(playReport, true));
            StatusFile(playReport.passed ? "playtest_passed" : "playtest_failed", $"{playReport.checks.Count} verificações aprovadas; {playReport.failures.Count} falhas");
            SessionState.SetBool("FEB.PlayTest", false); EditorApplication.isPlaying = false;
        }
    }
}
