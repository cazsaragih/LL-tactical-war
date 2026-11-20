// The sky: the palette's zenith blue easing down to the haze colour at the horizon, which is also
// the fog colour, so distant hills and mountains dissolve into it. Below the horizon it holds the haze.
Shader "LLTacticalWar/SkyGradient"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.56, 0.72, 0.88, 1)
        _Horizon ("Horizon haze", Color) = (0.66, 0.74, 0.82, 1)
        _Falloff ("Haze band height", Range(0.05, 4)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment SkyFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon;
                half _Falloff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            Varyings SkyVert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.direction = v.positionOS.xyz;
                return o;
            }

            half4 SkyFrag(Varyings i) : SV_Target
            {
                half up = saturate(normalize(i.direction).y);
                half t = 1.0 - pow(1.0 - up, 1.0 / _Falloff);
                return half4(lerp(_Horizon.rgb, _Zenith.rgb, t), 1);
            }
            ENDHLSL
        }
    }
}
