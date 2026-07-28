// ============================================================================
// SHADOW CITY — Systems/Cinematic.cs
// Opening cinematic — port of the web build's New-Game camera sequence:
//   Shot 1: high aerial push-in over Downtown (title card)
//   Shot 2: low sweep along the Neon Strip toward the spawn (story line)
//   Shot 3: crane-down behind the player into gameplay (pulse hint)
// Skippable with any key / touch. Letterbox + captions via UIBuilder.
// ============================================================================
using UnityEngine;

namespace ShadowCity
{
    public static class Cinematic
    {
        public static bool Active { get; private set; }

        static int shot;
        static float t, dur;
        static Vector3 p0, p1, l0, l1;
        static float skipGuard;   // ignore input carried over from the menu click

        public static void Begin()
        {
            Active = true;
            shot = 0;
            skipGuard = 0.35f;
            Bootstrap.I.State = "CINEMATIC";
            UIBuilder.I.ShowCinematic(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
            SetupShot();
        }

        static Vector3 Anchor(string district, float height)
        {
            var plan = CityGenerator.Plan;
            Vector2 a = plan.DistrictAnchors.TryGetValue(district, out var v)
                ? v : new Vector2(GameConfig.WorldSize / 2, GameConfig.WorldSize / 2);
            return new Vector3(a.x, height, a.y);
        }

        static void SetupShot()
        {
            t = 0;
            Vector3 spawn = CityGenerator.Plan.PlayerSpawn;
            switch (shot)
            {
                case 0:   // aerial: the whole grid breathes
                    p0 = Anchor("DOWNTOWN", 130) + new Vector3(-170, 0, -170);
                    p1 = Anchor("DOWNTOWN", 92) + new Vector3(-55, 0, -55);
                    l0 = l1 = Anchor("DOWNTOWN", 22);
                    dur = 4.6f;
                    UIBuilder.I.CineText(L10N.T("game.title"));
                    break;
                case 1:   // neon strip sweep toward the spawn point
                    p0 = Anchor("NEON_STRIP", 40) + new Vector3(130, 0, -50);
                    p1 = Vector3.Lerp(Anchor("NEON_STRIP", 30), spawn + Vector3.up * 26f, 0.6f);
                    l0 = Anchor("NEON_STRIP", 6);
                    l1 = spawn + Vector3.up * 3f;
                    dur = 4.6f;
                    UIBuilder.I.CineText(L10N.T("story.s1.line"));
                    AudioManager.I?.PlayDialog("dlg_s1");
                    break;
                default:  // crane down into third-person position
                    p0 = spawn + new Vector3(0, 26, -22);
                    p1 = spawn + new Vector3(0, 3.1f, -6.2f);
                    l0 = l1 = spawn + Vector3.up * 1.6f;
                    dur = 3.4f;
                    UIBuilder.I.CineText(L10N.T("hud.pulseHint"));
                    break;
            }
        }

        public static void Tick(float dt)
        {
            if (!Active) return;

            skipGuard -= dt;
            if (skipGuard <= 0 &&
                (Input.anyKeyDown || Input.GetMouseButtonDown(0) || GameInput.TouchInteract))
            { End(); return; }

            t += dt / dur;
            float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            var cam = Camera.main.transform;
            cam.position = Vector3.Lerp(p0, p1, e);
            Vector3 look = Vector3.Lerp(l0, l1, e);
            Vector3 fwd = look - cam.position;
            if (fwd.sqrMagnitude > 0.001f)
                cam.rotation = Quaternion.LookRotation(fwd);

            if (t >= 1f)
            {
                shot++;
                if (shot > 2) End();
                else SetupShot();
            }
        }

        static void End()
        {
            Active = false;
            UIBuilder.I.ShowCinematic(false);
            Bootstrap.I.State = "PLAYING";
            var p = PlayerController.I;
            if (p != null && ThirdPersonCamera.I != null)
                ThirdPersonCamera.I.Yaw = p.Heading;
            if (!GameInput.IsMobile)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }
    }
}
