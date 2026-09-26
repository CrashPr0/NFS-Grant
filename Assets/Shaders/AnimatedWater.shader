// GPU-animated translucent water for the hub waterfall and basin. Driven
// entirely by the built-in _Time uniform, so it animates with zero CPU /
// script cost - the right tool for an attention study that must hold 72+
// Hz on Quest and run in WebGL. Pipeline-independent CG pass, matching
// NSFGrant/TextOccluded and NSFGrant/UnlitTransparentColor.
//
// Two modes (uniform branch, so only one path runs per material):
//   _Streaks = 1  falling sheet: vertically stretched, two-layer tiling
//                 value noise scrolling along _ScrollSpeed, white water at
//                 the lip and impact zone, ragged soft side edges.
//   _Streaks = 0  still pool: slow two-layer shimmer, view-angle (Fresnel)
//                 sheen and small sparkles.
//
// Pattern motion: the texture moves ALONG _ScrollSpeed (sample = uv -
// scroll * t). The previous version added scroll*t to the sample point,
// which moved the bands the opposite way - the "downward" sheets flowed up.
//
// Precision: the noise tiles with an integer lattice period and the scroll
// offset is wrapped by exactly that period, so sample coordinates stay
// small no matter how long a session runs (no drift/stepping on Quest's
// GPUs), and the hash is sin-free for the same reason.
//
// Used for the namesake "waterfall" feature only; content panels stay
// static so motion does not confound the gaze/attention measures.
Shader "NSFGrant/AnimatedWater"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.75, 0.95, 0.5)
        _FoamColor ("Foam/Highlight Color", Color) = (1, 1, 1, 1)
        _ScrollSpeed ("Scroll Speed (xy, UV/s)", Vector) = (0, -0.6, 0, 0)
        _WaveScale ("Pattern Scale", Float) = 9
        _WaveSpeed ("Turbulence Speed", Float) = 2.2
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0.35
        _Streaks ("Falling Sheet (1) / Still Pool (0)", Range(0, 1)) = 1
        _EdgeSoftness ("Sheet Side Edge Softness", Range(0.01, 0.5)) = 0.14
        _Seed ("Pattern Seed", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _FoamColor;
            float4 _ScrollSpeed;
            float _WaveScale;
            float _WaveSpeed;
            float _FoamStrength;
            float _Streaks;
            float _EdgeSoftness;
            float _Seed;

            // Lattice period of the tiling noise (cells). Scroll offsets are
            // wrapped by this, which is seamless because the noise tiles.
            static const float2 Period = float2(64.0, 64.0);

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // Dave Hoskins' sin-free hash: stable on mobile GPUs.
            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 WrapCell(float2 c)
            {
                return c - Period * floor(c / Period);
            }

            // Value noise that tiles every Period cells, in [0, 1].
            float TileNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float2 seed = float2(_Seed * 17.0, _Seed * 31.0);
                float a = Hash12(WrapCell(i) + seed);
                float b = Hash12(WrapCell(i + float2(1, 0)) + seed);
                float c = Hash12(WrapCell(i + float2(0, 1)) + seed);
                float d = Hash12(WrapCell(i + float2(1, 1)) + seed);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Offset (in cells) for a pattern moving along `velocityCells`,
            // wrapped by the tile period so it never grows large.
            float2 ScrollOffset(float2 velocityCells, float t)
            {
                float2 o = velocityCells * t;
                return o - Period * floor(o / Period);
            }

            fixed4 FallingSheet(v2f i, float t)
            {
                float2 uv = i.uv;
                // Streaks: narrow across, long down the fall.
                float2 cellScale = float2(_WaveScale * 1.6, _WaveScale * 0.35);

                // Gentle sideways wobble so streaks don't read as rails.
                float wobble = 0.35 * sin(uv.y * 5.0 + t * _WaveSpeed * 0.5);

                float2 p1 = uv * cellScale + float2(wobble, 0);
                p1 -= ScrollOffset(_ScrollSpeed.xy * cellScale, t);
                float n1 = TileNoise(p1);

                // Finer, faster layer on top: turbulence + depth.
                float2 cellScale2 = cellScale * float2(2.1, 1.7);
                float2 p2 = uv * cellScale2 + float2(-wobble * 1.5, 0) + 7.3;
                p2 -= ScrollOffset(_ScrollSpeed.xy * 1.6 * cellScale2, t);
                float n2 = TileNoise(p2);

                float streak = saturate(n1 * 0.65 + n2 * 0.45 - 0.05);

                // White water: crest highlights, the lip at the top of the
                // fall, and churning froth where it hits the basin.
                float crest = smoothstep(0.62, 0.9, streak);
                float lip = 1.0 - smoothstep(0.0, 0.07, 1.0 - uv.y);
                float impact = 1.0 - smoothstep(0.0, 0.18, uv.y);
                float froth = (lip * 0.7 + impact) * (0.45 + 0.55 * n2);
                float foam = saturate(crest * _FoamStrength + froth * _FoamStrength * 1.6);

                fixed3 body = _Color.rgb * lerp(0.8, 1.2, streak);
                fixed3 rgb = lerp(body, _FoamColor.rgb, foam);

                float alpha = _Color.a * (0.6 + 0.6 * streak);
                alpha = max(alpha, foam * _FoamColor.a * 0.85);

                // Ragged, soft side edges instead of a hard quad outline.
                float edge = min(uv.x, 1.0 - uv.x) + (n1 - 0.5) * 0.07;
                alpha *= smoothstep(0.0, _EdgeSoftness, edge);
                return fixed4(rgb, saturate(alpha));
            }

            fixed4 StillPool(v2f i, float t)
            {
                float2 uv = i.uv;
                float2 cellScale = float2(_WaveScale, _WaveScale);

                // Two layers drifting in different directions -> shimmer.
                float2 drift = _ScrollSpeed.xy * cellScale;
                float n1 = TileNoise(uv * cellScale - ScrollOffset(drift, t));
                float n2 = TileNoise(uv * cellScale * 1.9 + 3.1
                                     - ScrollOffset(float2(-drift.y, drift.x) * 1.4 * _WaveSpeed, t));
                float shimmer = n1 * 0.55 + n2 * 0.45;

                // Brighter, more opaque at grazing angles, like real water.
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(i.viewDir);
                float fresnel = pow(1.0 - saturate(abs(dot(n, v))), 3.0);

                fixed3 rgb = _Color.rgb * lerp(0.85, 1.15, shimmer);
                rgb = lerp(rgb, _FoamColor.rgb, fresnel * 0.45);

                // Small sparkles where both layers peak together.
                float sparkle = smoothstep(0.78, 0.95, n1 * n2 * 1.6);
                rgb += _FoamColor.rgb * sparkle * _FoamStrength;

                float alpha = saturate(_Color.a * (0.8 + 0.2 * shimmer) + fresnel * 0.3);
                return fixed4(rgb, alpha);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                fixed4 col;
                UNITY_BRANCH
                if (_Streaks > 0.5)
                {
                    col = FallingSheet(i, t);
                }
                else
                {
                    col = StillPool(i, t);
                }
                // Match the scene's linear fog so the water recedes with
                // everything else when seen from the far rooms.
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
