// ============================================================================
// SHADOW CITY — Entities/CharacterRig.cs
// Procedural articulated humanoid (primitives) with analytic gait animation.
// Direct port of the web rig. Swappable later with a Tripo rigged model:
// TripoImporter replaces the visual child, gameplay code untouched.
// ============================================================================
using UnityEngine;

namespace ShadowCity
{
    public class CharacterRig : MonoBehaviour
    {
        public Transform Pelvis, Torso, Head;
        public Transform ArmL, ArmR, LegL, LegR;
        float gaitPhase, speedBlend, animTime;
        public bool Sitting, Dead;
        float deathT;

        static readonly Color[][] Palettes =
        {
            new[]{ new Color(0.85f,0.66f,0.47f), new Color(0.22f,0.27f,0.36f), new Color(0.14f,0.15f,0.17f) },
            new[]{ new Color(0.54f,0.35f,0.23f), new Color(0.36f,0.22f,0.26f), new Color(0.17f,0.18f,0.22f) },
            new[]{ new Color(0.91f,0.75f,0.60f), new Color(0.18f,0.36f,0.28f), new Color(0.23f,0.19f,0.15f) },
            new[]{ new Color(0.77f,0.55f,0.39f), new Color(0.42f,0.38f,0.31f), new Color(0.12f,0.16f,0.21f) },
        };

        bool hasAIModel;
        Transform aiModel;
        CharacterAnimator animator;   // non-null once Mixamo controller exists

        public static CharacterRig Create(Transform parent, int palette, bool police = false)
        {
            var root = new GameObject("CharacterRig");
            root.transform.SetParent(parent, false);
            var rig = root.AddComponent<CharacterRig>();

            // AI model swap (Resources/Models/): character | cop | ped_man | ped_woman
            string id = police ? "cop"
                : parent != null && parent.name == "Player" ? "character"
                : (palette % 2 == 0 ? "ped_man" : "ped_woman");
            var ai = ModelLibrary.TrySpawn(id, root.transform);
            if (ai != null)
            {
                rig.animator = CharacterAnimator.TryAttach(ai);
                if (rig.animator == null)
                {
                    // No Mixamo animation controller yet → the GLB is a frozen
                    // T-pose statue. The procedural rig (analytic gait) looks
                    // far better in motion, so use it until rigging is done.
                    Object.Destroy(ai);
                    rig.BuildBody(Palettes[Mathf.Abs(palette) % Palettes.Length], police);
                    return rig;
                }
                rig.hasAIModel = true;
                rig.aiModel = ai.transform;
                // build the procedural rig too but hide it: limb transforms
                // still drive gameplay poses (sitting offsets etc.)
                rig.BuildBody(Palettes[Mathf.Abs(palette) % Palettes.Length], police);
                SetVisible(rig.transform, false, ai.transform);
                return rig;
            }
            rig.BuildBody(Palettes[Mathf.Abs(palette) % Palettes.Length], police);
            return rig;
        }

