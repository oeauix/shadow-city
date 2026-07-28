// SHADOW CITY — general lit shader (built-in pipeline, always included via Resources).
// Replaces "Standard" for runtime-created materials in player builds where
// Shader.Find("Standard") returns null (nothing references it at build time).
// Supports: _Color, always-on _EmissionColor (no keyword needed — variants
// can't be stripped), dummy _Metallic/_Smoothness/_Glossiness for API parity.
Shader "ShadowCity/SimpleLit"
{
    Properties
    {
        _Color("Color", Color) = (1,1,1,1)
        _EmissionColor("Emission", Color) = (0,0,0,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.35
        _Glossiness("Glossiness", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 150
        CGPROGRAM
        #pragma surface surf Lambert
        fixed4 _Color;
        fixed4 _EmissionColor;
        struct Input { float4 color : COLOR; };
        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Emission = _EmissionColor.rgb;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Diffuse"
}
