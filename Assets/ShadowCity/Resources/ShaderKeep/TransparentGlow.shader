// SHADOW CITY — transparent unlit-ish shader for pulse ring / reveal glows.
// Alpha comes from _Color.a; emission adds neon glow. No keywords, so it can
// never be variant-stripped from player builds.
Shader "ShadowCity/TransparentGlow"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,0.5)
        _EmissionColor("Emission", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            fixed4 _EmissionColor;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_Color.rgb + _EmissionColor.rgb * 0.6, _Color.a);
            }
            ENDCG
        }
    }
}
