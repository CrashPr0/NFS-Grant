// Transparent glass ceiling for the hub's skylight: a tinted, Fresnel-
// rimmed pane that samples the nearest reflection probe (the baked
// NSFGrant/GradientSky skybox, captured once by HubReflectionProbe) so the
// sky reads through the glass with a believable reflective glint at
// grazing angles. Single forward CG pass, no per-frame script cost beyond
// the automatic per-object probe binding Unity already does. Pipeline-
// independent, matching the other NSFGrant shaders.
Shader "NSFGrant/GlassCeiling"
{
    Properties
    {
        _Color ("Glass Tint", Color) = (0.75, 0.85, 0.95, 0.22)
        _FresnelColor ("Fresnel Glint Color", Color) = (1, 0.97, 0.9, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
        _ReflectionStrength ("Sky Reflection Strength", Range(0, 1)) = 0.6
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
            fixed4 _FresnelColor;
            float _FresnelPower;
            float _ReflectionStrength;

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
                float3 worldViewDir : TEXCOORD1;
                float3 worldRefl : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldViewDir = normalize(_WorldSpaceCameraPos - worldPos);
                o.worldNormal = worldNormal;
                o.worldViewDir = worldViewDir;
                o.worldRefl = reflect(-worldViewDir, worldNormal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.worldViewDir);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

                fixed3 skyReflection = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, i.worldRefl).rgb;
                fixed3 col = lerp(_Color.rgb, skyReflection, _ReflectionStrength * (0.4 + 0.6 * fresnel));
                col = lerp(col, _FresnelColor.rgb, fresnel * 0.5);

                float alpha = saturate(_Color.a + fresnel * 0.5);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
