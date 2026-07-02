// Procedural inlay floor for the hub: concentric rings plus three warm
// spokes pointing toward the room doorways (-120/0/120 degrees, matching
// DiscoveryHallBuilder's station angles). Plain CGPROGRAM Pass, matching
// every other NSFGrant shader - an earlier version used a #pragma surface
// Standard surface shader for real PBR lighting response, but surface
// shaders depend on the Built-in Render Pipeline's lighting library and
// fail to compile under URP (Unity swaps in the magenta error material).
// This project targets both pipelines, so it stays a self-contained pass
// like NSFGrant/AnimatedWater etc. Since it can't react to the scene's
// real light, a static center-glow term fakes some depth instead: the
// floor sits directly under the hub's glass skylight (NSFGrant/
// GlassCeiling), so brightening toward the center and dimming toward the
// walls is a believable pseudo-shading cue, not just an arbitrary vignette.
// Pattern is computed from object-space position, so it stays correct
// under any GameObject scale. No textures, no loops - cheap on Quest/WebGL.
Shader "NSFGrant/RadialFloor"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.30, 0.30, 0.33, 1)
        _RingColor ("Ring Accent Color", Color) = (0.55, 0.57, 0.62, 1)
        _SpokeColor ("Spoke Accent Color", Color) = (1, 0.85, 0.55, 1)
        _SpokeAngles ("Spoke Angles (deg, xyz used)", Vector) = (-120, 0, 120, 0)
        _SpokeWidth ("Spoke Width (deg)", Range(1, 60)) = 14
        _RingSpacing ("Ring Spacing (object space)", Float) = 0.07
        _RingWidth ("Ring Width", Range(0, 1)) = 0.18
        _Radius ("Floor Radius (object space)", Float) = 0.5
        _CenterBrightness ("Center Brightness (under skylight)", Range(0.5, 2)) = 1.18
        _RimBrightness ("Rim Brightness (near walls)", Range(0.3, 1.5)) = 0.8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _BaseColor;
            fixed4 _RingColor;
            fixed4 _SpokeColor;
            float4 _SpokeAngles;
            float _SpokeWidth;
            float _RingSpacing;
            float _RingWidth;
            float _Radius;
            float _CenterBrightness;
            float _RimBrightness;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 localPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }

            float AngleDelta(float a, float b)
            {
                float d = abs(a - b);
                return d > 180.0 ? 360.0 - d : d;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.localPos.xz);
                float angleDeg = degrees(atan2(i.localPos.z, i.localPos.x));

                float ringWave = sin(r * 6.2831853 / max(_RingSpacing, 0.001));
                float ringMask = smoothstep(1.0 - _RingWidth, 1.0, ringWave * 0.5 + 0.5);
                fixed3 col = lerp(_BaseColor.rgb, _RingColor.rgb, ringMask * 0.55);

                float spokeMask = 0.0;
                spokeMask = max(spokeMask, 1.0 - smoothstep(0.0, _SpokeWidth, AngleDelta(angleDeg, _SpokeAngles.x)));
                spokeMask = max(spokeMask, 1.0 - smoothstep(0.0, _SpokeWidth, AngleDelta(angleDeg, _SpokeAngles.y)));
                spokeMask = max(spokeMask, 1.0 - smoothstep(0.0, _SpokeWidth, AngleDelta(angleDeg, _SpokeAngles.z)));
                float radialFade = saturate(r / max(_Radius, 0.001));
                col = lerp(col, _SpokeColor.rgb, spokeMask * radialFade * 0.45);

                // Pseudo-shading: brighter under the skylight at center,
                // dimmer toward the walls - a fixed lighting cue this pass
                // can't otherwise get from the scene's real light.
                float shade = lerp(_CenterBrightness, _RimBrightness, radialFade);
                col *= shade;

                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
