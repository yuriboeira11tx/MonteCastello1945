using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CoastCinematicBridge
{
    static bool busy;
    const string Request = "tmp/coast_request.txt";
    static CoastCinematicBridge() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        string command;
        try { command = File.ReadAllText(Request).Trim(); File.Delete(Request); } catch (IOException) { return; }
        busy = true;
        try
        {
            if (command == "inspect") Inspect();
            else
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("CoastCinematicSetup")).FirstOrDefault(t => t != null);
                if (type == null) throw new Exception("Coast setup is not loaded");
                type.GetMethod("Run").Invoke(null, new object[] { command });
            }
            File.WriteAllText("tmp/coast_status.txt", "OK " + command + " " + DateTime.Now);
        }
        catch (Exception e) { File.WriteAllText("tmp/coast_status.txt", "ERROR " + command + "\n" + e); Debug.LogException(e); }
        finally { busy = false; }
    }
    static void Inspect()
    {
        var s = SceneManager.GetActiveScene();
        if (s.name != "CutsceneInicial") throw new Exception("Expected CutsceneInicial; found " + s.name);
        var b = new StringBuilder();
        b.AppendLine($"Scene={s.path}, dirty={s.isDirty}, play={EditorApplication.isPlaying}");
        foreach (var root in s.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.parent && t.GetComponent<Terrain>() == null && t.name != "WaterBlock_50m") continue;
            b.AppendLine($"{t.name}: position={t.position}, rotation={t.eulerAngles}, scale={t.lossyScale}");
            var tr = t.GetComponent<Terrain>();
            if (tr) b.AppendLine($"Terrain={AssetDatabase.GetAssetPath(tr.terrainData)} size={tr.terrainData.size}");
        }
        File.WriteAllText("tmp/coast_inspection.txt", b.ToString());
        if (!EditorApplication.isPlaying)
        {
            Directory.CreateDirectory("ArtSource/Itapage/CoastRefinement/Backups");
            string path = "ArtSource/Itapage/CoastRefinement/Backups/CutsceneInicial_Antes_CostaRealista.unity";
            if (!File.Exists(path)) EditorSceneManager.SaveScene(s, path, true);
        }
    }
}
