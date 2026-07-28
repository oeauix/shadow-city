// ============================================================================
// SHADOW CITY — Entities/PlayerController.cs + ThirdPersonCamera.cs
// GTA-style third-person: CharacterController on foot, seat-handoff to
// vehicles, stats (health/stamina/focus), interaction scanning, shooting.
// ============================================================================
using UnityEngine;

namespace ShadowCity
{
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController I { get; private set; }

        public CharacterController CC;
        public CharacterRig Rig;
        public Vehicle CurrentVehicle;
        public string District = "";

        public float Health = GameConfig.MaxHealth;
        public float Stamina = GameConfig.MaxStamina;
        public float Focus = GameConfig.MaxFocus;
        public bool Dead;
        public bool HasPistol; public int Ammo;

        public float Heading;
        float velY; float staminaDelay; float stepTimer;
        public object InteractTarget; public string InteractLabel = "";
        float fireCooldown;

        public static PlayerController Spawn(Vector3 pos)
        {
            var go = new GameObject("Player");
            go.transform.position = pos;
            var pc = go.AddComponent<PlayerController>();
            pc.CC = go.AddComponent<CharacterController>();
            pc.CC.radius = 0.42f; pc.CC.height = 1.78f; pc.CC.center = new Vector3(0, 0.89f, 0);
            pc.Rig = CharacterRig.Create(go.transform, 0);
            return pc;
        }

        void Awake() { I = this; }

        public void Tick(float dt)
        {
            if (Dead) { Rig.Tick(dt, 0, 1); return; }

            if (CurrentVehicle != null) TickInVehicle(dt);
            else TickOnFoot(dt);

            // Focus & stamina regen
            Focus = Mathf.Min(GameConfig.MaxFocus, Focus + GameConfig.FocusRegen * RPG.FocusMult() * dt);
            staminaDelay -= dt;
            if (staminaDelay <= 0)
                Stamina = Mathf.Min(GameConfig.MaxStamina, Stamina + GameConfig.StaminaRegen * dt);

            // District tracking
            string d = CityGenerator.DistrictAt(transform.position.x, transform.position.z);
            if (d != District)
            {
                bool first = District == "";
                District = d;
                if (!first) GameEvents.Emit(GameEvents.DistrictEntered, d);
            }

            ScanInteract();
            if (GameInput.InteractPressed) DoInteract();
            if (GameInput.PulsePressed) Pulse.Fire();
            if (HasPistol && GameInput.FirePressed && CurrentVehicle == null) TryShoot();
            if (fireCooldown > 0) fireCooldown -= dt;
        }

        void TickOnFoot(float dt)
        {
            var move = GameInput.Move;
            bool wantSprint = GameInput.Sprint && move.y > 0.1f && Stamina > 1;
            float speedCap = wantSprint ? GameConfig.SprintSpeed
                          : move.sqrMagnitude > 0.01f ? GameConfig.RunSpeed : 0f;

            // camera-relative direction
            float camYaw = ThirdPersonCamera.I != null ? ThirdPersonCamera.I.Yaw : 0;
            Vector3 dir = Quaternion.Euler(0, camYaw, 0) * new Vector3(move.x, 0, move.y);
            if (dir.sqrMagnitude > 0.01f)
            {
                dir.Normalize();
                Heading = Mathf.LerpAngle(Heading, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg,
                                          1 - Mathf.Exp(-12 * dt));
            }
            transform.rotation = Quaternion.Euler(0, Heading, 0);

            if (wantSprint)
            { Stamina = Mathf.Max(0, Stamina - GameConfig.StaminaSprintDrain * dt); staminaDelay = 1.1f; }

            if (CC.isGrounded)
            {
                velY = -1f;
                if (GameInput.JumpPressed) velY = GameConfig.JumpVelocity;
            }
            velY -= GameConfig.Gravity * dt;

            Vector3 motion = dir * speedCap + Vector3.up * velY;
            CC.Move(motion * dt);

            float planarSpeed = new Vector2(CC.velocity.x, CC.velocity.z).magnitude;
            Rig.Tick(dt, planarSpeed, GameConfig.RunSpeed);

            // footsteps: dust + sound at gait cadence
            if (CC.isGrounded && planarSpeed > 0.5f)
            {
                stepTimer -= dt * planarSpeed;
                if (stepTimer <= 0)
                {
                    stepTimer = 2.1f;
                    ParticleFX.FootDust(transform.position);
                    AudioManager.I?.Play("sfx_footstep", 0.35f);
                }
            }
        }

        void TickInVehicle(float dt)
        {
            var v = CurrentVehicle;
            var move = GameInput.Move;
            v.Throttle = move.y;
            v.Steer = move.x;
            v.Handbrake = GameInput.Brake;
            transform.position = v.transform.position;
            Heading = v.transform.eulerAngles.y;
        }

        // ------------------------------ INTERACT ------------------------------
        void ScanInteract()
        {
            InteractTarget = null; InteractLabel = "";
            if (CurrentVehicle != null)
            { InteractLabel = L10N.T("hud.exitVehicle"); return; }

            var pos = transform.position;
            // nearest vehicle
            Vehicle best = null; float bestD = GameConfig.InteractRadius + 2f;
            foreach (var v in TrafficSystem.All)
            {
                if (v == null || v.Destroyed) continue;
                float d = Vector3.Distance(pos, v.transform.position);
                if (d < bestD) { bestD = d; best = v; }
            }
            if (best != null)
            { InteractTarget = best; InteractLabel = L10N.T("hud.enterVehicle"); return; }

            var m = Missions.OfferNear(pos, GameConfig.InteractRadius + 2f);
            if (m != null)
            { InteractTarget = m; InteractLabel = L10N.T("hud.mission", m.Title()); return; }

            if (Economy.ShopNear(pos, GameConfig.InteractRadius + 1.5f))
            { InteractTarget = "shop"; InteractLabel = L10N.T("hud.shop"); }
        }

