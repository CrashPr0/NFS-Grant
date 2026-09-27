// Stylized "fluffy" tree leaves for the procedural potted trees, after
// Harry Alisavakis' stylized tree leaves and Habrador's fluffy-tree trick:
//   - The crown is a cloud of leaf cards (ProceduralPlanter). All four
//     corners of a card sit at its center in the mesh; this vertex shader
//     expands each card toward the viewer (UV = corner, TEXCOORD1 = half
//     size and spin), so no card is ever seen edge-on as a sliver. Cards
//     face the camera position rather than the view plane, which keeps the
//     two eyes' cards consistent in VR.
//   - Normals come from the crown's ellipsoid (_CrownCenter/_CrownRadii,
//     object space), so the whole crown lights as one soft volume - lit
//     top, shaded underside - instead of as flat cards.
//   - Cel lighting: a soft-edged light cutoff between shadow and lit
//     greens, plus a highlight band on the sunny side.
//   - Fake subsurface scattering: leaves glow when the viewer looks
//     toward the key light through the crown; outer cards glow most.
//   - Rim light on the silhouette; inner-crown occlusion.
//   - Motion: the shared breeze (NSFGrantTree.cginc) plus a small per-card
//     flutter; a centimetre or two at most, so the planters (identical at
//     every doorway) never compete with exhibits for attention.
// Edges: alpha is mip-compensated and sharpened to ~1 px, then written to
// coverage (AlphaToMask) - smooth under MSAA, a crisp alpha test without.
// Stereo-instancing macros throughout; GPU instancing.
Shader "NSFGrant/StylizedLeaves"
{
    Properties
    {
        _MainTex ("Leaf Clump (RGB shade, A mask)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _ShadowColor ("Shadow", Color) = (0.05, 0.15, 0.13, 1)
        _MidColor ("Lit", Color) = (0.2, 0.41, 0.16, 1)
        _LitColor ("Highlight", Color) = (0.52, 0.7, 0.22, 1)
        _LightCutoff ("Light Cutoff", Range(-1, 1)) = 0.0
        _BandSoftness ("Band Softness", Range(0.01, 0.5)) = 0.12
        _SSSColor ("Subsurface", Color) = (0.62, 0.84, 0.26, 1)
        _SSSPower ("Subsurface Concentration", Range(1, 16)) = 3
        _SSSStrength ("Subsurface Strength", Range(0, 2)) = 0.6
        _RimColor ("Rim", Color) = (0.55, 0.78, 0.36, 1)
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35
        _CrownCenter ("Crown Center (object)", Vector) = (0, 1.3, 0, 0)
        _CrownRadii ("Crown Radii (xz, y)", Vector) = (0.4, 0.35, 0, 0)
        _WindStrength ("Sway (m at crown top)", Range(0, 0.05)) = 0.015
        _WindSpeed ("Sway Speed", Float) = 1.1
        _FlutterStrength ("Leaf Flutter (m)", Range(0, 0.03)) = 0.008
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        AlphaToMask On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "NSFGrantTree.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Cutoff;
            fixed4 _ShadowColor, _MidColor, _LitColor, _SSSColor, _RimColor;
            float _LightCutoff, _BandSoftness, _SSSPower, _SSSStrength, _RimStrength;
            float4 _CrownCenter, _CrownRadii;
            float _FlutterStrength;

            struct appdata
            {
                float4 vertex : POSITION;    // card center (same for all 4 corners)
                float4 color : COLOR;        // r: occlusion, g: tint, b: flutter phase, a: outerness
                float2 uv : TEXCOORD0;       // corner 0..1
                float2 card : TEXCOORD1;     // x: half size (m), y: spin (rad)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 crownOffset : TEXCOORD1;   // from crown center, in crown radii
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

                float3 center = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
                float scale = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20));

                // Camera-facing basis at the card center.
                float3 toCam = normalize(_WorldSpaceCameraPos - center);
                float3 right = normalize(cross(float3(0.0, 1.0, 0.0), toCam) + float3(1e-4, 0.0, 0.0));
                float3 up = cross(toCam, right);
                float s, c;
                sincos(v.card.y, s, c);
                float2 corner = v.uv * 2.0 - 1.0;
                float2 rotated = float2(corner.x * c - corner.y * s, corner.x * s + corner.y * c);
                float3 worldPos = center + (right * rotated.x + up * rotated.y) * (v.card.x * scale);

                // Crown-ellipsoid coordinates for lighting (before motion).
                float3 crown = mul(unity_ObjectToWorld, float4(_CrownCenter.xyz, 1.0)).xyz;
                o.crownOffset = (worldPos - crown) / float3(_CrownRadii.x, _CrownRadii.y, _CrownRadii.x);

                // Per-card flutter (the whole card bobs), then the breeze.
                float flutter = sin(_Time.y * 2.7 + v.color.b * 6.2831853);
                worldPos.y += flutter * _FlutterStrength * v.color.a;
                worldPos += TreeWind(worldPos, TreeOrigin());

                o.pos = UnityWorldToClipPos(worldPos);
                o.uv = v.uv;
                o.worldPos = worldPos;
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                // Keep leaf coverage from thinning out in the mips, then
                // sharpen the edge to about one pixel.
                float2 texel = i.uv * _MainTex_TexelSize.zw;
                float2 dx = ddx(texel), dy = ddy(texel);
                float mip = max(0.0, 0.5 * log2(max(dot(dx, dx), dot(dy, dy))));
                float a = tex.a * (1.0 + mip * 0.25);
                a = (a - _Cutoff) / max(fwidth(a), 0.0001) + 0.5;
                clip(a);

                // Ellipsoid normal, nudged per card so neighbouring cards
                // don't shade identically.
                float depth = length(i.crownOffset);
                float3 n = normalize(i.crownOffset + (i.color.g - 0.5) * 0.35);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 keyDir = TreeKeyDir();
                float ndl = dot(n, keyDir);

                float lit = smoothstep(_LightCutoff - _BandSoftness, _LightCutoff + _BandSoftness, ndl);
                float3 col = lerp(_ShadowColor.rgb, _MidColor.rgb, lit);
                col = lerp(col, _LitColor.rgb, smoothstep(0.5, 0.8, ndl) * 0.9);
                // Per-card tint (slightly yellower or bluer) and the sprite's
                // own leaf shading.
                col *= lerp(float3(0.9, 0.96, 1.08), float3(1.1, 1.05, 0.88), i.color.g);
                col *= tex.rgb;
                // Occlusion: deep inside the crown and on inner cards.
                col *= lerp(0.45, 1.0, saturate(depth * 1.1)) * lerp(0.7, 1.0, i.color.r);

                float sss = pow(saturate(dot(viewDir, -keyDir)), _SSSPower);
                col += _SSSColor.rgb * sss * _SSSStrength * saturate(depth) * i.color.a * tex.rgb;

                float rim = 1.0 - saturate(dot(n, viewDir));
                col += _RimColor.rgb * (rim * rim * rim) * _RimStrength * (0.35 + 0.65 * lit) * tex.rgb;

                fixed4 outCol = fixed4(col * kTreeKeyColor, saturate(a));
                UNITY_APPLY_FOG(i.fogCoord, outCol);
                return outCol;
            }
            ENDCG
        }
    }
}
