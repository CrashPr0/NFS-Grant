// GPU-animated translucent water for the hub waterfall and basin. Driven
// entirely by the built-in _Time uniform, so it animates with zero CPU /
// script cost - the right tool for an attention study that must hold 72+
// Hz on Quest and run in WebGL. Pipeline-independent CG pass, matching
// NSFGrant/TextOccluded and NSFGrant/UnlitTransparentColor.
//
// Used for the namesake "waterfall" feature only; content panels stay
// static so motion does not confound the gaze/attention measures.
Shader "NSFGrant/AnimatedWater"
{
    Properties
    {
        _Color ("Color", Color) = (0.5, 0.75, 0.95, 0.5)
        _FoamColor ("Foam/Highlight Color", Color) = (1, 1, 1, 1)
        _ScrollSpeed ("Scroll Speed (xy)", Vector) = (0, -0.6, 0, 0)
        _WaveScale ("Wave Scale", Float) = 9
        _WaveSpeed ("Wave Speed", Float) = 2.2
        _FoamStrength ("Foam Strength", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _FoamColor;
            float4 _ScrollSpeed;
            float _WaveScale;
            float _WaveSpeed;
            float _FoamStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                // Flow the sample point along the scroll direction, then sum
                // two crossing sine waves for moving light/dark banding.
                float2 flow = i.uv + _ScrollSpeed.xy * t;
                float wave = sin(flow.y * _WaveScale + t * _WaveSpeed)
                           + sin(flow.x * _WaveScale * 0.7 - t * _WaveSpeed * 1.3);
                wave = wave * 0.25 + 0.5; // remap to 0..1

                // Bright foam where the crests stack up.
                float foam = smoothstep(0.78, 1.0, wave) * _FoamStrength;
                fixed4 col = lerp(_Color, _FoamColor, foam);
                col.a = _Color.a * (0.7 + 0.3 * wave);
                return col;
            }
            ENDCG
        }
    }
}
