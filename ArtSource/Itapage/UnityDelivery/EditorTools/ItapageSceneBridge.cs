using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ItapageSceneBridge
{
    const string Request = "tmp/itapage_unity_request.txt";
    static bool busy;
    static ItapageSceneBridge() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); }
        catch (IOException) { return; }
        busy = true;
        try
        {
            if (command == "inspect") Inspect();
            else
            {
                Type worker = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("ItapageUnitySetup")).FirstOrDefault(t => t != null);
                if (worker == null) throw new Exception("Setup worker not loaded");
                worker.GetMethod("Run").Invoke(null, new object[] { command });
            }
            File.WriteAllText("tmp/itapage_unity_status.txt", "OK " + command + " " + DateTime.Now);
        }
        catch (Exception e) { File.WriteAllText("tmp/itapage_unity_status.txt", "ERROR " + command + "\n" + e); Debug.LogException(e); }
        finally { busy = false; }
    }
    static void Inspect()
    {
        Scene s = SceneManager.GetActiveScene();
        var b = new StringBuilder();
        b.AppendLine($"SCENE {s.name} path={s.path} dirty={s.isDirty} play={EditorApplication.isPlaying}");
        b.AppendLine($"SKY {RenderSettings.skybox?.name} fog={RenderSettings.fog} PIPE {UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}");
        foreach (var root in s.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            b.AppendLine($"OBJECT {t.name} pos={t.position:F3} rot={t.eulerAngles:F2} scale={t.lossyScale:F3} active={t.gameObject.activeInHierarchy}");
            b.AppendLine(" components="+string.Join(",",t.GetComponents<Component>().Select(c=>c?c.GetType().Name:"MISSING")));
            var mf=t.GetComponent<MeshFilter>();
            if(mf && mf.sharedMesh) b.AppendLine($" mesh={mf.sharedMesh.name} vertices={mf.sharedMesh.vertexCount} bounds={mf.sharedMesh.bounds} asset={AssetDatabase.GetAssetPath(mf.sharedMesh)}");
            var rr=t.GetComponent<Renderer>();
            if(rr) foreach(var m in rr.sharedMaterials) if(m) b.AppendLine($" material={m.name} shader={m.shader.name} asset={AssetDatabase.GetAssetPath(m)} bounds={rr.bounds}");
            var tr=t.GetComponent<Terrain>();
            if(tr) b.AppendLine($" terrain={tr.terrainData?.name} data={AssetDatabase.GetAssetPath(tr.terrainData)} size={tr.terrainData?.size}");
        }
        File.WriteAllText("tmp/itapage_unity_inspection.txt",b.ToString());
        if(s.name=="CutsceneInicial" && !EditorApplication.isPlaying)
        {
            Directory.CreateDirectory("ArtSource/Itapage/Backups");
            string path="ArtSource/Itapage/Backups/CutsceneInicial_Antes_Itapage.unity";
            if(!File.Exists(path)) EditorSceneManager.SaveScene(s,path,true);
        }
    }
}
