using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class ItapageUnitySetup
{
    const string Ship="Assets/Art/Ships/Itapage";
    const string Env="Assets/Art/Environment/CutsceneInicial";
    const string Delivery="ArtSource/Itapage/UnityDelivery";
    [Serializable] public class MatEntry { public string name, original, albedo, normal; public float metallic,smoothness; public bool twoSided; }
    [Serializable] public class ModelEntry { public int lod,vertices,polygons; public string path; }
    [Serializable] public class FrameEntry { public float time; public float[] weights; }
    [Serializable] public class AnimEntry { public string name; public float length,frameRate; public string[] shapes; public FrameEntry[] frames; }
    [Serializable] public class Manifest { public MatEntry[] materials; public ModelEntry[] staticModels; public string flagModel; public AnimEntry animation; }
    static Manifest ReadManifest()=>JsonUtility.FromJson<Manifest>(File.ReadAllText(Delivery+"/unity_manifest.json"));
    static T Component<T>(GameObject go) where T:UnityEngine.Component {var c=go.GetComponent<T>();return c?c:go.AddComponent<T>();}
    static void Ensure(string path){Directory.CreateDirectory(path);AssetDatabase.Refresh();}
    public static void Run(string command)
    {
        if(command=="import") Import();
        else if(command=="scene") BuildScene();
        else if(command=="validate") Validate();
        else if(command=="preview") Preview();
        else if(command=="runtime") RuntimeCheck();
        else if(command=="models") InspectModels();
        else if(command=="capture") ScreenCapture.CaptureScreenshot(Delivery+"/CutsceneInicial_Unity.png",2);
        else if(command=="flagcapture") ScreenCapture.CaptureScreenshot(Delivery+"/Bandeira_Unity.png",2);
        else if(command=="flagview")
        {
            if(!EditorApplication.isPlaying)throw new Exception("Flag preview is runtime-only");
            var cam=Camera.main;var origin=GameObject.Find("Itapage").transform.position;
            cam.transform.position=origin+new Vector3(-60.3f,8.6f,7.5f);cam.transform.LookAt(origin+new Vector3(-60.3f,8.6f,0));cam.fieldOfView=27;
        }
        else throw new Exception("Unknown command "+command);
    }
    static void ConfigureTexture(string path,bool normal,bool clamp=false,int max=1024)
    {
        var ti=AssetImporter.GetAtPath(path) as TextureImporter;
        if(!ti)throw new Exception("Texture not imported: "+path);
        ti.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        ti.sRGBTexture=!normal;ti.mipmapEnabled=true;ti.wrapMode=clamp?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
        ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=8;ti.maxTextureSize=max;
        ti.textureCompression=TextureImporterCompression.CompressedHQ;ti.isReadable=false;ti.SaveAndReimport();
    }
    static void InspectModels()
    {
        AssetDatabase.Refresh();var b=new StringBuilder();
        foreach(var path in ReadManifest().staticModels.Select(m=>m.path).Concat(new[]{ReadManifest().flagModel}))
        {
            b.AppendLine(path);
            foreach(var a in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if(a is Mesh m)b.AppendLine($"MESH {m.name} verts={m.vertexCount} shapes={m.blendShapeCount} bounds={m.bounds}");
                if(a is GameObject g)foreach(var r in g.GetComponentsInChildren<Renderer>())b.AppendLine($"RENDERER {r.name} type={r.GetType().Name} bounds={r.bounds}");
            }
        }
        File.WriteAllText(Delivery+"/model_diagnostics.txt",b.ToString());
    }
    static void Import()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before importing.");
        var manifest=ReadManifest();
        foreach(var p in new[]{Ship+"/Materials",Ship+"/Prefabs",Ship+"/Animations",Env+"/Materials",Env+"/Terrain",Env+"/Textures",Env+"/Lighting"}) Ensure(p);
        var materials=new Dictionary<string,Material>();
        foreach(var m in manifest.materials)
        {
            ConfigureTexture(m.albedo,false,m.twoSided);ConfigureTexture(m.normal,true,m.twoSided);
            string path=Ship+"/Materials/"+m.name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.name=m.name;mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(m.albedo));mat.SetColor("_BaseColor",Color.white);
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(m.normal));mat.SetFloat("_BumpScale",m.twoSided?.5f:.65f);
            mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_Metallic",m.metallic);mat.SetFloat("_Smoothness",m.smoothness);
            mat.SetFloat("_Cull",m.twoSided?0:2);mat.doubleSidedGI=m.twoSided;mat.enableInstancing=true;
            materials.Add(m.name,mat);EditorUtility.SetDirty(mat);
        }
        foreach(var path in manifest.staticModels.Select(m=>m.path).Concat(new[]{manifest.flagModel}))
        {
            var mi=AssetImporter.GetAtPath(path) as ModelImporter;
            if(mi==null)throw new Exception("Missing FBX "+path);
            mi.globalScale=1;mi.useFileScale=true;mi.bakeAxisConversion=true;mi.preserveHierarchy=true;
            mi.importCameras=false;mi.importLights=false;mi.importAnimation=path==manifest.flagModel;mi.animationType=path==manifest.flagModel?ModelImporterAnimationType.Generic:ModelImporterAnimationType.None;
            mi.importBlendShapes=path==manifest.flagModel;mi.importNormals=ModelImporterNormals.Import;
            mi.importBlendShapeNormals=ModelImporterNormals.Calculate;mi.importTangents=ModelImporterTangents.CalculateMikk;
            mi.meshCompression=ModelImporterMeshCompression.Off;mi.isReadable=false;mi.addCollider=false;
            mi.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            foreach(var kv in materials)mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),kv.Key),kv.Value);
            mi.SaveAndReimport();
        }
        var root=new GameObject("Itapage");
        try
        {
            var levels=new List<LOD>();float[] thresholds={.36f,.13f,.015f};
            foreach(var model in manifest.staticModels)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(model.path));
                go.name="Estrutura_LOD"+model.lod;go.transform.SetParent(root.transform,false);
                var rr=go.GetComponentsInChildren<Renderer>();
                foreach(var r in rr){r.shadowCastingMode=ShadowCastingMode.On;r.receiveShadows=true;Remap(r,materials);}
                levels.Add(new LOD(thresholds[model.lod],rr));
            }
            var lod=root.AddComponent<LODGroup>();lod.fadeMode=LODFadeMode.CrossFade;lod.animateCrossFading=true;lod.SetLODs(levels.ToArray());lod.RecalculateBounds();
            var flag=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(manifest.flagModel));
            flag.name="Bandeira_Brasil";flag.transform.SetParent(root.transform,false);
            var sr=flag.GetComponentInChildren<SkinnedMeshRenderer>();
            if(sr==null)
            {
                var mf=flag.GetComponentInChildren<MeshFilter>();
                if(mf && mf.sharedMesh.blendShapeCount>0)
                {
                    var mr=mf.GetComponent<MeshRenderer>();var shared=mr.sharedMaterials;var mesh=mf.sharedMesh;
                    UnityEngine.Object.DestroyImmediate(mr);sr=mf.gameObject.AddComponent<SkinnedMeshRenderer>();sr.sharedMesh=mesh;sr.sharedMaterials=shared;sr.localBounds=mesh.bounds;
                    UnityEngine.Object.DestroyImmediate(mf);
                }
            }
            if(sr==null || sr.sharedMesh.blendShapeCount!=manifest.animation.shapes.Length)throw new Exception("Flag blend shapes missing");
            Remap(sr,materials);sr.shadowCastingMode=ShadowCastingMode.TwoSided;sr.receiveShadows=true;
            sr.updateWhenOffscreen=false;var bound=sr.localBounds;bound.Expand(2);sr.localBounds=bound;
            var clip=new AnimationClip{name="Brasil_Vento_Loop",frameRate=30,wrapMode=WrapMode.Loop};
            string bindingPath=AnimationUtility.CalculateTransformPath(sr.transform,flag.transform);
            for(int i=0;i<manifest.animation.shapes.Length;i++)
            {
                int shape=-1;
                for(int j=0;j<sr.sharedMesh.blendShapeCount;j++)if(sr.sharedMesh.GetBlendShapeName(j).EndsWith(manifest.animation.shapes[i],StringComparison.Ordinal))shape=j;
                if(shape<0)throw new Exception("Shape name mismatch "+manifest.animation.shapes[i]);
                var curve=new AnimationCurve(manifest.animation.frames.Select(f=>new Keyframe(f.time,f.weights[i])).ToArray());
                for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(bindingPath,typeof(SkinnedMeshRenderer),"blendShape."+sr.sharedMesh.GetBlendShapeName(shape)),curve);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
            string clipPath=Ship+"/Animations/Brasil_Vento_Loop.anim";
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if(existing){EditorUtility.CopySerialized(clip,existing);UnityEngine.Object.DestroyImmediate(clip);clip=existing;}else AssetDatabase.CreateAsset(clip,clipPath);
            string acpath=Ship+"/Animations/Brasil_Bandeira.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(acpath);
            if(!controller){controller=AnimatorController.CreateAnimatorControllerAtPath(acpath);var state=controller.layers[0].stateMachine.AddState("Vento continuo");state.motion=clip;controller.layers[0].stateMachine.defaultState=state;}
            var animator=Component<Animator>(flag);animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            clip.SampleAnimation(flag,0);
            var collision=new GameObject("Colisoes_Simplificadas");collision.transform.SetParent(root.transform,false);
            HullCollider(collision.transform,"Casco central",new Vector3(-3,-.8f,0),new Vector3(91,10.5f,15));
            HullCollider(collision.transform,"Proa",new Vector3(48.5f,-.2f,0),new Vector3(18,10,7));
            HullCollider(collision.transform,"Popa",new Vector3(-53,-.7f,0),new Vector3(12,10,7.5f));
            PrefabUtility.SaveAsPrefabAsset(root,Ship+"/Prefabs/Itapage.prefab");
            var report=new StringBuilder();report.AppendLine("Prefab created with 3 LODs, native flag Animator, 16 blend shapes, PBR materials, simple hull colliders.");
            foreach(var r in root.GetComponentsInChildren<Renderer>())report.AppendLine(r.name+" bounds="+r.bounds);
            File.WriteAllText(Delivery+"/unity_import_report.txt",report.ToString());
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        AssetDatabase.SaveAssets();
    }
    static void HullCollider(Transform parent,string name,Vector3 center,Vector3 size)
    {var g=new GameObject(name);g.transform.SetParent(parent,false);var b=g.AddComponent<BoxCollider>();b.center=center;b.size=size;}
    static void Remap(Renderer r,Dictionary<string,Material> materials)
    {r.sharedMaterials=r.sharedMaterials.Select(m=>m!=null && materials.ContainsKey(m.name)?materials[m.name]:m).ToArray();}
    static Scene TargetScene()
    {var s=SceneManager.GetSceneByPath("Assets/Scenes/CutsceneInicial.unity");if(!s.IsValid()||!s.isLoaded)s=EditorSceneManager.OpenScene("Assets/Scenes/CutsceneInicial.unity",OpenSceneMode.Single);SceneManager.SetActiveScene(s);return s;}
    static GameObject Find(Scene s,string name)=>s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name==name)?.gameObject;
    static void BuildScene()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before scene setup");
        var s=TargetScene();var water=Find(s,"WaterBlock_50m");if(!water)throw new Exception("Existing water object not found");
        var center=water.transform.position;
        var ship=Find(s,"Itapage");if(!ship)ship=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Ship+"/Prefabs/Itapage.prefab"),s);
        ship.transform.position=center;ship.transform.rotation=Quaternion.identity;ship.transform.localScale=Vector3.one;
        // Preserve the existing water object and shader; changes are local to this scene's material.
        var wr=water.GetComponent<Renderer>();string waterpath=Env+"/Materials/Mar_CutsceneInicial.mat";
        var wm=AssetDatabase.LoadAssetAtPath<Material>(waterpath);
        if(!wm){wm=new Material(wr.sharedMaterial){name="Mar_CutsceneInicial"};AssetDatabase.CreateAsset(wm,waterpath);}
        wm.SetColor("Color_36218622185947c6a5ae36366d8e21d8",new Color(.035f,.15f,.21f,.97f));
        wm.SetColor("Color_93e06cd551a5449091bcde90b46765a0",new Color(.12f,.36f,.35f,.30f));
        wm.SetFloat("Vector1_6269b1025b26473ca8bc61634f34b537",.91f); // Smoothness
        wm.SetFloat("Vector1_687f54e8c371429f86b9eaab0e7dfe3e",.23f); // Normal strength
        wm.SetFloat("Vector1_7273530c27a34c9f8ee5723b84f96baa",.20f); // Displacement
        wm.SetFloat("Vector1_6c82dffdd68049bcb019d3a9c64c92a0",.065f); // Wave speed
        wm.SetFloat("Vector1_6f56a0970372485390c6587863c2374e",-8f); // Depth color range
        wm.SetFloat("Boolean_d3c978b0d14a4f66be175a9b89855be0",0); // Native URP environment reflection
        wm.SetVector("Vector2_4351ac2be1d74054986ec5378db9d578",new Vector4(300,300,0,0));
        wr.sharedMaterial=wm;wr.shadowCastingMode=ShadowCastingMode.Off;wr.receiveShadows=true;
        water.transform.localScale=new Vector3(80,1,80);EditorUtility.SetDirty(wm);PrefabUtility.RecordPrefabInstancePropertyModifications(water.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(wr);
        SetupSky(s,center);
        SetupTerrain(s,center);
        var camera=Find(s,"Main Camera").GetComponent<Camera>();camera.transform.position=center+new Vector3(68,22,-148);camera.transform.LookAt(center+new Vector3(0,11,0));
        camera.fieldOfView=40;camera.nearClipPlane=.3f;camera.farClipPlane=5000;camera.clearFlags=CameraClearFlags.Skybox;camera.allowHDR=true;
        var cd=camera.GetUniversalAdditionalCameraData();cd.requiresDepthTexture=true;cd.requiresColorTexture=true;cd.renderPostProcessing=true;cd.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;cd.antialiasingQuality=AntialiasingQuality.High;cd.dithering=true;
        var atmosphere=Find(s,"Atmosfera_Cutscene")??new GameObject("Atmosfera_Cutscene");
        var volume=Component<Volume>(atmosphere);volume.isGlobal=true;volume.priority=1;
        string profilepath=Env+"/Lighting/CutsceneInicial_Volume.asset";var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilepath);
        if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilepath);
            var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
            var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(.15f);color.contrast.Override(5);color.saturation.Override(-4);
            var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(1.2f);bloom.intensity.Override(.08f);
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
        }
        volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
        DynamicGI.UpdateEnvironment();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
        Selection.activeGameObject=ship;
        var sv=SceneView.lastActiveSceneView;if(sv){sv.sceneLighting=true;sv.sceneViewState.showSkybox=true;sv.sceneViewState.showFog=true;sv.sceneViewState.alwaysRefresh=true;sv.LookAt(center+Vector3.up*13,camera.transform.rotation,150,false,true);sv.Repaint();}
        File.WriteAllText(Delivery+"/scene_setup_report.txt",$"Scene={s.path}\nShip waterline={ship.transform.position}\nExisting water expanded to 4000m, unique material.\nCamera={camera.transform.position}\nCoast starts about 500m behind the ship.\n");
    }
    static void SetupSky(Scene s,Vector3 center)
    {
        string hdri=Env+"/Sky/Kloofendal_PartlyCloudy_4k.hdr";
        var importer=(TextureImporter)AssetImporter.GetAtPath(hdri);importer.textureType=TextureImporterType.Default;importer.sRGBTexture=false;importer.maxTextureSize=4096;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.isReadable=true;importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(hdri);
        string path=Env+"/Materials/Ceu_Nuvens_Reais.mat";var sky=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!sky){sky=new Material(Shader.Find("Skybox/Panoramic")){name="Ceu_Nuvens_Reais"};AssetDatabase.CreateAsset(sky,path);}
        const float skyRotation=355;
        sky.SetTexture("_MainTex",texture);sky.SetFloat("_Exposure",1f);sky.SetFloat("_Rotation",skyRotation);sky.SetColor("_Tint",new Color(.5f,.5f,.5f));
        RenderSettings.skybox=sky;RenderSettings.ambientMode=AmbientMode.Skybox;RenderSettings.ambientIntensity=.85f;
        RenderSettings.defaultReflectionMode=DefaultReflectionMode.Skybox;RenderSettings.defaultReflectionResolution=256;RenderSettings.reflectionIntensity=.85f;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.00042f;RenderSettings.fogColor=new Color(.57f,.67f,.74f);
        var sun=Find(s,"Directional Light").GetComponent<Light>();sun.type=LightType.Directional;sun.intensity=2.2f;sun.useColorTemperature=true;sun.colorTemperature=5700;sun.color=Color.white;sun.shadows=LightShadows.Soft;sun.shadowStrength=.85f;sun.shadowBias=.025f;sun.shadowNormalBias=.25f;
        // Match the directional light to the brightest pixel in the HDRI.
        var pixels=texture.GetPixels();float peak=-1;int idx=0;
        for(int i=0;i<pixels.Length;i++){float value=pixels[i].maxColorComponent;if(value>peak){peak=value;idx=i;}}
        float u=(idx%texture.width+.5f)/texture.width,v=(idx/texture.width+.5f)/texture.height;
        float lon=(.5f-u)*Mathf.PI*2,lat=(1-v)*Mathf.PI;
        var toSun=new Vector3(Mathf.Cos(lon)*Mathf.Sin(lat),Mathf.Cos(lat),Mathf.Sin(lon)*Mathf.Sin(lat));
        toSun=Quaternion.Euler(0,-skyRotation,0)*toSun;if(toSun.y<.1f)toSun=new Vector3(-.4f,.7f,-.5f).normalized;
        sun.transform.rotation=Quaternion.LookRotation(-toSun,Vector3.up);RenderSettings.sun=sun;
        importer.isReadable=false;importer.SaveAndReimport();EditorUtility.SetDirty(sky);
        var pg=Find(s,"Reflexos_Ceu")??Find(s,"Reflexos_Ceu_e_Costa")??new GameObject("Reflexos_Ceu");pg.name="Reflexos_Ceu";pg.transform.position=center+Vector3.up*8;
        var probe=Component<ReflectionProbe>(pg);probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.resolution=256;probe.hdr=true;probe.boxProjection=false;probe.size=new Vector3(5000,1000,5000);probe.blendDistance=60;probe.intensity=1;
        probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.cullingMask=0;probe.nearClipPlane=.3f;probe.farClipPlane=3000;probe.RenderProbe();
        var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(pipeline && pipeline.shadowDistance<350)
        {
            string backup="ArtSource/Itapage/Backups/PC_RPAsset_Antes_Cutscene.asset";
            if(!File.Exists(backup))File.Copy(AssetDatabase.GetAssetPath(pipeline),backup);
            pipeline.shadowDistance=350;EditorUtility.SetDirty(pipeline);
        }
    }
    static float Fbm(float x,float z)
    {return Mathf.PerlinNoise(x*.0011f,z*.0011f)*.55f+Mathf.PerlinNoise(x*.0038f+23,z*.0038f+17)*.3f+Mathf.PerlinNoise(x*.013f+7,z*.013f+51)*.15f;}
    static void SetupTerrain(Scene s,Vector3 center)
    {
        string path=Env+"/Terrain/Costa_Distante.asset";var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if(!data){data=new TerrainData{name="Costa_Distante"};AssetDatabase.CreateAsset(data,path);}
        data.heightmapResolution=513;data.alphamapResolution=256;data.baseMapResolution=1024;data.size=new Vector3(3000,230,1500);
        var heights=new float[513,513];
        for(int z=0;z<513;z++)for(int x=0;x<513;x++)
        {
            float wx=x/512f*3000-1500,wz=z/512f*1500;
            float coast=110+62*Mathf.Sin(wx*.0028f)+35*Mathf.Sin(wx*.008f+2);
            float distance=wz-coast;
            float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/240));
            float ridge=35+100*Fbm(wx+3700,wz+1800)+25*Mathf.Sin(wx*.0031f+1)+18*Mathf.Sin(wx*.012f+wz*.008f);
            float h=distance<0?Mathf.Lerp(4,25,Mathf.Clamp01((distance+160)/160)):25+rise*ridge;
            heights[z,x]=h/230;
        }
        data.SetHeights(0,0,heights);
        Color[] bases={new Color(.55f,.49f,.37f),new Color(.28f,.3f,.25f),new Color(.19f,.25f,.14f),new Color(.33f,.34f,.2f)};
        string[] names={"Areia_Costeira","Rocha_Exposta","Vegetacao_Baixa","Capim_Seco"};var layers=new TerrainLayer[4];
        for(int k=0;k<4;k++)
        {
            string tp=Env+"/Textures/"+names[k]+"_BaseColor.png";
            if(!File.Exists(tp))
            {
                var tex=new Texture2D(512,512,TextureFormat.RGB24,false);var colors=new Color[512*512];
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                {float noise=Mathf.PerlinNoise(x*.07f+k*13,y*.07f+k*31)*.45f+Mathf.PerlinNoise(x*.33f,y*.33f)*.25f+Mathf.PerlinNoise(x*.013f,y*.013f)*.3f;colors[y*512+x]=bases[k]*Mathf.Lerp(.7f,1.25f,noise);}
                tex.SetPixels(colors);tex.Apply();File.WriteAllBytes(tp,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(tp);ConfigureTexture(tp,false,false,512);
            }
            string lp=Env+"/Terrain/"+names[k]+".terrainlayer";layers[k]=AssetDatabase.LoadAssetAtPath<TerrainLayer>(lp);
            if(!layers[k]){layers[k]=new TerrainLayer();AssetDatabase.CreateAsset(layers[k],lp);}
            layers[k].diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(tp);layers[k].tileSize=new Vector2(k==1?22:16,k==1?22:16);layers[k].smoothness=.08f;layers[k].metallic=0;
            string maskPath=Env+"/Textures/"+names[k]+"_Mask.png";
            if(!File.Exists(maskPath))
            {
                var mask=new Texture2D(16,16,TextureFormat.RGBA32,false,true);mask.SetPixels(Enumerable.Repeat(new Color(0,1,.5f,.08f),256).ToArray());mask.Apply();File.WriteAllBytes(maskPath,mask.EncodeToPNG());UnityEngine.Object.DestroyImmediate(mask);AssetDatabase.ImportAsset(maskPath);
                var im=(TextureImporter)AssetImporter.GetAtPath(maskPath);im.sRGBTexture=false;im.textureCompression=TextureImporterCompression.Uncompressed;im.SaveAndReimport();
            }
            layers[k].maskMapTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);EditorUtility.SetDirty(layers[k]);
        }
        data.terrainLayers=layers;var alpha=new float[256,256,4];
        for(int z=0;z<256;z++)for(int x=0;x<256;x++)
        {
            float u=x/255f,v=z/255f,h=data.GetInterpolatedHeight(u,v)-25,slope=data.GetSteepness(u,v);
            float sand=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((h-1)/10));
            float rock=Mathf.Clamp01((slope-14)/25)*.9f+Mathf.Clamp01((h-115)/50)*.3f;
            rock=Mathf.Clamp01(rock)*(1-sand);float dry=Mathf.PerlinNoise(u*15+8,v*15+2)*.45f*(1-sand-rock);
            alpha[z,x,0]=sand;alpha[z,x,1]=rock;alpha[z,x,3]=dry;alpha[z,x,2]=Mathf.Max(0,1-sand-rock-dry);
        }
        data.SetAlphamaps(0,0,alpha);
        var go=Find(s,"Costa_Distante")??Find(s,"Terrain");if(!go)go=Terrain.CreateTerrainGameObject(data);go.name="Costa_Distante";
        go.transform.SetParent(null,true);go.transform.position=center+new Vector3(-1500,-25,800);
        var terrain=Component<Terrain>(go);terrain.terrainData=data;terrain.drawInstanced=true;terrain.heightmapPixelError=8;terrain.basemapDistance=1800;
        var collider=Component<TerrainCollider>(go);collider.terrainData=data;
        string mp=Env+"/Materials/Costa_Terrain_URP.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(mp);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));AssetDatabase.CreateAsset(mat,mp);}
        terrain.materialTemplate=mat;EditorUtility.SetDirty(data);
    }
    static void Validate()
    {
        var s=TargetScene();var ship=Find(s,"Itapage");if(!ship)throw new Exception("Ship not in scene");
        var errors=new List<string>();var report=new StringBuilder();int renderers=0;
        foreach(var r in ship.GetComponentsInChildren<Renderer>(true))
        {
            renderers++;foreach(var m in r.sharedMaterials)if(!m||!m.shader||!m.shader.isSupported||m.shader.name!="Universal Render Pipeline/Lit")errors.Add("Invalid material on "+r.name);
        }
        foreach(var lod in ship.GetComponent<LODGroup>().GetLODs())
        {
            if(lod.renderers.Length!=1 || !lod.renderers[0]){errors.Add("Invalid LOD renderer");continue;}
            var mf=lod.renderers[0].GetComponent<MeshFilter>();
            if(!mf || !mf.sharedMesh)errors.Add("Missing LOD mesh");
            else report.AppendLine($"{mf.sharedMesh.name}: vertices={mf.sharedMesh.vertexCount}, submeshes={mf.sharedMesh.subMeshCount}, triangles={Enumerable.Range(0,mf.sharedMesh.subMeshCount).Sum(i=>(long)mf.sharedMesh.GetIndexCount(i))/3}");
        }
        foreach(var t in s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)errors.Add("Missing script: "+t.name);
        var flag=ship.transform.Find("Bandeira_Brasil").gameObject;var sr=flag.GetComponentInChildren<SkinnedMeshRenderer>();var animator=flag.GetComponent<Animator>();
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Ship+"/Animations/Brasil_Vento_Loop.anim");
        var snapshots=new List<Vector3[]>();
        foreach(float time in new[]{0f,.73f,8f}){clip.SampleAnimation(flag,time);var mesh=new Mesh();sr.BakeMesh(mesh);snapshots.Add(mesh.vertices);UnityEngine.Object.DestroyImmediate(mesh);}
        float motion=0,seam=0;for(int i=0;i<snapshots[0].Length;i++){motion=Mathf.Max(motion,Vector3.Distance(snapshots[0][i],snapshots[1][i]));seam=Mathf.Max(seam,Vector3.Distance(snapshots[0][i],snapshots[2][i]));}
        if(motion<.03f)errors.Add("Flag does not deform");if(seam>.002f)errors.Add("Loop seam exceeds tolerance");
        if(!animator.runtimeAnimatorController)errors.Add("No flag controller");if(ship.GetComponent<LODGroup>().lodCount!=3)errors.Add("Expected three LODs");
        var water=Find(s,"WaterBlock_50m");if(Mathf.Abs(ship.transform.position.y-water.transform.position.y)>.05f)errors.Add("Waterline mismatch");
        if(!RenderSettings.skybox||RenderSettings.skybox.shader.name!="Skybox/Panoramic")errors.Add("Sky missing");
        var coast=Find(s,"Costa_Distante").GetComponent<Terrain>();if(!coast.terrainData||coast.terrainData.terrainLayers.Length!=4)errors.Add("Terrain layers missing");
        clip.SampleAnimation(flag,0);
        report.AppendLine("Errors="+errors.Count);report.AppendLine("Renderers="+renderers);report.AppendLine("Flag shape count="+sr.sharedMesh.blendShapeCount);report.AppendLine("Flag max sampled displacement="+motion+" m");report.AppendLine("Loop endpoint error="+seam+" m");report.AppendLine("Sky="+RenderSettings.skybox.name);report.AppendLine("Terrain="+coast.terrainData.size);report.AppendLine("Ship position="+ship.transform.position);
        foreach(var e in errors)report.AppendLine(e);File.WriteAllText(Delivery+"/unity_validation.txt",report.ToString());
        if(errors.Count>0)throw new Exception(string.Join("\n",errors));
        EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);
    }
    static void Preview()
    {
        var s=TargetScene();var cam=Find(s,"Main Camera").GetComponent<Camera>();
        var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGBHalf);rt.Create();
        var request=new UniversalRenderPipeline.SingleCameraRequest{destination=rt};
        RenderPipeline.SubmitRenderRequest(cam,request);
        var before=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();
        File.WriteAllBytes(Delivery+"/CutsceneInicial_Unity.png",tex.EncodeToPNG());RenderTexture.active=before;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
    static void RuntimeCheck()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Runtime check requires Play Mode");
        var ship=GameObject.Find("Itapage");var sr=ship.transform.Find("Bandeira_Brasil").GetComponentInChildren<SkinnedMeshRenderer>();
        var animator=sr.GetComponentInParent<Animator>();var weights=Enumerable.Range(0,sr.sharedMesh.blendShapeCount).Select(sr.GetBlendShapeWeight).ToArray();
        File.WriteAllText(Delivery+"/runtime_"+Time.frameCount+".txt",$"time={Time.time}\nstate={animator.GetCurrentAnimatorStateInfo(0).normalizedTime}\nweights={string.Join(",",weights)}\nplaying={Application.isPlaying}\n");
    }
}
