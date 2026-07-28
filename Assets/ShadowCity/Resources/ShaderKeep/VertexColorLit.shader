// SHADOW CITY — vertex-colored lit shader (built-in pipeline).
// City building hulls bake per-face shading + palette into vertex colors;
// Standard shader ignores COLOR, so this shader displays them.
// Lives in Resources/ so it is always included in builds.
Shader "ShadowCity/VertexColorLit"
{
    Properties
    {
        _Color("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Lambert
        fixed4 _Color;
        struct Input
        {
            float4 color : COLOR;
        };
        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb * _Color.rgb;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Diffuse"
}
