// The game's one surface shader: every figure, prop, building and the board draw with it.
// Light comes in three bands (lit, half, shadow) with soft edges; the shadow band takes the
// palette's cool tint instead of black, and cast shadows, cloud-shadow cookies and SSAO fall into
// the same bands. Figures add a thin warm rim. A second pass draws the inverted-hull outline in a
// darkened copy of the surface's own colour, about 2 px wide up close and thinner with distance.
// Its LightMode is "Outline", which URP's opaque pass skips: the renderer's ToonOutlines feature
// (ProjectSetup) draws every outline in one sweep after the opaques, so the two passes don't
// alternate per object. Materials that should have none disable the "Outline" pass.
//
// Meshes from the import postprocessors carry their smoothed normal in TANGENT with w = 0, so the
// hull stays closed across hard edges. Any other mesh pushes its hull along its own normal.
Shader "LLTacticalWar/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor ("Emission (hit flash, dimming lift, glow)", Color) = (0, 0, 0, 1)

        _ShadeColor ("Shadow band light", Color) = (0.42, 0.45, 0.66, 1)
        _MidLevel ("Half band, from shadow to lit", Range(0, 1)) = 0.55
        _LitEdge ("Lit band starts at", Range(0, 1)) = 0.58
        _ShadeEdge ("Half band starts at", Range(0, 1)) = 0.36
        _BandSoft ("Band edge softness", Range(0.001, 0.5)) = 0.03
        _SkyBounce ("Sky bounce (ambient probe)", Range(0, 2)) = 0.3

        _RimColor ("Rim colour", Color) = (1, 0.81, 0.54, 1)
        _RimStrength ("Rim strength", Range(0, 2)) = 0
        _RimWidth ("Rim width", Range(0.01, 1)) = 0.34

        _OutlineWidth ("Outline width, px at 1080p up close", Range(0, 6)) = 2
        _OutlineNear ("Outline keeps full width within (m)", Float) = 28
        _OutlineMinScale ("Outline: thinnest share of full width", Range(0, 1)) = 0.4
        _OutlineDarken ("Outline: own colour times", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _EmissionColor, _ShadeColor, _RimColor;
            half _MidLevel, _LitEdge, _ShadeEdge, _BandSoft, _SkyBounce, _RimStrength, _RimWidth;
            float _OutlineWidth, _OutlineNear, _OutlineMinScale, _OutlineDarken;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        struct DepthAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
        };
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
            };

            Varyings ToonVert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            // h runs 0..1 (half-lambert times cast shadow); the result is the light on the surface.
            half3 Bands(half h, half3 shade, half3 lit)
            {
                half toHalf = smoothstep(_ShadeEdge - _BandSoft, _ShadeEdge + _BandSoft, h);
                half toLit = smoothstep(_LitEdge - _BandSoft, _LitEdge + _BandSoft, h);
                half3 mid = lerp(shade, lit, _MidLevel);
                return lerp(lerp(shade, mid, toHalf), lit, toLit);
            }

            half4 ToonFrag(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 n = normalize(i.normalWS);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.positionCS = i.positionCS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);

                // the key: its colour carries the cloud cookie and SSAO's direct share
                Light key = GetMainLight(inputData, inputData.shadowMask, ao);
                half NdotL = dot(n, key.direction);
                half h = (NdotL * 0.5 + 0.5) * key.shadowAttenuation * key.distanceAttenuation;
                half3 light = Bands(h, _ShadeColor.rgb * ao.indirectAmbientOcclusion, key.color);
                light += SampleSH(n) * (_SkyBounce * ao.indirectAmbientOcclusion);

                // fills and any other light: soft wrapped lambert, no bands
                #if defined(_ADDITIONAL_LIGHTS)
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light l = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, ao);
                    half wrap = saturate(dot(n, l.direction) * 0.6 + 0.4);
                    light += l.color * (l.distanceAttenuation * l.shadowAttenuation * wrap * wrap);
                }
                #endif
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, inputData, inputData.shadowMask, ao);
                    half wrap = saturate(dot(n, l.direction) * 0.6 + 0.4);
                    light += l.color * (l.distanceAttenuation * l.shadowAttenuation * wrap * wrap);
                LIGHT_LOOP_END
                #endif

                half3 color = albedo * light;

                // rim: a thin warm edge, strongest on the side the key lights
                half fresnel = 1.0 - saturate(dot(n, inputData.viewDirectionWS));
                half rim = smoothstep(1.0 - _RimWidth, 1.0 - _RimWidth + 0.12, fresnel) * saturate(NdotL + 0.6);
                color += _RimColor.rgb * (rim * _RimStrength) * (albedo * 0.5 + 0.5);

                color += _EmissionColor.rgb;
                // fog per pixel: the meadow is one quad 400 m across, too coarse to fog by vertex
                return half4(MixFog(color, ComputeFogFactor(TransformWorldToHClip(i.positionWS).z)), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "Outline" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings OutlineVert(Attributes v)
            {
                Varyings o;
                float3 pushOS = abs(v.tangentOS.w) < 0.5 ? v.tangentOS.xyz : v.normalOS;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float4 positionCS = TransformWorldToHClip(positionWS);
                float2 push = mul((float3x3)GetWorldToHClipMatrix(), TransformObjectToWorldNormal(pushOS)).xy;
                push = dot(push, push) > 1e-10 ? normalize(push) : float2(0, 0);
                // pixels at 1080p, full width near the camera and thinner far off, then into clip space
                float scale = clamp(_OutlineNear / distance(GetCameraPositionWS(), positionWS), _OutlineMinScale, 1.0);
                float px = _OutlineWidth * scale * _ScreenParams.y / 1080.0;
                positionCS.xy += push * (px * 2.0 * positionCS.w) / _ScreenParams.xy;
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(positionCS.z);
                return o;
            }

            half4 OutlineFrag(Varyings i) : SV_Target
            {
                half3 own = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 color = own * _OutlineDarken + _EmissionColor.rgb * 0.5;
                return half4(MixFog(color, i.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment NoColor
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 ShadowVert(DepthAttributes v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 toLight = normalize(_LightPosition - positionWS);
                #else
                    float3 toLight = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, toLight)));
            }

            half4 NoColor() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment NoColor

            float4 DepthVert(DepthAttributes v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half4 NoColor() : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth and normals for SSAO's prepass.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVert(DepthAttributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 NormalsFrag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(n) * 0.5 + 0.5)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
