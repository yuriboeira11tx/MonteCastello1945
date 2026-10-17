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

public static class CoastCinematicSetup
{
    const string Env = "Assets/Art/Environment/CutsceneInicial";
    const string Work = "ArtSource/Itapage/CoastRefinement";
    static string[] ids = { "aerial_beach_01", "aerial_rocks_02", "aerial_grass_rock", "aerial_ground_rock" };
    static string[] names = { "Praia_Areia_PBR", "Escarpa_Rochosa_PBR", "Rocha_Vegetacao_Rasteira_PBR", "Solo_Pedregoso_PBR" };
    static GameObject Find(string name) => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == name)?.gameObject;
    static T Ensure<T>(GameObject go) where T : Component { var c = go.GetComponent<T>(); return c ? c : go.AddComponent<T>(); }
    public static void Run(string command)
    {
        Directory.CreateDirectory(Work);
        if (SceneManager.GetActiveScene().name != "CutsceneInicial") throw new Exception("Open CutsceneInicial first");
        if (command == "capture") { ScreenCapture.CaptureScreenshot(Work + "/CutsceneInicial_CostaRealista.png", 2); return; }
        if (command == "capturecoast") { ScreenCapture.CaptureScreenshot(Work + "/Costa_Detalhe.png", 2); return; }
        if (command == "coastview")
        {
            if (!EditorApplication.isPlaying) throw new Exception("Inspection view is runtime only");
            Camera.main.transform.position = new Vector3(-600, 65, 450); Camera.main.transform.LookAt(new Vector3(-300, 120, 1250)); Camera.main.fieldOfView = 48; return;
        }
        if (command == "validate") { Validate(); return; }
        if (command == "benchmark") { BeginBenchmark(); return; }
        if (command == "mistoff" || command == "miston") { Find("Bruma_Maritima_Distante").SetActive(command == "miston"); return; }
        if (command == "basemaptest") { Find("Costa_Distante").GetComponent<Terrain>().basemapDistance=4500; return; }
        if (EditorApplication.isPlaying) throw new Exception("Exit play mode before editing");
        if (command == "materials") CreateLayers();
        else if (command == "optimize") OptimizeTextures();
        else if (command == "terrain") BuildTerrain();
        else if (command == "mist") BuildMist();
        else throw new Exception("Unknown coast command: " + command);
        AssetDatabase.SaveAssets(); var s = SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(s); EditorSceneManager.SaveScene(s);
    }
    static void Import(string path, bool normal, bool linear = false, int size = 2048)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var i = (TextureImporter)AssetImporter.GetAtPath(path);
        i.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        i.sRGBTexture = !normal && !linear; i.maxTextureSize = size; i.mipmapEnabled = true;
        i.streamingMipmaps = true; i.anisoLevel = 4; i.wrapMode = TextureWrapMode.Repeat;
        i.textureCompression = TextureImporterCompression.CompressedHQ; i.isReadable = false;
        var platform=i.GetPlatformTextureSettings("Standalone");platform.overridden=true;platform.maxTextureSize=size;
        platform.format=normal?TextureImporterFormat.BC5:TextureImporterFormat.BC7;
        platform.textureCompression=TextureImporterCompression.CompressedHQ;i.SetPlatformTextureSettings(platform);
        i.SaveAndReimport();
    }
    static void OptimizeTextures()
    {
        for(int k=0;k<ids.Length;k++)
        {
            string pre=Env+"/Textures/CoastPBR/"+ids[k];
            Import(pre+"_diff_2k.jpg",false,false,k==1?2048:1024);
            Import(pre+"_nor_gl_2k.png",true,false,1024);
            Import(pre+"_TerrainMask.png",false,true,512);
        }
    }
    static void CreateLayers()
    {
        Directory.CreateDirectory(Env + "/Terrain/CoastRealistic");
        for (int k = 0; k < ids.Length; k++)
        {
            string pre = Env + "/Textures/CoastPBR/" + ids[k];
            Import(pre + "_diff_2k.jpg", false); Import(pre + "_nor_gl_2k.png", true);
            // Repack the CC0 ARM source into Unity Terrain's metallic/AO/height/smoothness layout.
            var source = new Texture2D(2, 2, TextureFormat.RGB24, false, true);
            source.LoadImage(File.ReadAllBytes(pre + "_arm_2k.jpg"));
            var pix = source.GetPixels32();
            for (int p = 0; p < pix.Length; p++)
            {
                var a = pix[p];
                pix[p] = new Color32(0, (byte)Mathf.Lerp(175,255,a.r/255f), 128, (byte)Mathf.Clamp((255-a.g)*0.45f,0,60));
            }
            var mask = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
            mask.SetPixels32(pix); mask.Apply(); string mp = pre + "_TerrainMask.png";
            File.WriteAllBytes(mp, mask.EncodeToPNG()); Object.DestroyImmediate(source); Object.DestroyImmediate(mask);
            Import(mp, false, true, 1024);
            string lp = Env + "/Terrain/CoastRealistic/" + names[k] + ".terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(lp);
            if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, lp); }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pre + "_diff_2k.jpg");
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pre + "_nor_gl_2k.png");
            layer.maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(mp);
            float scale = new[] { 45f, 78f, 55f, 50f }[k]; layer.tileSize = new Vector2(scale, scale);
            layer.tileOffset = new Vector2(k * 13.7f, k * 8.3f); layer.normalScale = new[] { .5f, 1.1f, .85f, .7f }[k];
            layer.metallic = 0; layer.smoothness = .08f;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = k == 2 ? new Vector4(.72f,.82f,.69f,1) : k == 0 ? new Vector4(.77f,.80f,.80f,1) : new Vector4(.86f,.89f,.87f,1);
            EditorUtility.SetDirty(layer);
        }
    }
    static float N(float x, float y) => Mathf.PerlinNoise(x + 71.3f, y + 39.7f) * 2 - 1;
    static float FB(float x, float y)
    {
        return N(x,y)*.56f + N(x*2.08f+17,y*2.08f-21)*.25f + N(x*4.21f-11,y*4.21f+33)*.12f + N(x*8.5f+41,y*8.5f+13)*.07f;
    }
    static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
    static float Peak(float x, float z, float px, float pz, float sx, float sz, float height)
    { float a=(x-px)/sx,b=(z-pz)/sz; return height*Mathf.Exp(-(a*a+b*b)*1.65f); }
    static void BuildTerrain()
    {
        const int res=1025; const float width=4000,depth=2600,height=420;
        var h=new float[res,res];
        for(int z=0;z<res;z++) for(int x=0;x<res;x++)
        {
            float wx=x/(res-1f)*width-width*.5f,wz=z/(res-1f)*depth;
            float shore=170+160*FB(wx*.0018f,13)+45*N(wx*.008f,8);
            float d=wz-shore;
            float xx=wx+90*FB(wx*.003f,wz*.003f),zz=wz+85*FB(wx*.002f+100,wz*.002f);
            float mass=Mathf.Max(Peak(xx,zz,-1550,670,460,600,175),Peak(xx,zz,-850,1100,650,800,300));
            mass=Mathf.Max(mass,Peak(xx,zz,-150,650,390,550,210));
            mass=Mathf.Max(mass,Peak(xx,zz,610,1150,700,850,335));
            mass=Mathf.Max(mass,Peak(xx,zz,1430,750,480,650,230));
            mass=Mathf.Max(mass,Peak(xx,zz,-1450,2060,600,700,330));
            mass=Mathf.Max(mass,Peak(xx,zz,-180,2260,650,650,375));
            mass=Mathf.Max(mass,Peak(xx,zz,1410,2120,620,650,355));
            float ridges=1-Mathf.Abs(N(xx*.0045f,zz*.0038f));
            float detail=19*FB(xx*.016f,zz*.016f)+5*FB(xx*.047f,zz*.047f);
            float land=Smooth(-5,190,d);
            float cliff=25*Smooth(5,45,d)*(1-Smooth(80,210,d))*(.7f+.3f*N(wx*.009f,18));
            float meters=28+Mathf.Clamp(d*.11f,-24,5)+land*(mass*(.64f+.36f*ridges)+detail)+cliff;
            float edge=Smooth(0,240,Mathf.Min(wx+width*.5f,width*.5f-wx));
            edge*=1-Smooth(depth-130,depth,wz);
            h[z,x]=Mathf.Clamp01(Mathf.Lerp(4,meters,edge)/height);
        }
        Erode(h,60000,2317);
        string path=Env+"/Terrain/CoastRealistic/Costa_Esculpida.asset";
        var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if(!data){data=new TerrainData{name="Costa_Esculpida"};AssetDatabase.CreateAsset(data,path);}
        data.heightmapResolution=res;data.alphamapResolution=1024;data.baseMapResolution=2048;
        data.size=new Vector3(width,height,depth);data.SetHeights(0,0,h);data.SyncHeightmap();
        data.terrainLayers=names.Select(n=>AssetDatabase.LoadAssetAtPath<TerrainLayer>(Env+"/Terrain/CoastRealistic/"+n+".terrainlayer")).ToArray();
        var alpha=new float[1024,1024,4];
        for(int z=0;z<1024;z++)for(int x=0;x<1024;x++)
        {
            float u=x/1023f,v=z/1023f,alt=data.GetInterpolatedHeight(u,v)-28,slope=data.GetSteepness(u,v);
            float macro=FB(u*27,v*19),patch=FB(u*80+13,v*70);
            float beach=(1-Smooth(1,8+macro*5,alt))*(1-Smooth(14,33,slope));
            float rock=Mathf.Clamp01(Smooth(20,49,slope)*.8f+Smooth(155,300,alt)*.24f+Smooth(.05f,.6f,patch)*.3f);
            float dry=Mathf.Clamp01(.18f+macro*.5f+patch*.3f+Smooth(150,280,alt)*.25f);
            float green=Mathf.Clamp01(1-rock-dry);
            float total=rock+dry+green;
            alpha[z,x,0]=beach;alpha[z,x,1]=(1-beach)*rock/total;
            alpha[z,x,2]=(1-beach)*green/total;alpha[z,x,3]=(1-beach)*dry/total;
        }
        data.SetAlphamaps(0,0,alpha);
        var go=Find("Costa_Distante");if(!go)go=Terrain.CreateTerrainGameObject(data);go.name="Costa_Distante";
        go.transform.position=new Vector3(-2003.37f,-24.18f,669.2f);
        var terrain=Ensure<Terrain>(go);terrain.terrainData=data;terrain.drawInstanced=true;
        terrain.heightmapPixelError=5;terrain.basemapDistance=2300;terrain.treeDistance=1600;terrain.detailObjectDistance=0;
        terrain.shadowCastingMode=ShadowCastingMode.Off;
        Ensure<TerrainCollider>(go).terrainData=data;
        string mp=Env+"/Materials/Costa_Realista_Terrain_URP.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(mp);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));AssetDatabase.CreateAsset(material,mp);}
        material.enableInstancing=true;material.SetFloat("_EnableInstancedPerPixelNormal",1);material.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
        material.EnableKeyword("_NORMALMAP");material.EnableKeyword("_MASKMAP");terrain.materialTemplate=material;
        terrain.Flush();EditorUtility.SetDirty(data);EditorUtility.SetDirty(material);
    }
    static Vector3 Sample(float[,] h,float x,float z)
    {
        int ix=(int)x,iz=(int)z;float u=x-ix,v=z-iz;
        float a=h[iz,ix],b=h[iz,ix+1],c=h[iz+1,ix],d=h[iz+1,ix+1];
        return new Vector3((b-a)*(1-v)+(d-c)*v,(c-a)*(1-u)+(d-b)*u,a*(1-u)*(1-v)+b*u*(1-v)+c*(1-u)*v+d*u*v);
    }
    static void Erode(float[,] h,int droplets,int seed)
    {
        // Offline sediment transport: no terrain generation or erosion runs in the game.
        int r=h.GetLength(0);var random=new System.Random(seed);
        for(int drop=0;drop<droplets;drop++)
        {
            float x=2+(float)random.NextDouble()*(r-5),z=2+(float)random.NextDouble()*(r-5);
            float dx=0,dz=0,water=1,speed=1,sediment=0;
            for(int life=0;life<70;life++)
            {
                var old=Sample(h,x,z);if(old.z<.07f)break;
                dx=dx*.12f-old.x*.88f;dz=dz*.12f-old.y*.88f;
                float length=Mathf.Sqrt(dx*dx+dz*dz);if(length<.000001f)break;dx/=length;dz/=length;
                float nx=x+dx,nz=z+dz;if(nx<2||nz<2||nx>=r-3||nz>=r-3)break;
                float delta=Sample(h,nx,nz).z-old.z;
                float capacity=Mathf.Max(-delta*speed*water*5,.00003f);
                int ix=(int)x,iz=(int)z;float u=x-ix,v=z-iz;
                if(sediment>capacity||delta>0)
                {
                    float amount=delta>0?Mathf.Min(delta,sediment):(sediment-capacity)*.25f;
                    sediment-=amount;h[iz,ix]+=amount*(1-u)*(1-v);h[iz,ix+1]+=amount*u*(1-v);h[iz+1,ix]+=amount*(1-u)*v;h[iz+1,ix+1]+=amount*u*v;
                }
                else
                {
                    float amount=Mathf.Min((capacity-sediment)*.24f,-delta);
                    for(int bz=-1;bz<=1;bz++)for(int bx=-1;bx<=1;bx++)
                    {float weight=(bx==0&&bz==0)?.25f:(bx==0||bz==0)?.125f:.0625f;float eroded=Mathf.Min(h[iz+bz,ix+bx],amount*weight);h[iz+bz,ix+bx]-=eroded;sediment+=eroded;}
                }
                speed=Mathf.Sqrt(Mathf.Max(.01f,speed*speed-delta*4));water*=.985f;x=nx;z=nz;
            }
        }
    }
    static void BuildMist()
    {
        var s=SceneManager.GetActiveScene();
        string backup=Work+"/Backups/Atmosfera_Anterior.txt";
        if(!File.Exists(backup))File.WriteAllText(backup,$"Fog {RenderSettings.fog} mode={RenderSettings.fogMode} color={RenderSettings.fogColor} density={RenderSettings.fogDensity}");
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogDensity=.00024f;RenderSettings.fogColor=new Color(.58f,.66f,.71f);
        string noisePath=Env+"/Textures/CoastPBR/Bruma_Densidade.png";
        if(!File.Exists(noisePath))
        {
            var texture=new Texture2D(128,128,TextureFormat.RGB24,false,true);var colors=new Color[128*128];
            for(int z=0;z<128;z++)for(int x=0;x<128;x++){float n=.5f+.5f*FB(x/128f*4,z/128f*4);colors[z*128+x]=new Color(n,n,n,1);}
            texture.SetPixels(colors);texture.Apply();File.WriteAllBytes(noisePath,texture.EncodeToPNG());Object.DestroyImmediate(texture);
        }
        Import(noisePath,false,true,128);
        string path=Env+"/Materials/Bruma_Maritima.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("FEB/Environment/Coastal Height Mist"));AssetDatabase.CreateAsset(material,path);}
        Vector3 min=new Vector3(-3200,2.82f,380),max=new Vector3(3200,650,4200);
        material.SetColor("_MistColor",new Color(.52f,.62f,.69f,1));
        material.SetFloat("_Density",.0018f);material.SetFloat("_HeightFalloff",60);
        material.SetFloat("_SeaLevel",3.82f);material.SetFloat("_NoiseStrength",.22f);material.SetFloat("_MaxOpacity",.85f);
        material.SetTexture("_NoiseTex",AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath));
        var go=Find("Bruma_Maritima_Distante");if(!go){go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Bruma_Maritima_Distante";Object.DestroyImmediate(go.GetComponent<Collider>());}
        go.SetActive(true);
        go.transform.position=(min+max)*.5f;go.transform.localScale=max-min;
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;renderer.allowOcclusionWhenDynamic=false;
        Camera.main.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
        EditorUtility.SetDirty(material);
    }
    static int benchFrames,benchStage;
    static GameObject benchCoast,benchMist;
    static bool coastWasActive,mistWasActive;
    static StringBuilder bench;
    static void BeginBenchmark()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Benchmark requires Play Mode");
        benchCoast=Find("Costa_Distante");benchMist=Find("Bruma_Maritima_Distante");
        coastWasActive=benchCoast.activeSelf;mistWasActive=benchMist.activeSelf;
        benchCoast.SetActive(false);benchMist.SetActive(false);benchFrames=0;benchStage=0;
        bench=new StringBuilder("Unity Editor Game view, fixed camera. Render counters, not standalone frame-time benchmark.\n");
        EditorApplication.update-=BenchmarkTick;EditorApplication.update+=BenchmarkTick;
    }
    static void BenchmarkTick()
    {
        if(!EditorApplication.isPlaying){EditorApplication.update-=BenchmarkTick;return;}
        if(++benchFrames<60)return;
        benchFrames=0;
        bench.AppendLine($"Stage={benchStage} triangles={UnityStats.triangles} batches={UnityStats.batches} drawCalls={UnityStats.drawCalls} setPass={UnityStats.setPassCalls}");
        if(benchStage==0){benchCoast.SetActive(true);benchStage=1;}
        else if(benchStage==1){benchMist.SetActive(true);benchStage=2;}
        else
        {
            benchCoast.SetActive(coastWasActive);benchMist.SetActive(mistWasActive);
            bench.AppendLine("0=water and ship; 1=add coast; 2=add sea mist. Environment restored.");
            File.WriteAllText(Work+"/contadores_renderizacao.txt",bench.ToString());EditorApplication.update-=BenchmarkTick;
        }
    }
    static void Validate()
    {
        var b=new StringBuilder();var errors=new List<string>();
        var coast=Find("Costa_Distante").GetComponent<Terrain>();var d=coast.terrainData;
        b.AppendLine("Scene="+SceneManager.GetActiveScene().path);b.AppendLine("Play="+EditorApplication.isPlaying);
        b.AppendLine($"Terrain={AssetDatabase.GetAssetPath(d)} heightmap={d.heightmapResolution} alphamap={d.alphamapResolution} size={d.size}");
        b.AppendLine($"Instanced={coast.drawInstanced} pixelError={coast.heightmapPixelError} basemapDistance={coast.basemapDistance} detailDistance={coast.detailObjectDistance} terrainShadow={coast.shadowCastingMode}");
        if(d.terrainLayers.Length!=4)errors.Add("Expected four terrain layers");
        long memory=0;
        foreach(var l in d.terrainLayers)
        {
            if(!l||!l.diffuseTexture||!l.normalMapTexture||!l.maskMapTexture){errors.Add("Missing terrain PBR map");continue;}
            foreach(var t in new[]{l.diffuseTexture,l.normalMapTexture,l.maskMapTexture})memory+=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t);
            b.AppendLine($"Layer={l.name} Albedo={l.diffuseTexture.width} Normal={l.normalMapTexture.width} Mask={l.maskMapTexture.width} Tile={l.tileSize}");
        }
        b.AppendLine("Terrain layer texture runtime memory MiB="+(memory/1048576f).ToString("F2"));
        var mist=Find("Bruma_Maritima_Distante");if(!mist)errors.Add("Missing mist");
        else {var m=mist.GetComponent<Renderer>().sharedMaterial;if(!m||!m.shader.isSupported||ShaderUtil.ShaderHasError(m.shader))errors.Add("Mist shader invalid");b.AppendLine($"Mist: 1 renderer, 12 triangles; density={m.GetFloat("_Density")} height={m.GetFloat("_HeightFalloff")} shadows=off; depth-aware sea intersection");}
        foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)errors.Add("Missing script "+t.name);
        var ship=Find("Itapage");b.AppendLine("Ship position="+ship.transform.position);
        b.AppendLine("UnityStats triangles="+UnityStats.triangles+" batches="+UnityStats.batches+" drawCalls="+UnityStats.drawCalls+" setPass="+UnityStats.setPassCalls);
        b.AppendLine("Errors="+errors.Count);foreach(var e in errors)b.AppendLine(e);
        File.WriteAllText(Work+"/validacao_costa.txt",b.ToString());if(errors.Count>0)throw new Exception(string.Join("\n",errors));
    }
}
