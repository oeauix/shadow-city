// ============================================================================
// SHADOW CITY — Systems/ParticleFX.cs
// All particle effects built 100% in code (no prefabs, no textures):
//   explosion (fireball + smoke + sparks), vehicle exhaust, damage smoke,
//   muzzle flash, pulse motes, footstep dust, pickup sparkle.
// Uses Unity's built-in ParticleSystem with the default particle material.
// ============================================================================
using UnityEngine;

namespace ShadowCity
{
    public static class ParticleFX
    {
        static Material particleMat;
        static Transform root;

        static Material Mat()
        {
            if (particleMat == null)
            {
                var sh = ShaderLib.Particle;
                particleMat = new Material(sh) { color = Color.white };
            }
            return particleMat;
        }

        static Transform Root()
        {
            if (root == null) root = new GameObject("ParticleFX").transform;
            return root;
        }

        /// <summary>Base builder: configured ParticleSystem ready to Play().</summary>
        static ParticleSystem Make(string name, Vector3 pos, Color a, Color b,
            float size, float speed, float life, int burst, bool gravity,
            float radius = 0.3f, bool loop = false, float rate = 0)
        {
            var go = new GameObject("fx_" + name);
            go.transform.SetParent(Root(), false);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(a, b);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.gravityModifier = gravity ? 1f : 0f;
            main.loop = loop;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            if (loop) { em.rateOverTime = rate; }
            else
            {
                em.rateOverTime = 0;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            }

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = Mat();

            if (!loop) Object.Destroy(go, life * 1.6f + 0.5f);
            return ps;
        }

        // ------------------------------ ONE-SHOTS -------------------------------

        public static void Explosion(Vector3 pos)
        {
            Make("fireball", pos + Vector3.up * 0.6f,
                new Color(1f, 0.85f, 0.4f), new Color(1f, 0.35f, 0.05f),
                1.6f, 6f, 0.7f, 26, false, 0.5f).Play();
            Make("sparks", pos + Vector3.up * 0.8f,
                new Color(1f, 0.9f, 0.5f), new Color(1f, 0.5f, 0.1f),
                0.22f, 14f, 0.9f, 34, true, 0.3f).Play();
            Make("smoke", pos + Vector3.up * 1f,
                new Color(0.18f, 0.17f, 0.16f, 0.8f), new Color(0.35f, 0.34f, 0.33f, 0.6f),
                2.6f, 2.2f, 2.4f, 18, false, 0.8f).Play();
        }

        public static void MuzzleFlash(Vector3 pos, Vector3 dir)
        {
            var ps = Make("muzzle", pos,
                new Color(1f, 0.92f, 0.6f), new Color(1f, 0.6f, 0.2f),
                0.3f, 5f, 0.09f, 6, false, 0.05f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = dir.x * 4f; vel.y = dir.y * 4f; vel.z = dir.z * 4f;
            ps.Play();
        }

        public static void PulseMotes(Vector3 pos, float radius)
        {
            Make("motes", pos + Vector3.up * 1.2f,
                new Color(0.25f, 0.9f, 1f), new Color(0.55f, 0.45f, 1f),
                0.35f, 3.4f, 1.3f, 30, false, Mathf.Min(radius * 0.4f, 8f)).Play();
        }

        public static void FootDust(Vector3 pos)
        {
            Make("dust", pos + Vector3.up * 0.08f,
                new Color(0.5f, 0.48f, 0.44f, 0.5f), new Color(0.6f, 0.58f, 0.54f, 0.35f),
                0.3f, 0.8f, 0.5f, 3, false, 0.1f).Play();
        }

        public static void Sparkle(Vector3 pos)
        {
            Make("sparkle", pos + Vector3.up * 1.2f,
                new Color(1f, 0.9f, 0.4f), new Color(0.4f, 1f, 0.7f),
                0.25f, 3.5f, 0.9f, 20, true, 0.3f).Play();
        }

        public static void CrashDebris(Vector3 pos)
        {
            Make("debris", pos + Vector3.up * 0.6f,
                new Color(0.7f, 0.7f, 0.72f), new Color(0.4f, 0.4f, 0.42f),
                0.2f, 6f, 0.8f, 16, true, 0.4f).Play();
        }

        // ------------------------------ ATTACHED --------------------------------

        /// <summary>Looping exhaust for a driven vehicle. Returns the PS to stop later.</summary>
        public static ParticleSystem Exhaust(Transform vehicle, float length)
        {
            var ps = Make("exhaust", Vector3.zero,
                new Color(0.55f, 0.55f, 0.58f, 0.28f), new Color(0.7f, 0.7f, 0.72f, 0.18f),
                0.35f, 0.8f, 1.1f, 0, false, 0.08f, loop: true, rate: 14f);
            ps.transform.SetParent(vehicle, false);
            ps.transform.localPosition = new Vector3(0, 0.3f, -length / 2f - 0.15f);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ps.Play();
            return ps;
        }

        /// <summary>Looping damage smoke on a low-HP vehicle.</summary>
        public static ParticleSystem DamageSmoke(Transform vehicle, float height)
        {
            var ps = Make("dmgsmoke", Vector3.zero,
                new Color(0.15f, 0.14f, 0.13f, 0.75f), new Color(0.3f, 0.28f, 0.26f, 0.5f),
                0.9f, 1.6f, 1.6f, 0, false, 0.2f, loop: true, rate: 20f);
            ps.transform.SetParent(vehicle, false);
            ps.transform.localPosition = new Vector3(0, height, 0.9f);
            ps.Play();
            return ps;
        }
    }
}
