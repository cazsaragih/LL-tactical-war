// The game's one surface shader: every figure, prop, building and the board draw with it, cel-shaded.
// Light takes two flat tones. Lit is the sun plus a flat sky lift; shadow is the palette's cool tone,
// never black or grey. The terminator, a cast shadow's edge and a cloud shadow's edge are each a step
// one pixel wide (fwidth), hard but anti-aliased: the soft shadow map's penumbra (and the soft cloud
// cookie) is cut at its middle, so a cast shadow gets a clean rounded edge and the same tone as the
// surface's own shadow side. Styles add a small stepped highlight on grey and white texels (steel, linen, never skin or team
// cloth) and a stepped warm rim on the sunward silhouette. A second pass draws the inverted-hull outline
// in a dark tone of the surface's own colour, about 3 px wide up close and thinner with distance. Its
// LightMode is "Outline", which URP's opaque pass skips: the renderer's ToonOutlines feature
// (ProjectSetup) draws every outline in one sweep after the opaques, so the two passes don't alternate
// per object. Materials that should have none disable the "Outline" pass.
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

        _ShadeColor ("Shadow tone light", Color) = (0.42, 0.45, 0.66, 1)
        _Terminator ("Lit where N.L is above", Range(-0.5, 0.5)) = 0.05
        _SkyBounce ("Flat sky lift on the lit tone", Range(0, 1)) = 0.3

        _SpecStrength ("Highlight strength", Range(0, 2)) = 0
        _SpecEdge ("Highlight where N.H is above (smaller spot nearer 1)", Range(0.8, 1)) = 0.95

        _RimColor ("Rim colour", Color) = (1, 0.81, 0.54, 1)
        _RimStrength ("Rim strength", Range(0, 2)) = 0
        _RimWidth ("Rim width (fresnel)", Range(0.01, 1)) = 0.3

        _OutlineWidth ("Outline width, px at 1080p up close", Range(0, 6)) = 3
        _OutlineNear ("Outline keeps full width within (m)", Float) = 30
        _OutlineMinScale ("Outline: thinnest share of full width", Range(0, 1)) = 0.45
        _OutlineDarken ("Outline: own colour times", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _EmissionColor, _ShadeColor, _RimColor;
            half _Terminator, _SkyBounce, _SpecStrength, _SpecEdge, _RimStrength, _RimWidth;
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
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
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

            half _CloudShade;   // global (CloudShadows): the share of sunlight under a cloud

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

            // 0 below edge, 1 above it, the change spread over one pixel: hard, but never stair-stepped
            half CelStep(half edge, half x)
            {
                return saturate((x - edge) / max(fwidth(x), 1e-4) + 0.5);
            }

            half4 ToonFrag(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 n = normalize(i.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // the key; its cloud cookie (CloudShadows: 1 clear sky, 0 cloud, soft) is stepped like
                // every other light edge, so a cloud's shadow is a flat patch of _CloudShade sunlight
                Light key = GetMainLight();
                key.shadowAttenuation = MainLightShadow(TransformWorldToShadowCoord(i.positionWS), i.positionWS, half4(1, 1, 1, 1), _MainLightOcclusionProbes);
                #if defined(_LIGHT_COOKIES)
                    key.color *= lerp(_CloudShade, 1.0, CelStep(0.5, SampleMainLightCookie(i.positionWS).r));
                #endif
                half NdotL = dot(n, key.direction);
                half sunlit = CelStep(0.5, key.shadowAttenuation * key.distanceAttenuation);
                half lit = CelStep(_Terminator, NdotL) * sunlit;
                half3 color = albedo * lerp(_ShadeColor.rgb, key.color + SampleSH(half3(0, 1, 0)) * _SkyBounce, lit);

                // highlight: a small stepped spot where the albedo is grey or white (steel, linen)
                half hi = max(albedo.r, max(albedo.g, albedo.b));
                half lo = min(albedo.r, min(albedo.g, albedo.b));
                half gloss = saturate(1.0 - 3.5 * (hi - lo) / max(hi, 1e-3));
                half spec = CelStep(_SpecEdge, dot(n, normalize(key.direction + view))) * lit * gloss;
                color += key.color * (spec * _SpecStrength);

                // rim: a stepped warm band inside the silhouette, on the side the sun reaches
                half fresnel = 1.0 - saturate(dot(n, view));
                half rim = CelStep(1.0 - _RimWidth, fresnel) * CelStep(-0.2, NdotL) * sunlit;
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
    }
}
