// Peripheral comfort vignette for smooth locomotion: a soft black ring
// that closes in from the edges of view while the participant is
// stick-walking, the standard mitigation for vection-induced discomfort.
// Drawn on a small quad parented in front of the eye; alpha ramps from
// _Inner to _Outer in UV-radial distance and scales by _Strength, which
// VRLocomotion animates with movement. Stereo-instancing macros included
// (Single Pass Instanced-safe) like every other NSFGrant shader - this
// one sits centimeters from the eyes where per-eye errors are worst.
Shader "NSFGrant/ComfortVignette"
{
    Properties
    {
        _Color ("Vignette Color", Color) = (0, 0, 0, 1)
        _Strength ("Strength", Range(0, 1)) = 0
        _Inner ("Inner Radius (uv)", Range(0, 1)) = 0.16
        _Outer ("Outer Radius (uv)", Range(0, 1)) = 0.36
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Strength;
            float _Inner;
            float _Outer;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = distance(i.uv, float2(0.5, 0.5));
                float a = _Strength * smoothstep(_Inner, _Outer, d);
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}
