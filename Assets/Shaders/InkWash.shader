// The board's tile highlights, painted like a general's map: each region (the tiles sharing one colour
// and strength in the tile-data texture, one texel per tile) gets a watercolour wash that pools darker
// toward its edge, diagonal ink hatching (crossed for the danger zone), and one inked outline round the
// region rather than a box round every tile. Hatching is in world space, so it lines up across tiles.
Shader "LLTacticalWar/InkWash"
{
    Properties
    {
        _MainTex ("Tile data (rgb colour, a strength), one texel per tile", 2D) = "black" {}
        _Ink ("Ink", Color) = (0.17, 0.2, 0.25, 1)
        _Tiles ("Tiles across, down", Vector) = (14, 10, 0, 0)
        _Cross ("Cross-hatch", Float) = 0
        _Spacing ("Hatch spacing (world units)", Float) = 0.3
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex WashVert
            #pragma fragment WashFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Ink;
                float4 _Tiles;
                half _Cross;
                float _Spacing;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };

            Varyings WashVert(Attributes v)
            {
                Varyings o;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = v.uv;
                return o;
            }

            half4 Cell(int2 id)
            {
                if (id.x < 0 || id.y < 0 || id.x >= (int)_Tiles.x || id.y >= (int)_Tiles.y) return 0;
                return LOAD_TEXTURE2D(_MainTex, id);
            }

            bool Same(half4 a, half4 b) { return all(abs(a - b) < 0.02); }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }

            // 1 on an ink line of the hatch family along `u` (world units / spacing), anti-aliased.
            float Hatch(float u, float width)
            {
                float d = 0.5 - abs(frac(u) - 0.5);
                float aa = fwidth(u);
                return 1.0 - smoothstep(width - aa, width + aa, d);
            }

            half4 WashFrag(Varyings i) : SV_Target
            {
                float2 g = i.uv * _Tiles.xy;
                int2 id = (int2)floor(g);
                float2 f = frac(g);
                half4 c = Cell(id);
                if (c.a < 0.004) return 0;

                // distance (tile units) to the region's border: sides whose neighbour differs, and the
                // corner point of an inside corner
                float d = 1e3;
                bool w = Same(Cell(id + int2(-1, 0)), c), e = Same(Cell(id + int2(1, 0)), c);
                bool s = Same(Cell(id + int2(0, -1)), c), n = Same(Cell(id + int2(0, 1)), c);
                if (!w) d = min(d, f.x);
                if (!e) d = min(d, 1.0 - f.x);
                if (!s) d = min(d, f.y);
                if (!n) d = min(d, 1.0 - f.y);
                if (w && s && !Same(Cell(id + int2(-1, -1)), c)) d = min(d, length(f));
                if (e && s && !Same(Cell(id + int2(1, -1)), c)) d = min(d, length(f - float2(1, 0)));
                if (w && n && !Same(Cell(id + int2(-1, 1)), c)) d = min(d, length(f - float2(0, 1)));
                if (e && n && !Same(Cell(id + int2(1, 1)), c)) d = min(d, length(f - float2(1, 1)));

                float2 p = i.world.xz;
                float aa = fwidth(g.x);
                float pool = 1.0 - smoothstep(0.0, 0.3, d);
                float blot = Noise(p * 0.9) * 0.6 + Noise(p * 2.7) * 0.4;
                half alpha = c.a * (0.7 + 0.4 * pool) * (0.8 + 0.4 * blot);

                // hatching stops short of the outline, like a hand that keeps inside its line
                float u = (p.x + p.y) / _Spacing;
                float lines = Hatch(u, 0.11);
                if (_Cross > 0.5) lines = max(lines, Hatch((p.x - p.y) / _Spacing, 0.11));
                lines *= smoothstep(0.1, 0.2, d) * (0.75 + 0.25 * Noise(p * 3.1));

                half3 dark = c.rgb * 0.5;   // the same pigment, laid on thick: deep blue, deep crimson
                half3 col = lerp(c.rgb, dark, 0.3 * pool);
                col = lerp(col, dark, lines * 0.85);
                alpha = saturate(alpha + lines * c.a * 0.9);

                // the outline, a touch in from the region's edge
                float edge = 1.0 - smoothstep(0.036 - aa, 0.036 + aa, abs(d - 0.08));
                col = lerp(col, lerp(dark, _Ink.rgb, 0.3), edge);
                alpha = lerp(alpha, saturate(0.55 + c.a), edge);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
