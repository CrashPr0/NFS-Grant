// Procedural inlay floor for the hub: concentric rings plus three warm
// spokes pointing toward the room doorways (-120/0/120 degrees, matching
// DiscoveryHallBuilder's station angles). Unlike the other NSFGrant
// shaders (all unlit overlays), this is a real Standard surface shader so
// the floor keeps catching the hub's key light, ambient trilight, and the
// hub reflection probe like every other procedural primitive in the room.
// Pattern is computed from object-space position (vert -> Input.localPos),
// so it stays correct under any GameObject scale. No textures, no loops
// beyond an unrolled 3-spoke check - cheap on Quest/WebGL.
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
        _Smoothness ("Smoothness", Range(0, 1)) = 0.35
        _Metallic ("Metallic", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        fixed4 _BaseColor;
        fixed4 _RingColor;
        fixed4 _SpokeColor;
        float4 _SpokeAngles;
        float _SpokeWidth;
        float _RingSpacing;
        float _RingWidth;
        float _Radius;
        half _Smoothness;
        half _Metallic;

        struct Input
        {
            float3 localPos;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.localPos = v.vertex.xyz;
        }

        float AngleDelta(float a, float b)
        {
            float d = abs(a - b);
            return d > 180.0 ? 360.0 - d : d;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float r = length(IN.localPos.xz);
            float angleDeg = degrees(atan2(IN.localPos.z, IN.localPos.x));

            float ringWave = sin(r * 6.2831853 / max(_RingSpacing, 0.001));
            float ringMask = smoothstep(1.0 - _RingWidth, 1.0, ringWave * 0.5 + 0.5);
            fixed3 col = lerp(_BaseColor.rgb, _RingColor.rgb, ringMask * 0.55);

            float spokeMask = 0.0;
            spokeMask = max(spokeMask, 1.0 - smoothstep(0.0, _SpokeWidth, AngleDelta(angleDeg, _SpokeAngles.x)));
            spokeMask = max(spokeMask, 1.0 - smoothstep(0.0, _SpokeWidth, AngleDelta(angleDeg, _SpokeAngles.y)));
            spokeMask = max(spokeMask, 1.0 - smoothstep(0.0, _SpokeWidth, AngleDelta(angleDeg, _SpokeAngles.z)));
            float radialFade = saturate(r / max(_Radius, 0.001));
            col = lerp(col, _SpokeColor.rgb, spokeMask * radialFade * 0.45);

            o.Albedo = col;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;
            o.Alpha = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
