// The room docents (MINERVA / HINA / BHUMI) as projected AI guides: an
// opaque, cel-shaded figure (so it reads solidly in the headset and never
// needs transparency sorting) dressed up with cheap "hologram" cues:
//   - Two-band toon lighting from a key light fixed in the docent's own
//     frame (like the planter trees), so every room's docent is lit
//     identically whatever way its room faces.
//   - A strong theme-colored Fresnel rim, broken by fine scanlines that
//     drift upward.
//   - A scan sweep: every few seconds a soft band of light rises from
//     the hem to the head.
//   - Emissive trim where vertex alpha > 0 (collar, hem, sash, the
//     visor face, halo, pedestal ring), gently breathing.
//   - The visor face (head mesh, _Visor 1): two eye dots inside the
//     visor band, drawn from object-space position, blinking now and then.
//   - The stole (body, _Stole 1): two theme bands down the robe's front.
//   - _Highlight (set per docent by DocentPresence while pointed at or
//     just clicked) brightens rim and trim.
// Vertex colors: rgb albedo, a = emission mask. uv2.x = breathing weight
// (chest), uv2.y = height for the sweep (m above the pedestal).
// Pipeline-independent CG pass with stereo/instancing macros, like the
// other NSFGrant shaders.
Shader "NSFGrant/DocentHologram"
{
    Properties
    {
        _Theme ("Theme Color", Color) = (0.3, 0.7, 1, 1)
        _Tint ("Albedo Tint", Color) = (1, 1, 1, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.45, 0.5, 0.66, 1)
        _AmbientSky ("Ambient Sky", Color) = (0.22, 0.23, 0.27, 1)
        _BandSoftness ("Band Softness", Range(0.01, 0.5)) = 0.12
        _RimStrength ("Rim Strength", Range(0, 3)) = 0.85
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.0
        _ScanDensity ("Scanlines per m", Float) = 55
        _Emission ("Trim Emission", Range(0, 4)) = 1.6
        _SweepPeriod ("Sweep Period (s)", Float) = 5.5
        _Visor ("Visor Face (0/1)", Range(0, 1)) = 0
        _Stole ("Stole (0/1)", Range(0, 1)) = 0
        _Breath ("Breathing Amount", Range(0, 0.05)) = 0.014
        _Highlight ("Highlight", Range(0, 1)) = 0
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

            fixed4 _Theme, _Tint, _ShadowTint, _AmbientSky;
            float _BandSoftness, _RimStrength, _RimPower, _ScanDensity;
            float _Emission, _SweepPeriod, _Visor, _Stole, _Breath, _Highlight;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv2 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float4 color : TEXCOORD2;
                float3 objPos : TEXCOORD3;
                float height : TEXCOORD4;
                UNITY_FOG_COORDS(5)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Breathing: the chest swells a little, slowly.
                float breath = sin(_Time.y * 1.7) * _Breath * v.uv2.x;
                float4 p = v.vertex;
                p.xz *= 1.0 + breath;
                p.y += breath * 0.3;
                o.objPos = v.vertex.xyz;
                o.worldPos = mul(unity_ObjectToWorld, p).xyz;
                o.pos = UnityWorldToClipPos(o.worldPos);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                o.height = v.uv2.y;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // Key light fixed in the docent's frame: up, in front (+Z, the
            // visitor side) and to the docent's right.
            float3 KeyDir()
            {
                return normalize(mul((float3x3)unity_ObjectToWorld, float3(0.4, 0.75, 0.55)));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 albedo = i.color.rgb * _Tint.rgb;
                float t = _Time.y;

                // Stole (body only): two theme-colored bands down the front
                // from the shoulders to the knees, each ending in a glowing
                // hem - drawn from object-space angle and height, so the
                // edges are crisp and the robe mesh stays shared by all
                // rooms.
                float stoleHem = 0.0;
                if (_Stole > 0.5)
                {
                    float3 op = i.objPos;
                    float ang = abs(atan2(op.x, op.z));
                    float band = smoothstep(0.16, 0.19, ang) * (1.0 - smoothstep(0.43, 0.46, ang)) * step(0.0, op.z);
                    float span = smoothstep(0.33, 0.345, op.y) * (1.0 - smoothstep(1.37, 1.4, op.y));
                    albedo = lerp(albedo, _Theme.rgb * 1.25 + 0.03, band * span);
                    stoleHem = band * smoothstep(0.33, 0.345, op.y) * (1.0 - smoothstep(0.37, 0.385, op.y));
                }

                // Toon key + sky fill.
                float ndl = dot(n, KeyDir());
                float lit = smoothstep(-_BandSoftness, _BandSoftness, ndl - 0.05);
                float3 col = albedo * lerp(_ShadowTint.rgb, float3(1.0, 0.96, 0.9), lit);
                col += albedo * _AmbientSky.rgb * (n.y * 0.5 + 0.5);

                // Fresnel rim with drifting scanlines.
                float rim = pow(1.0 - saturate(dot(n, viewDir)), _RimPower);
                float scan = frac(i.worldPos.y * _ScanDensity - t * 0.6);
                float lines = lerp(0.55, 1.0, smoothstep(0.35, 0.5, scan) * smoothstep(0.95, 0.8, scan));
                float highlight = _Highlight;
                col += _Theme.rgb * rim * lines * (_RimStrength + highlight * 1.4);

                // Rising sweep band (on the whole figure at once, so every
                // part - body, arms, head - lights in step).
                float sweepY = frac(t / _SweepPeriod) * 3.2 - 0.5;
                float sweep = exp(-abs(i.height - sweepY) * 9.0);
                col += _Theme.rgb * sweep * 0.35;

                // Emissive trim, softly breathing.
                float glow = i.color.a * _Emission * (0.85 + 0.15 * sin(t * 2.1)) * (1.0 + highlight * 0.6);
                col = lerp(col, _Theme.rgb * glow + albedo * 0.2, saturate(i.color.a));
                col += _Theme.rgb * stoleHem * _Emission;

                // Visor face (head only): a dark glass band across the
                // front with two soft eye dots, all from object-space
                // position so the edges stay crisp at any mesh density.
                // A blink closes the eyes for ~0.1 s every ~4.3 s.
                if (_Visor > 0.5)
                {
                    float3 op = i.objPos;
                    float front = smoothstep(0.02, 0.06, op.z);
                    float band = smoothstep(-0.036, -0.028, op.y) * (1.0 - smoothstep(0.05, 0.058, op.y));
                    float span = 1.0 - smoothstep(0.084, 0.094, abs(op.x));
                    float visor = front * band * span;
                    float3 glass = float3(0.025, 0.03, 0.045) + _Theme.rgb * (0.3 + rim * 0.8);
                    col = lerp(col, glass, visor);

                    float blinkPhase = frac(t / 4.3);
                    float open = 1.0 - smoothstep(0.0, 0.012, blinkPhase) * (1.0 - smoothstep(0.03, 0.045, blinkPhase));
                    float2 e = float2(abs(op.x) - 0.042, (op.y - 0.011) / max(open, 0.08));
                    float eye = (1.0 - smoothstep(0.011, 0.018, length(e))) * visor * open;
                    col += lerp(_Theme.rgb, float3(1.0, 1.0, 1.0), 0.75) * eye * 1.6;
                }

                fixed4 outCol = fixed4(col, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, outCol);
                return outCol;
            }
            ENDCG
        }
    }
}