        static void SetVisible(Transform root, bool visible, Transform except)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (except != null && r.transform.IsChildOf(except)) continue;
                r.enabled = visible;
            }
        }

        void BuildBody(Color[] pal, bool police)
        {
            Color skin = pal[0], top = police ? new Color(0.11f, 0.16f, 0.25f) : pal[1];
            Color bottom = police ? new Color(0.09f, 0.13f, 0.18f) : pal[2];

            Pelvis = Part(transform, "Pelvis", new Vector3(0, 0.96f, 0), new Vector3(0.34f, 0.18f, 0.2f), bottom);
            Torso = Part(Pelvis, "Torso", new Vector3(0, 0.35f, 0), new Vector3(0.4f, 0.5f, 0.24f), top);
            Head = Part(Torso, "Head", new Vector3(0, 0.45f, 0), new Vector3(0.22f, 0.24f, 0.22f), skin);
            ArmL = Limb(Torso, "ArmL", new Vector3(-0.27f, 0.2f, 0), new Vector3(0.1f, 0.55f, 0.11f), top);
            ArmR = Limb(Torso, "ArmR", new Vector3(0.27f, 0.2f, 0), new Vector3(0.1f, 0.55f, 0.11f), top);
            LegL = Limb(Pelvis, "LegL", new Vector3(-0.11f, -0.09f, 0), new Vector3(0.14f, 0.8f, 0.15f), bottom);
            LegR = Limb(Pelvis, "LegR", new Vector3(0.11f, -0.09f, 0), new Vector3(0.14f, 0.8f, 0.15f), bottom);
        }

        static Material sharedShader;
        static Transform Part(Transform parent, string name, Vector3 pos, Vector3 size, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var sh = ShaderLib.Lit;
            var m = new Material(sh) { color = col };
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go.transform;
        }

        /// <summary>Limb = pivot at shoulder/hip, mesh hangs below.</summary>
        static Transform Limb(Transform parent, string name, Vector3 pivot, Vector3 size, Color col)
        {
            var pivotGo = new GameObject(name);
            pivotGo.transform.SetParent(parent, false);
            pivotGo.transform.localPosition = pivot;
            Part(pivotGo.transform, name + "_mesh", new Vector3(0, -size.y / 2, 0), size, col);
            return pivotGo.transform;
        }

        public void Tick(float dt, float speed, float maxSpeed)
        {
            animTime += dt;
            if (animator != null)
            {
                animator.SetSpeed(maxSpeed > 0 ? Mathf.Clamp01(speed / maxSpeed) : 0);
                animator.SetDriving(Sitting);
                animator.SetDead(Dead);
                return;   // Mixamo animations own the visuals now
            }
            if (hasAIModel && aiModel != null && !Dead && !Sitting)
            {
                // static-mesh gait fake: bob + slight lean while moving
                float w = Mathf.Clamp01(speed / Mathf.Max(maxSpeed, 0.1f));
                gaitPhase += dt * Mathf.Max(speed, 0.001f) * 3.3f;
                aiModel.localPosition = new Vector3(0,
                    Mathf.Abs(Mathf.Sin(gaitPhase)) * 0.06f * w, 0);
                aiModel.localRotation = Quaternion.Euler(w * 7f,
                    Mathf.Sin(gaitPhase) * 4f * w, 0);
            }
            if (hasAIModel && aiModel != null && Sitting)
                aiModel.localPosition = new Vector3(0, -0.35f, 0);
            if (Dead)
            {
                deathT = Mathf.Min(1, deathT + dt * 2.2f);
                transform.localRotation = Quaternion.Euler(0, 0, deathT * 86f);
                Pelvis.localPosition = new Vector3(0, Mathf.Lerp(0.96f, 0.24f, deathT), 0);
                return;
            }
            if (Sitting)
            {
                Pelvis.localPosition = new Vector3(0, 0.62f, 0);
                LegL.localRotation = LegR.localRotation = Quaternion.Euler(-78f, 0, 0);
                ArmL.localRotation = ArmR.localRotation = Quaternion.Euler(-48f, 0, 0);
                return;
            }

            float target = maxSpeed > 0 ? Mathf.Clamp(speed / maxSpeed * 2f, 0, 2) : 0;
            speedBlend = Mathf.Lerp(speedBlend, target, 1 - Mathf.Exp(-10 * dt));
            float walk = Mathf.Clamp01(speedBlend);
            float run = Mathf.Clamp01(speedBlend - 1);

            gaitPhase += dt * Mathf.Max(speed, 0.001f) * Mathf.PI / Mathf.Lerp(1.9f, 2.6f, run) * 2f;
            float swing = Mathf.Lerp(0, Mathf.Lerp(32f, 55f, run), walk);

            LegL.localRotation = Quaternion.Euler(Mathf.Sin(gaitPhase) * swing, 0, 0);
            LegR.localRotation = Quaternion.Euler(Mathf.Sin(gaitPhase + Mathf.PI) * swing, 0, 0);
            ArmL.localRotation = Quaternion.Euler(Mathf.Sin(gaitPhase + Mathf.PI) * swing * 0.75f, 0, 4);
            ArmR.localRotation = Quaternion.Euler(Mathf.Sin(gaitPhase) * swing * 0.75f, 0, -4);
            Pelvis.localPosition = new Vector3(0,
                0.96f + Mathf.Abs(Mathf.Sin(gaitPhase)) * 0.05f * walk +
                Mathf.Sin(animTime * 1.7f) * 0.012f, 0);
            Torso.localRotation = Quaternion.Euler(run * 8f, Mathf.Sin(gaitPhase) * 3.4f * walk, 0);
        }

        public void SetSitting(bool s)
        {
            Sitting = s;
            if (!s)
            {
                LegL.localRotation = LegR.localRotation = Quaternion.identity;
                ArmL.localRotation = ArmR.localRotation = Quaternion.identity;
                Torso.localRotation = Quaternion.identity;
            }
        }
        public void Die() { Dead = true; deathT = 0; }
        public void Revive()
        {
            Dead = false; deathT = 0;
            transform.localRotation = Quaternion.identity;
            Pelvis.localPosition = new Vector3(0, 0.96f, 0);
        }
    }
}
