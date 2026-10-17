using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class MenuMusicSetupOnce
{
    const string Request = "tmp/menu_music_apply.request";
    const string Result = "tmp/menu_music_result.json";
    [Serializable] class Report { public bool saved, loop, playOnAwake; public string scene, clip, gameObject, error; public float volume, spatialBlend; }
    static MenuMusicSetupOnce() { EditorApplication.update += ApplyWhenReady; }
    static void ApplyWhenReady()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= ApplyWhenReady;
        var report = new Report();
        try
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audios/Menu/menu_expedicionario.mp3");
            if (!clip) throw new Exception("Música menu_expedicionario não encontrada.");
            StageUtility.GoToMainStage();
            var scene = SceneManager.GetSceneByPath("Assets/Scenes/Menu.unity");
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity", OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var source = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AudioSource>(true)).FirstOrDefault(a => a.clip == clip);
            if (!source)
            {
                var go = new GameObject("Musica_Menu_Expedicionario");
                SceneManager.MoveGameObjectToScene(go, scene);
                source = go.AddComponent<AudioSource>();
            }
            source.clip = clip;
            source.enabled = true;
            source.gameObject.SetActive(true);
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 0;
            source.volume = .5f;
            source.dopplerLevel = 0;
            source.bypassReverbZones = true;
            EditorUtility.SetDirty(source);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Não foi possível salvar Menu.unity.");
            Selection.activeGameObject = source.gameObject;
            report.saved = true; report.scene = scene.path; report.clip = AssetDatabase.GetAssetPath(source.clip); report.gameObject = source.name;
            report.loop = source.loop; report.playOnAwake = source.playOnAwake; report.volume = source.volume; report.spatialBlend = source.spatialBlend;
        }
        catch (Exception e) { report.error = e.ToString(); Debug.LogException(e); }
        File.WriteAllText(Result, JsonUtility.ToJson(report, true));
        File.Delete(Request);
    }
}
