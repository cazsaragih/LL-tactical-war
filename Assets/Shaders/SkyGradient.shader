// The sky: the haze colour at the horizon (also the fog colour, so distant hills dissolve into it)
// turning quickly to the palette's zenith blue, so even a low camera sees blue sky above the hills,
// with a faint warm glow hugging the horizon. Below the horizon it holds the haze.
Shader "LLTacticalWar/SkyGradient"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.56, 0.72, 0.88, 1)
        _Horizon ("Horizon haze", Color) = (0.66, 0.74, 0.82, 1)
        _Band ("Elevation where the blue is full (sine)", Range(0.05, 1)) = 0.34
        _Glow ("Horizon glow", Color) = (1, 0.94, 0.85, 1)
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
                half4 _Zenith, _Horizon, _Glow;
                half _Band;
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
                half t = pow(saturate(up / _Band), 0.62);
                half3 sky = lerp(_Horizon.rgb, _Zenith.rgb, t);
                sky = lerp(sky, _Glow.rgb, 0.22 * exp(-up * 30.0) * step(0.0005, up));
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
}
