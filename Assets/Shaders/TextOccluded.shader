// Depth-tested replacement for the built-in font shader. Unity's default
// 3D TextMesh material draws with ZTest Always, so labels render through
// panels, walls and the docent figure; this shader is identical except it
// respects the depth buffer (ZTest LEqual), letting scene geometry occlude
// text naturally. Assigned by DiscoveryHallBuilder.CreateTextMesh.
Shader "NSFGrant/TextOccluded"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Lighting Off
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Glyph coverage lives in the font atlas alpha channel;
                // tint comes from the TextMesh vertex color.
                fixed4 col = i.color;
                col.a *= tex2D(_MainTex, i.uv).a;
                return col;
            }
            ENDCG
        }
    }
}
