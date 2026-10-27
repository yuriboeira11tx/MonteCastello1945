Shader "FEB/Environment/Ocean Natural Lighting"
{
    Properties
    {
        _DeepColor ("Deep Water", Color) = (0.055,0.13,0.16,1)
        _ShallowColor ("Shallow Water", Color) = (0.12,0.23,0.24,1)
        _NormalA ("Broad Ripple Data (linear RG)", 2D) = "gray" {}
        _NormalB ("Fine Ripple Data (linear RG)", 2D) = "gray" {}
        _NormalStrength ("Ripple Strength", Range(0,1)) = 0.42
        _WaveScale ("Ripple Size (m)", Float) = 24
        _Roughness ("Surface Roughness", Range(0.08,0.6)) = 0.28
        _ReflectionStrength ("Sky Reflection", Range(0,2)) = 1
        _ScatterStrength ("Subsurface Light", Range(0,3)) = 1
        _Absorption ("Depth Absorption", Range(0.01,2)) = 0.22
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "OceanForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_NormalA); SAMPLER(sampler_NormalA);
            TEXTURE2D(_NormalB); SAMPLER(sampler_NormalB);
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor;
                float _NormalStrength, _WaveScale, _Roughness, _ReflectionStrength, _ScatterStrength, _Absorption;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half fog:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            half FifthPower(half x) { half x2=x*x;return x2*x2*x; }
            Varyings Vert(Attributes input)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.fog=ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float distanceToCamera=distance(GetCameraPositionWS(),i.positionWS);
                float2 uv=i.positionWS.xz/max(1,_WaveScale);
                // The supplied water maps pack slopes into R/G and other data into B: not tangent normals.
                // Their copies are imported as linear data, so neutral R/G=0.5 remains zero slope.
                half2 a=SAMPLE_TEXTURE2D(_NormalA,sampler_NormalA,uv+float2(_Time.y*.009,_Time.y*.004)).rg*2-1;
                half2 b=SAMPLE_TEXTURE2D(_NormalB,sampler_NormalB,uv*1.73+float2(-_Time.y*.006,_Time.y*.008)).rg*2-1;
                float attenuation=lerp(1,.025,smoothstep(250,2200,distanceToCamera));
                float2 slope=(a+b*.45)*_NormalStrength*attenuation;
                half3 N=normalize(half3(-slope.x,1,-slope.y));
                half3 V=SafeNormalize(GetWorldSpaceViewDir(i.positionWS));
                half NoV=saturate(dot(N,V));
                half3 R=reflect(-V,N);
                // Keep near-horizontal rays in the sky hemisphere; the source HDRI also contains terrestrial scenery.
                R.y=max(R.y,.055);R=normalize(R);
                half rough=lerp(_Roughness,.31,smoothstep(500,8000,distanceToCamera));
                float2 screenUV=GetNormalizedScreenSpaceUV(i.positionCS);
                half3 reflected=GlossyEnvironmentReflection(R,i.positionWS,rough,1,screenUV);
                // Air/water IOR ~= 1.333 gives normal-incidence reflectance ~= 2%.
                half fresnel=.02037+.97963*FifthPower(1-NoV);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half NoL=saturate(dot(N,sun.direction));
                half3 H=SafeNormalize(V+sun.direction);
                half NoH=saturate(dot(N,H)),VoH=saturate(dot(V,H));
                float alpha=max(.015,rough*rough),alpha2=alpha*alpha;
                float denominator=NoH*NoH*(alpha2-1)+1;
                float distribution=alpha2/max(PI*denominator*denominator,.00001);
                float k=(rough+1)*(rough+1)*.125;
                float visibility=(NoV/max(NoV*(1-k)+k,.0001))*(NoL/max(NoL*(1-k)+k,.0001));
                half Fsun=.02037+.97963*FifthPower(1-VoH);
                half3 directSpecular=sun.color*(distribution*visibility*Fsun/max(4*NoV,.001))*sun.shadowAttenuation;
                float depth=SampleSceneDepth(screenUV);
                float sceneEye=LinearEyeDepth(depth,_ZBufferParams);
                float waterEye=-TransformWorldToView(i.positionWS).z;
                float thickness=max(0,sceneEye-waterEye);
                half3 waterTint=lerp(_DeepColor.rgb,_ShallowColor.rgb,exp(-thickness*_Absorption));
                half3 diffuseLight=SampleSH(half3(0,1,0))+.35*sun.color*NoL*sun.shadowAttenuation;
                half3 scattered=waterTint*diffuseLight*_ScatterStrength;
                half3 color=scattered*(1-fresnel)+reflected*fresnel*_ReflectionStrength+directSpecular;
                color=MixFog(color,i.fog);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
