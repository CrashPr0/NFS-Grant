// Cheap, stereo-correct shader for the controller-tracked hands. Written
// as an explicit single-pass-instanced-safe pass (UNITY_VERTEX_* macros)
// because the hands sit ~30 cm from the eyes, where the per-eye stereo
// error of a non-instanced shader is glaringly obvious - it was the cause
// of "each controller renders differently to each eye." Relying on the
// primitive's pipeline-default material was the trap: under URP a
// CreatePrimitive can hand back a Built-in Standard material that is not
// single-pass-instanced-correct.
//
// Shading is a fixed-direction half-Lambert (no dependence on scene
// lights, so it looks identical under Built-in and URP and never renders
// flat/black), tinted by _Color which the script animates on trigger
// squeeze. Opaque, matching the solid look a hand should have.
Shader "NSFGrant/HandShaded"
{
    Properties
    {
        _Color ("Color", Color) = (0.8, 0.83, 0.9, 1)
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
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Fixed key direction (up-and-to-the-side); half-Lambert so
                // even back-facing areas keep a soft floor of light.
                float3 keyDir = normalize(float3(0.3, 0.9, 0.2));
                float ndl = dot(normalize(i.worldNormal), keyDir) * 0.5 + 0.5;
                float shade = 0.55 + 0.45 * ndl;
                return fixed4(_Color.rgb * shade, 1.0);
            }
            ENDCG
        }
    }
}
