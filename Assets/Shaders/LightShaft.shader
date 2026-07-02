// Soft additive light shaft for the hub's oculus skylight. The vertical
// gradient is computed from object-space Y (the default Cylinder primitive
// spans -1..1 in mesh space, independent of the GameObject's scale), so a
// stretched cone-stand-in always reads bright near the light source and
// fades to nothing at the floor regardless of how it's scaled. _Time-driven
// shimmer, zero per-frame script cost. Pipeline-independent CG pass,
// matching NSFGrant/AnimatedWater and the other NSFGrant shaders.
Shader "NSFGrant/LightShaft"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.95, 0.82, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.0
        _BottomFalloff ("Bottom Falloff", Range(0.5, 6)) = 2.5
        _ShimmerSpeed ("Shimmer Speed", Float) = 0.6
        _ShimmerStrength ("Shimmer Strength", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;
            float _BottomFalloff;
            float _ShimmerSpeed;
            float _ShimmerStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float h : TEXCOORD0;
                float side : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                // Default Cylinder primitive spans y -1..1 in mesh space.
                o.h = saturate((v.vertex.y + 1.0) * 0.5);
                o.side = v.vertex.x;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float fall = pow(i.h, _BottomFalloff);
                float shimmer = 1.0 + sin(_Time.y * _ShimmerSpeed + i.side * 6.0) * _ShimmerStrength;
                float strength = fall * shimmer * _Intensity;
                return fixed4(_Color.rgb * strength, 1.0);
            }
            ENDCG
        }
    }
}
