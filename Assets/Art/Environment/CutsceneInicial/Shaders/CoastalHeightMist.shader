Shader "FEB/Environment/Coastal Height Mist"
{
    Properties
    {
        _MistColor ("Mist Color", Color) = (0.57,0.65,0.70,1)
        _Density ("Density", Range(0,0.005)) = 0.0008
        _HeightFalloff ("Height Falloff (m)", Float) = 75
        _SeaLevel ("Sea Level", Float) = 3.82
        _NoiseTex ("Soft Density Variation", 2D) = "gray" {}
        _NoiseStrength ("Density Variation", Range(0,1)) = 0.2
        _MaxOpacity ("Maximum Opacity", Range(0,1)) = 0.8
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }
        Pass
        {
            Name "CoastalHeightMist"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Front
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MistColor;
                float _Density, _HeightFalloff, _SeaLevel, _NoiseStrength, _MaxOpacity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                // The box is a screen coverage proxy. Its exit faces must not be clipped by the
                // camera far plane; opaque depth and the analytic box interval do the real clipping.
                o.positionCS.z=o.positionCS.w*0.5;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                float depth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
                #endif
                float3 surfaceWS=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                float3 origin=GetCameraPositionWS();
                float3 offset=surfaceWS-origin;
                float sceneDistance=length(offset);
                float3 direction=offset/max(sceneDistance,0.0001);
                // Clip atmospheric integration against both opaque depth and the transparent sea plane.
                if(direction.y < -0.00001)
                {
                    float seaDistance=(_SeaLevel-origin.y)/direction.y;
                    if(seaDistance>0) sceneDistance=min(sceneDistance,seaDistance);
                }
                float3 originOS=TransformWorldToObject(origin);
                float3 directionOS=TransformWorldToObjectDir(direction,false);
                float3 safeDirection=sign(directionOS+0.000000001)*max(abs(directionOS),0.00000001);
                float3 t0=(-0.5-originOS)/safeDirection;
                float3 t1=(0.5-originOS)/safeDirection;
                float3 lo=min(t0,t1), hi=max(t0,t1);
                float entry=max(max(lo.x,lo.y),max(lo.z,0));
                float exitDistance=min(min(hi.x,hi.y),min(hi.z,sceneDistance));
                float distanceInMist=max(0,exitDistance-entry);
                clip(distanceInMist-0.001);
                float heightScale=max(1,_HeightFalloff);
                float h0=max(0,origin.y+direction.y*entry-_SeaLevel);
                float h1=max(0,origin.y+direction.y*exitDistance-_SeaLevel);
                float averageDensity;
                if(abs(h1-h0)>0.01) averageDensity=heightScale*(exp(-h0/heightScale)-exp(-h1/heightScale))/(h1-h0);
                else averageDensity=exp(-h0/heightScale);
                float3 midpoint=origin+direction*(entry+distanceInMist*0.5);
                float noise=SAMPLE_TEXTURE2D(_NoiseTex,sampler_NoiseTex,midpoint.xz*0.00035+float2(_Time.y*0.0006,0)).r;
                float density=_Density*lerp(1,0.65+noise*0.7,_NoiseStrength);
                float opacity=min(_MaxOpacity,1-exp(-density*distanceInMist*averageDensity));
                return half4(_MistColor.rgb,opacity);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
