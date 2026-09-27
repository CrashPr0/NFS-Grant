// Fake contact shadow: a soft dark ellipse on the floor under an object,
// multiplied into whatever is already drawn there. The hall's only
// shadow-casting light points straight down, so real shadows land almost
// exactly under each object - this patch reproduces that look with no
// shadow map, no extra pass and one quad. Shape is computed from the UV
// (no texture); the quad's scale sets the ellipse. Used under the
// procedural planters everywhere (their stylized shaders cast no real
// shadow) and, in the headset only (layer XROnlyVisual), under columns and
// docents, where real-time shadows are switched off to save GPU time.
// Stereo-instancing macros and GPU instancing like the other NSFGrant
// shaders; fades out with the scene fog.
Shader "NSFGrant/ContactShadow"
{
    Properties
    {
        _Color ("Shadow Color (center)", Color) = (0.42, 0.44, 0.5, 1)
        _Strength ("Strength", Range(0, 1)) = 0.7
        _Softness ("Edge Softness", Range(0.05, 1)) = 0.9
    }
    SubShader
    {
        // After opaque geometry (the floor), before other transparents.
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend DstColor Zero
        ZWrite Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Strength, _Softness;

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
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Darkest under the object, fading smoothly to nothing at the
                // quad's edge (quad radius ~1.5-2x the object's footprint).
                float d = length(i.uv * 2.0 - 1.0);
                float a = saturate((1.0 - d) / _Softness);
                a = a * a * (3.0 - 2.0 * a);
                fixed4 col = fixed4(lerp(float3(1, 1, 1), _Color.rgb, a * _Strength), 1.0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(1, 1, 1, 1));
                return col;
            }
            ENDCG
        }
    }
}
