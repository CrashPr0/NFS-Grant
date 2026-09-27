// Cel-shaded solids for the procedural potted trees: the branching wood
// and the ceramic pot (ProceduralPlanter). Colors come from the mesh's
// vertex colors (rgb albedo, a = glaze gloss), so one shader covers both:
//   - Soft-edged two-band lighting, the shadow side tinted cool, from the
//     same planter-relative key light as NSFGrant/StylizedLeaves.
//   - _BarkStreaks: vertical bark grooves from a noise that wraps around
//     each branch (periodic in the around-the-branch UV, so no seam).
//   - Glaze: a stepped highlight where vertex alpha > 0 (the pot's glaze
//     and brass band; soil and wood stay matte).
//   - Rim light, fog, and the shared breeze (NSFGrantTree.cginc) - set
//     _WindStrength 0 for the pot, so only the tree moves.
// Pipeline-independent CG pass with stereo-instancing macros and GPU
// instancing, like the other NSFGrant shaders.
Shader "NSFGrant/StylizedSolid"
{
    Properties
    {
        _ShadowTint ("Shadow Tint", Color) = (0.42, 0.46, 0.6, 1)
        _LightCutoff ("Light Cutoff", Range(-1, 1)) = 0.0
        _BandSoftness ("Band Softness", Range(0.01, 0.5)) = 0.18
        _AmbientSky ("Ambient Sky", Color) = (0.2, 0.21, 0.24, 1)
        _BarkStreaks ("Bark Streaks (0/1)", Range(0, 1)) = 0
        _BarkScale ("Bark Groove Density", Float) = 3
        _Gloss ("Glaze Highlight", Range(0, 1)) = 0
        _GlossPower ("Glaze Tightness", Range(8, 256)) = 90
        _RimColor ("Rim", Color) = (0.55, 0.6, 0.7, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.2
        _WindStrength ("Sway (m at crown top)", Range(0, 0.05)) = 0
        _WindSpeed ("Sway Speed", Float) = 1.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "NSFGrantTree.cginc"

            fixed4 _ShadowTint, _AmbientSky, _RimColor;
            float _LightCutoff, _BandSoftness, _BarkStreaks, _BarkScale;
            float _Gloss, _GlossPower, _RimStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;     // rgb albedo, a gloss
                float2 uv : TEXCOORD0;    // x around the branch/pot, y along it (m-ish)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float4 color : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                worldPos += TreeWind(worldPos, TreeOrigin());
                o.pos = UnityWorldToClipPos(worldPos);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = worldPos;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 albedo = i.color.rgb;

                if (_BarkStreaks > 0.5)
                {
                    // Around-the-branch angle mapped onto a circle so the
                    // noise wraps with no seam; stretched along the branch.
                    float ang = i.uv.x * 6.2831853;
                    float3 p = float3(cos(ang) * _BarkScale, sin(ang) * _BarkScale, i.uv.y * 0.9);
                    float groove = TreeNoise(p) * 0.65 + TreeNoise(p * 2.3 + 7.1) * 0.35;
                    albedo *= lerp(0.62, 1.12, smoothstep(0.25, 0.75, groove));
                }

                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 keyDir = TreeKeyDir();
                float ndl = dot(n, keyDir);
                float lit = smoothstep(_LightCutoff - _BandSoftness, _LightCutoff + _BandSoftness, ndl);

                float3 col = albedo * lerp(_ShadowTint.rgb, kTreeKeyColor, lit);
                col += albedo * _AmbientSky.rgb * (n.y * 0.5 + 0.5);

                float gloss = i.color.a * _Gloss;
                if (gloss > 0.0)
                {
                    float3 h = normalize(keyDir + viewDir);
                    float spec = pow(saturate(dot(n, h)), _GlossPower);
                    col += kTreeKeyColor * smoothstep(0.35, 0.5, spec) * 0.55 * gloss;
                }

                float rim = 1.0 - saturate(dot(n, viewDir));
                col += _RimColor.rgb * (rim * rim * rim) * _RimStrength * albedo * 2.0;

                fixed4 outCol = fixed4(col, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, outCol);
                return outCol;
            }
            ENDCG
        }
    }
}
