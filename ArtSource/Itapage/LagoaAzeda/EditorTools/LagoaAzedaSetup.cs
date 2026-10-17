using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Authoring tool, archived outside Assets after the scene is validated.
public static class LagoaAzedaSetup
{
    const string Env = "Assets/Art/Environment/CutsceneInicial";
    const string Dir = Env + "/Terrain/LagoaAzeda";
    const string Work = "ArtSource/Itapage/LagoaAzeda";
    const float Sea = 3.82f, ShipX = -3.37f, ShipZ = 19.2f, Offshore = 14816f;
    const float Width = 48000f, Depth = 3200f, Base = -36.18f, Vertical = 140f;
    static GameObject Find(string name) => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name)?.gameObject;
    static T Ensure<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c ? c : go.AddComponent<T>(); }
    static float N(float x, float z) => Mathf.PerlinNoise(x + 71.3f, z + 39.7f) * 2 - 1;
    static float FB(float x, float z) => N(x,z)*.58f + N(x*2.09f+17,z*2.09f)*.28f + N(x*4.23f,z*4.23f+33)*.14f;
    static float S(float a, float b, float x) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
    static float G(float x, float center, float spread) => Mathf.Exp(-Mathf.Pow((x-center)/spread,2));
    // Locally straight coast, gently indented. The shoreline at x=0 is exactly 8 nautical miles from the ship.
    static float Shore(float x) => 350f + 90f*(FB(x*.00055f,17)-FB(0,17)) + 34f*(N(x*.0019f,6)-N(0,6));
    static float Relief(float x) => 7f + 29f*(.5f+.5f*FB(x*.00035f,12)) + 12f*G(x,-7300,2200) - 18f*G(x,-1800,1050) - 15*G(x,-11000,540) - 19*G(x,-4100,430) - 18*G(x,5700,700);
    static float Elevation(float x, float z)
    {
        float d=z-Shore(x), low=G(x,-1800,1050), height=Mathf.Max(5,Relief(x));
        float beachWidth=28+38*low+8*N(x*.0017f,8);
        float gully=Mathf.Pow(Mathf.Clamp01(.5f+.5f*N(x*.006f,3)),4)*38 + 8*N(x*.013f,14);
        float face=beachWidth+gully;
        float rising=S(face,face+14+low*65,d);
        float top=height+2.2f*FB(x*.004f,z*.006f)+3.5f*FB(x*.001f,z*.0018f);
        float meters=d<0?Mathf.Max(-32,d*.14f):Mathf.Min(1.4f,d*.045f);
        meters+=rising*top;
        // Incised drainage hollows and flat-topped sedimentary landforms; no alpine peaks.
        meters-=rising*12*Mathf.Pow(Mathf.Clamp01(.5f+.5f*N(x*.006f+z*.0015f,7)),5)*(1-S(70,450,d));
        meters+=rising*S(250,1500,d)*(5+5*FB(x*.0007f,z*.0012f));
        // A low sandy restinga and sheltered lagoon depression behind it, interpreted rather than surveyed.
        float lagoon=G(x,-1800,700)*G(d,550,290);
        meters=Mathf.Lerp(meters,-2.3f,Mathf.Clamp01(lagoon*1.9f));
        float edge=S(0,1200,Width*.5f-Mathf.Abs(x));
        meters=Mathf.Lerp(-32,meters,edge);
        return Mathf.Clamp01((Sea+meters-Base)/Vertical);
    }
    public static void Run(string command)
    {
        if(SceneManager.GetActiveScene().name!="CutsceneInicial")throw new Exception("Open CutsceneInicial first");
        Directory.CreateDirectory(Work);
        if(command=="capture"||command=="capturecoast") { if(!EditorApplication.isPlaying)throw new Exception("Capture in Play Mode"); ScreenCapture.CaptureScreenshot(Work+(command=="capture"?"/LagoaAzeda_Unity.png":"/LagoaAzeda_Detalhe.png"),2);return; }
        if(command=="coastview") { if(!EditorApplication.isPlaying)throw new Exception("Runtime inspection only"); Camera.main.transform.position=new Vector3(-7100,38,ShipZ+Offshore-170);Camera.main.transform.LookAt(new Vector3(-7500,22,ShipZ+Offshore+160));Camera.main.fieldOfView=50;return; }
        if(command=="restoreview") { if(!EditorApplication.isPlaying)throw new Exception("Runtime inspection only");Camera.main.transform.SetPositionAndRotation(new Vector3(64.63f,25.82f,-128.8f),Quaternion.Euler(3.86f,335.32f,0));Camera.main.fieldOfView=40;return; }
        if(command=="validate"){Validate();return;}
        if(command=="benchmark"){BeginBenchmark();return;}
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before authoring");
        if(command=="reload"){if(SceneManager.GetActiveScene().isDirty)throw new Exception("Unsaved scene; refusing reload");EditorSceneManager.OpenScene(SceneManager.GetActiveScene().path);Validate();return;}
        if(command=="build"){Directory.CreateDirectory(Dir);AssetDatabase.Refresh();BuildLayers();BuildTerrain();BuildWater();BuildAtmosphere();}
        else throw new Exception("Unknown command: "+command);
        AssetDatabase.SaveAssets();var scene=SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    static readonly string[] layerNames={"Areia_Clara","Falesia_Sedimentar_Clara","Restinga_Tabuleiro","Falesia_Ocre"};
    static void BuildLayers()
    {
        string[] source={"Praia_Areia_PBR","Escarpa_Rochosa_PBR","Rocha_Vegetacao_Rasteira_PBR","Solo_Pedregoso_PBR"};
        Vector4[] tint={new Vector4(1.05f,1,.86f,1),new Vector4(1.32f,1.12f,.84f,1),new Vector4(.43f,.62f,.32f,1),new Vector4(1.3f,.72f,.41f,1)};
        for(int i=0;i<4;i++)
        {
            string path=Dir+"/"+layerNames[i]+".terrainlayer";
            var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if(!layer){layer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<TerrainLayer>(Env+"/Terrain/CoastRealistic/"+source[i]+".terrainlayer"));AssetDatabase.CreateAsset(layer,path);}
            layer.name=layerNames[i];layer.tileSize=new Vector2(28,28);layer.diffuseRemapMin=Vector4.zero;layer.diffuseRemapMax=tint[i];layer.normalScale=i==1?.8f:.5f;EditorUtility.SetDirty(layer);
        }
    }
    static void BuildTerrain()
    {
        const int res=1025, alphaRes=1024;
        var heights=new float[res,res];
        for(int z=0;z<res;z++)for(int x=0;x<res;x++)heights[z,x]=Elevation(x/(res-1f)*Width-Width*.5f,z/(res-1f)*Depth);
        string path=Dir+"/LagoaAzeda_Tabuleiros_Restinga.asset";
        var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if(!data){data=new TerrainData{name="LagoaAzeda_Tabuleiros_Restinga"};AssetDatabase.CreateAsset(data,path);}
        data.heightmapResolution=res;data.alphamapResolution=alphaRes;data.baseMapResolution=2048;data.size=new Vector3(Width,Vertical,Depth);
        data.SetHeights(0,0,heights);data.SyncHeightmap();
        data.terrainLayers=layerNames.Select(n=>AssetDatabase.LoadAssetAtPath<TerrainLayer>(Dir+"/"+n+".terrainlayer")).ToArray();
        var alpha=new float[alphaRes,alphaRes,4];
        for(int z=0;z<alphaRes;z++)for(int x=0;x<alphaRes;x++)
        {
            float u=x/(alphaRes-1f),v=z/(alphaRes-1f),wx=u*Width-Width*.5f,wz=v*Depth,d=wz-Shore(wx);
            float alt=data.GetInterpolatedHeight(u,v)+Base-Sea,slope=data.GetSteepness(u,v),top=Mathf.Max(5,Relief(wx));
            float sand=(1-S(1.2f,4.6f,alt))*(1-S(15,35,slope));
            float face=S(19,47,slope)*(1-S(top-3,top+2,alt));
            float strata=.5f+.5f*Mathf.Sin(alt*.65f+N(wx*.001f,4)*2);
            float ochre=Mathf.Clamp01(.1f+.23f*strata+.2f*FB(wx*.001f,alt*.07f));
            float dry=S(.2f,.8f,FB(wx*.0017f,wz*.004f))*.16f;
            alpha[z,x,0]=sand;alpha[z,x,1]=(1-sand)*face*(1-ochre);
            alpha[z,x,3]=(1-sand)*(face*ochre+(1-face)*dry);
            alpha[z,x,2]=(1-sand)*(1-face)*(1-dry);
        }
        data.SetAlphamaps(0,0,alpha);
        var go=Find("Costa_Distante");var terrain=go.GetComponent<Terrain>();
        go.transform.position=new Vector3(ShipX-Width*.5f,Base,ShipZ+Offshore-Shore(0));
        terrain.terrainData=data;Ensure<TerrainCollider>(go).terrainData=data;terrain.drawInstanced=true;terrain.heightmapPixelError=1;
        terrain.basemapDistance=2500;terrain.treeDistance=0;terrain.detailObjectDistance=0;terrain.shadowCastingMode=ShadowCastingMode.Off;
        terrain.Flush();EditorUtility.SetDirty(data);
    }
    static Material CopyMaterial(string from,string to)
    {
        var m=AssetDatabase.LoadAssetAtPath<Material>(to);if(!m){m=new Material(AssetDatabase.LoadAssetAtPath<Material>(from));AssetDatabase.CreateAsset(m,to);}return m;
    }
    static void BuildWater()
    {
        var water=Find("WaterBlock_50m");
        var m=CopyMaterial(Env+"/Materials/Mar_CutsceneInicial.mat",Env+"/Materials/Mar_LagoaAzeda.mat");
        m.SetColor("Color_36218622185947c6a5ae36366d8e21d8",new Color(.045f,.235f,.265f,.98f));
        m.SetColor("Color_93e06cd551a5449091bcde90b46765a0",new Color(.17f,.37f,.34f,.40f));
        m.SetFloat("Vector1_6269b1025b26473ca8bc61634f34b537",.84f);
        m.SetFloat("Vector1_687f54e8c371429f86b9eaab0e7dfe3e",.32f);
        m.SetFloat("Vector1_6c82dffdd68049bcb019d3a9c64c92a0",.065f);
        m.SetFloat("Vector1_7273530c27a34c9f8ee5723b84f96baa",.22f);
        m.SetVector("Vector2_4351ac2be1d74054986ec5378db9d578",new Vector4(300,300,0,0));
        // Adaptive static grid: detail around the ship, progressively coarser at the horizon.
        // UVs retain the previous physical scale instead of stretching the normal map across 70 km.
        const int n=257;var positions=new Vector3[n*n];var uv=new Vector2[n*n];var normals=new Vector3[n*n];var triangles=new int[(n-1)*(n-1)*6];
        float k=(float)Math.Log(437.5+Math.Sqrt(437.5*437.5+1));
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            float xx=80*(float)Math.Sinh((x/128f-1)*k),zz=80*(float)Math.Sinh((z/128f-1)*k);int i=z*n+x;
            positions[i]=new Vector3(xx,0,zz);normals[i]=Vector3.up;uv[i]=new Vector2(xx/4000+.5f,zz/4000+.5f);
        }
        int p=0;for(int z=0;z<n-1;z++)for(int x=0;x<n-1;x++) { int i=z*n+x;triangles[p++]=i;triangles[p++]=i+n;triangles[p++]=i+1;triangles[p++]=i+1;triangles[p++]=i+n;triangles[p++]=i+n+1; }
        string meshPath=Dir+"/Oceano_Grade_Adaptativa.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(!mesh){mesh=new Mesh{name="Oceano_70km_Grade_Adaptativa"};AssetDatabase.CreateAsset(mesh,meshPath);}else mesh.Clear();
        mesh.indexFormat=IndexFormat.UInt32;mesh.vertices=positions;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateTangents();mesh.RecalculateBounds();
        var bounds=mesh.bounds;bounds.Expand(new Vector3(0,4,0));mesh.bounds=bounds;
        water.GetComponent<MeshFilter>().sharedMesh=mesh;water.transform.localScale=Vector3.one;
        water.GetComponent<Renderer>().sharedMaterial=m;
        // This plane has no gameplay physics; preserve any existing collider without making a 70 km collider.
        var collider=water.GetComponent<Collider>();if(collider)collider.enabled=false;
        foreach(var component in water.GetComponents<Component>())if(component&&PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorUtility.SetDirty(mesh);EditorUtility.SetDirty(m);
    }
    static void BuildAtmosphere()
    {
        Camera.main.farClipPlane=40000;
        Camera.main.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.000018f;
        RenderSettings.fogColor=new Color(.57f,.67f,.70f,1);
        var mist=Find("Bruma_Maritima_Distante");
        var m=CopyMaterial(Env+"/Materials/Bruma_Maritima.mat",Env+"/Materials/Bruma_LagoaAzeda.mat");
        m.SetColor("_MistColor",new Color(.57f,.68f,.72f,1));m.SetFloat("_Density",.000025f);m.SetFloat("_HeightFalloff",210);
        m.SetFloat("_SeaLevel",Sea);m.SetFloat("_NoiseStrength",.13f);m.SetFloat("_MaxOpacity",.72f);
        Vector3 min=new Vector3(-35000,Sea-1,1800),max=new Vector3(35000,1200,36000);
        mist.transform.position=(min+max)*.5f;mist.transform.localScale=max-min;mist.GetComponent<Renderer>().sharedMaterial=m;
        EditorUtility.SetDirty(m);
    }
    static void Validate()
    {
        var b=new StringBuilder();var errors=new List<string>();
        var terrain=Find("Costa_Distante").GetComponent<Terrain>();var data=terrain.terrainData;
        float shoreZ=terrain.transform.position.z+Shore(0),distance=shoreZ-Find("Itapage").transform.position.z;
        b.AppendLine("Scene="+SceneManager.GetActiveScene().path+" play="+EditorApplication.isPlaying);
        b.AppendLine("Coastal reference distance (planar, m)="+distance+"; nautical miles="+distance/1852);
        if(Mathf.Abs(distance-Offshore)>.1f)errors.Add("Distance does not match 8 nautical miles");
        b.AppendLine("Coast="+AssetDatabase.GetAssetPath(data)+" position="+terrain.transform.position+" size="+data.size);
        b.AppendLine($"Native Terrain: heightmap={data.heightmapResolution}, control={data.alphamapResolution}, basemap={data.baseMapResolution}, pixelError={terrain.heightmapPixelError}, instancing={terrain.drawInstanced}, foliage objects=0, terrain shadows={terrain.shadowCastingMode}");
        var hs=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);float lo=float.MaxValue,hi=float.MinValue;
        foreach(float h in hs){float v=h*data.size.y+terrain.transform.position.y-Sea;lo=Mathf.Min(lo,v);hi=Mathf.Max(hi,v);}
        b.AppendLine($"Terrain elevations relative to sea: {lo:F2} to {hi:F2} m. Artistic reconstruction, not surveyed geodata.");
        long memory=0;var textures=new HashSet<Texture>();
        foreach(var layer in data.terrainLayers){if(!layer||!layer.diffuseTexture||!layer.normalMapTexture||!layer.maskMapTexture)errors.Add("Missing PBR layer texture");else foreach(var t in new[]{layer.diffuseTexture,layer.normalMapTexture,layer.maskMapTexture})textures.Add(t);}
        foreach(var t in textures)memory+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
        b.AppendLine("Shared PBR texture memory MiB="+(memory/1048576f).ToString("F2"));
        var water=Find("WaterBlock_50m");var mesh=water.GetComponent<MeshFilter>().sharedMesh;
        b.AppendLine($"Ocean static adaptive mesh: vertices={mesh.vertexCount}, triangles={mesh.triangles.Length/3}, span={mesh.bounds.size}, scale={water.transform.lossyScale}; animated GPU shader, no runtime simulation.");
        b.AppendLine("Ocean material="+AssetDatabase.GetAssetPath(water.GetComponent<Renderer>().sharedMaterial));
        b.AppendLine("Camera far clip="+Camera.main.farClipPlane+"; fog="+RenderSettings.fogDensity);
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)errors.Add("Missing script: "+t.name);
        foreach(var go in new[]{water,Find("Bruma_Maritima_Distante")}){var m=go.GetComponent<Renderer>().sharedMaterial;if(!m||!m.shader.isSupported||ShaderUtil.ShaderHasError(m.shader))errors.Add("Invalid shader: "+go.name);}
        b.AppendLine("Ship position="+Find("Itapage").transform.position);b.AppendLine("Errors="+errors.Count);foreach(var e in errors)b.AppendLine(e);
        File.WriteAllText(Work+"/validacao_lagoa_azeda.txt",b.ToString());if(errors.Count>0)throw new Exception(string.Join("\n",errors));
    }
    static int frames,stage;static bool wasCoast,wasMist;static StringBuilder counters;
    static void BeginBenchmark(){if(!EditorApplication.isPlaying)throw new Exception("Play required");wasCoast=Find("Costa_Distante").activeSelf;wasMist=Find("Bruma_Maritima_Distante").activeSelf;Find("Costa_Distante").SetActive(false);Find("Bruma_Maritima_Distante").SetActive(false);frames=stage=0;counters=new StringBuilder("Unity Editor Game view render counters, not standalone FPS benchmark.\n");EditorApplication.update-=BenchmarkTick;EditorApplication.update+=BenchmarkTick;}
    static void BenchmarkTick(){if(!EditorApplication.isPlaying){EditorApplication.update-=BenchmarkTick;return;}if(++frames<90)return;frames=0;counters.AppendLine($"Stage={stage}, triangles={UnityStats.triangles}, drawCalls={UnityStats.drawCalls}, batches={UnityStats.batches}, setPass={UnityStats.setPassCalls}");if(stage==0){Find("Costa_Distante").SetActive(true);stage++;}else if(stage==1){Find("Bruma_Maritima_Distante").SetActive(true);stage++;}else{Find("Costa_Distante").SetActive(wasCoast);Find("Bruma_Maritima_Distante").SetActive(wasMist);counters.AppendLine("0=ship and ocean, 1=add coast, 2=add mist. State restored.");File.WriteAllText(Work+"/contadores_lagoa_azeda.txt",counters.ToString());EditorApplication.update-=BenchmarkTick;}}
}
