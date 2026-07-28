// ============================================================================
// SHADOW CITY — Core/ShaderLib.cs
// Central shader picker. Every runtime material MUST come through here.
//
// Why: all Shadow City materials are created in code, so nothing references
// "Standard" / "Particles/Standard Unlit" at build time → Unity strips them
// from player builds and Shader.Find() returns null → pink/invisible world.
// Our own shaders live in Resources/ShaderKeep (always included in builds).
//
// Rule: URP shaders only when a URP pipeline asset is actually ACTIVE;
// otherwise our custom built-in shaders (guaranteed present in any build).
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;

namespace ShadowCity
{
    public static class ShaderLib
    {
        static Shader lit, particle, transparent, vertexColor;

        /// <summary>True when a Scriptable Render Pipeline (URP) is active.</summary>
        public static bool URPActive => GraphicsSettings.currentRenderPipeline != null;

        /// <summary>Opaque lit shader (color + always-on emission property).</summary>
        public static Shader Lit
        {
            get
            {
                if (lit != null) return lit;
                if (URPActive) lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) lit = Shader.Find("ShadowCity/SimpleLit");
                if (lit == null) lit = Shader.Find("Standard");
                if (lit == null) lit = Shader.Find("Legacy Shaders/Diffuse");
                return lit;
            }
        }

        /// <summary>Vertex-colored lit shader (city building hulls, batched props).</summary>
        public static Shader VertexColorLit
        {
            get
            {
                if (vertexColor != null) return vertexColor;
                vertexColor = Shader.Find("ShadowCity/VertexColorLit");
                if (vertexColor == null) vertexColor = Lit;
                return vertexColor;
            }
        }

        /// <summary>Unlit particle shader (vertex-color × tint, alpha blend).</summary>
        public static Shader Particle
        {
            get
            {
                if (particle != null) return particle;
                particle = Shader.Find("ShadowCity/ParticleUnlit");
                if (particle == null && URPActive)
                    particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (particle == null) particle = Shader.Find("Particles/Standard Unlit");
                if (particle == null) particle = Shader.Find("Sprites/Default");
                return particle;
            }
        }

        /// <summary>Transparent glow shader (pulse ring, reveal spheres).</summary>
        public static Shader TransparentGlow
        {
            get
            {
                if (transparent != null) return transparent;
                transparent = Shader.Find("ShadowCity/TransparentGlow");
                if (transparent == null) transparent = Lit;
                return transparent;
            }
        }

        /// <summary>
        /// Make a runtime material transparent, working in EVERY pipeline/build:
        ///  • URP active  → URP Lit surface/blend setup
        ///  • built-in    → swap to our TransparentGlow shader (keyword variants
        ///    of Standard are stripped from builds, so flags alone won't work)
        /// Preserves color + emission.
        /// </summary>
        public static void MakeTransparent(Material m)
        {
            if (URPActive && m.HasProperty("_Surface"))          // URP Lit
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3000;
                return;
            }
            var col = m.color;
            var emis = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            m.shader = TransparentGlow;
            m.color = col;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emis);
            m.renderQueue = 3000;
        }
    }
}