        void DoInteract()
        {
            if (CurrentVehicle != null) { ExitVehicle(); return; }
            switch (InteractTarget)
            {
                case Vehicle v: EnterVehicle(v); break;
                case Missions.Offer o: Missions.Accept(o); break;
                case "shop": UIBuilder.I.OpenShop(); break;
            }
        }

        public void EnterVehicle(Vehicle v)
        {
            CurrentVehicle = v;
            v.Driver = this;
            Rig.gameObject.SetActive(false);
            CC.enabled = false;
            if (v.Owner != "player")
            {
                v.Owner = "player";
                GameEvents.Emit(GameEvents.VehicleStolen, v);
                GameEvents.Emit(GameEvents.CrimeCommitted, "CAR_THEFT");
                GameEvents.Emit(GameEvents.Notify, L10N.T("notif.carStolen"));
            }
            GameEvents.Emit(GameEvents.EnterVehicle, v);
            AudioManager.I?.EngineStart();
        }

        public void ExitVehicle()
        {
            var v = CurrentVehicle;
            if (v == null) return;
            v.Driver = null; v.Throttle = 0; v.Steer = 0;
            CurrentVehicle = null;
            Vector3 side = -v.transform.right * (v.Def.W / 2 + 0.9f);
            transform.position = v.transform.position + side + Vector3.up * 0.5f;
            Rig.gameObject.SetActive(true);
            Rig.SetSitting(false);
            CC.enabled = true;
            GameEvents.Emit(GameEvents.ExitVehicle, v);
            AudioManager.I?.EngineStop();
        }

        // ------------------------------- COMBAT --------------------------------
        void TryShoot()
        {
            if (fireCooldown > 0 || Ammo <= 0) return;
            Ammo--; fireCooldown = 0.29f;
            var muzzle = transform.position + Vector3.up * 1.45f +
                         transform.forward * 0.6f;
            ParticleFX.MuzzleFlash(muzzle, transform.forward);
            AudioManager.I?.Play("sfx_gunshot", 0.8f);
            GameEvents.Emit(GameEvents.CrimeCommitted, "WEAPON_FIRE");

            var cam = Camera.main;
            if (cam != null && Physics.Raycast(cam.transform.position, cam.transform.forward,
                out var hit, 70f))
            {
                var ped = hit.collider.GetComponentInParent<Pedestrian>();
                if (ped != null) ped.Damage(22f);
                var cop = hit.collider.GetComponentInParent<PoliceOfficer>();
                if (cop != null) cop.Damage(22f);
                var veh = hit.collider.GetComponentInParent<Vehicle>();
                if (veh != null) veh.Damage(15f);
            }
            PedestrianSystem.Panic(transform.position, 16f);
        }

        public void Damage(float amount, string source = "")
        {
            if (Dead) return;
            Health -= amount;
            if (Health <= 0)
            {
                Health = 0; Dead = true;
                if (CurrentVehicle != null) ExitVehicle();
                Rig.Die();
                GameEvents.Emit(GameEvents.PlayerDied);
            }
        }

        public void Respawn(Vector3 pos)
        {
            transform.position = pos;
            Health = GameConfig.MaxHealth; Stamina = GameConfig.MaxStamina;
            Dead = false; Rig.Revive();
        }
    }

    // ========================================================================
    public class ThirdPersonCamera : MonoBehaviour
    {
        public static ThirdPersonCamera I { get; private set; }
        public float Yaw = 180f, Pitch = 12f;
        Vector3 smoothPos;

        void Awake() { I = this; }

        public void Tick(float dt)
        {
            var p = PlayerController.I;
            if (p == null) return;

            var look = GameInput.Look;
            Yaw += look.x;
            Pitch = Mathf.Clamp(Pitch - look.y, -20f, 70f);

            bool driving = p.CurrentVehicle != null;
            float dist = driving ? 8.6f + Mathf.Abs(p.CurrentVehicle.Speed) * 0.06f : 5.4f;
            float height = driving ? 3.2f : 2.1f;

            if (driving)   // camera follows behind vehicle heading
                Yaw = Mathf.LerpAngle(Yaw, p.CurrentVehicle.transform.eulerAngles.y,
                                       1 - Mathf.Exp(-3f * dt));

            Vector3 target = p.transform.position + Vector3.up * height;
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0);
            Vector3 wanted = target - rot * Vector3.forward * dist;

            // occlusion: pull in if a building blocks the boom
            if (Physics.Linecast(target, wanted, out var hit))
                wanted = hit.point + (target - wanted).normalized * 0.4f;
            if (wanted.y < 0.5f) wanted.y = 0.5f;

            smoothPos = smoothPos == Vector3.zero ? wanted
                : Vector3.Lerp(smoothPos, wanted, 1 - Mathf.Exp(-8f * dt));
            transform.position = smoothPos;
            transform.rotation = Quaternion.LookRotation(target - smoothPos);

            var cam = GetComponent<Camera>();
            float targetFov = driving
                ? 68f + 10f * Mathf.Clamp01(Mathf.Abs(p.CurrentVehicle.Speed) / 40f)
                : GameInput.Sprint && GameInput.Move.y > 0.1f ? 74f : 66f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1 - Mathf.Exp(-4f * dt));
        }
    }
}
