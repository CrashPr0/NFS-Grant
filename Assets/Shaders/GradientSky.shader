// Procedural gradient skybox for the Discovery Hall: a calm three-stop
// vertical gradient (sky / horizon / ground) with an almost-imperceptible
// horizon drift. Replaces Unity's default blue procedural sky. _Time-driven
// so it costs nothing per frame, and ambient/symmetric so it cannot bias
// the attention measures. Plain CG pass like the other NSFGrant shaders;
// revisit at the URP migration.
Shader "NSFGrant/GradientSky"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.20, 0.34, 0.54, 1)
        _HorizonColor ("Horizon Color", Color) = (0.72, 0.79, 0.87, 1)
        _BottomColor ("Bottom Color", Color) = (0.16, 0.17, 0.20, 1)
        _Exponent ("Gradient Exponent", Float) = 1.3
        _DriftSpeed ("Horizon Drift Speed", Float) = 0.03
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            fixed4 _BottomColor;
            float _Exponent;
            float _DriftSpeed;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // The skybox mesh is a unit box centered on the camera, so
                // the object-space vertex doubles as a view direction.
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float h = normalize(i.dir).y;            // -1 (down) .. 1 (up)
                float drift = sin(_Time.y * _DriftSpeed * 6.2831853) * 0.015;
                if (h > 0.0)
                {
                    float t = pow(saturate(h - drift), _Exponent);
                    return lerp(_HorizonColor, _TopColor, t);
                }
                float t = pow(saturate(-h), _Exponent);
                return lerp(_HorizonColor, _BottomColor, t);
            }
            ENDCG
        }
    }
}
