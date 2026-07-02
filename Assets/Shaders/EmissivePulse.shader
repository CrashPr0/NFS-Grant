// GPU-animated pulsing glow for the Guided-condition docent beacon. The
// beacon's purpose is to draw the participant toward the next room, and it
// only exists in Condition C, so the motion is a navigation aid rather than
// a confound. Driven by _Time, so no script runs per frame.
Shader "NSFGrant/EmissivePulse"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 1, 1)
        _PulseSpeed ("Pulse Speed", Float) = 2.5
        _MinIntensity ("Min Intensity", Float) = 0.6
        _MaxIntensity ("Max Intensity", Float) = 1.8
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

            fixed4 _Color;
            float _PulseSpeed;
            float _MinIntensity;
            float _MaxIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;
                float intensity = lerp(_MinIntensity, _MaxIntensity, pulse);
                return fixed4(_Color.rgb * intensity, 1.0);
            }
            ENDCG
        }
    }
}
