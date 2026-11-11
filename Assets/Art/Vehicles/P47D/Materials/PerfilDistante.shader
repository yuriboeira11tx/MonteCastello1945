Shader "FEB/Aviacao/PerfilDistante"
{
    Properties
    {
        _BaseColor ("Cor do avião à distância", Color) = (0.20,0.23,0.25,1)
        _AirDensity ("Fração da névoa no ar", Range(0.05,1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Perfil no céu"
            Tags { "LightMode"="UniversalForwardOnly" }
            ZTest LEqual
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _AirDensity;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float viewDepth : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.viewDepth = -pos.positionVS.z;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(i.positionCS);
                #endif
                // Dissolve only the last stretch of the visible route, before the far plane.
                float endFade = 1.0 - smoothstep(.90, .995, i.viewDepth / _ProjectionParams.z);
                clip(endFade - InterleavedGradientNoise(floor(i.positionCS.xy), 0));
                Light sun = GetMainLight();
                half diffuse = saturate(dot(normalize(i.normalWS), sun.direction));
                half3 paint = _BaseColor.rgb * (.78h + .5h * diffuse * sun.color);
                // Match the close model at the transition; above the ground haze retain the outline.
                float depthInHaze = lerp(i.viewDepth, i.viewDepth * _AirDensity,
                                          smoothstep(160.0, 330.0, i.viewDepth));
                return half4(MixFog(paint, ComputeFogFactorZ0ToFar(depthInHaze)), 1);
            }
            ENDHLSL
        }
    }
}
