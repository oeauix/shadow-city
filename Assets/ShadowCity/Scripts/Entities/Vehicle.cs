// ============================================================================
// SHADOW CITY — Entities/Vehicle.cs + TrafficSystem.cs
// Arcade vehicle (kinematic integration like the proven web physics —
// stable at any mobile framerate) + ambient traffic on the road grid with
// red-light behavior, spawn/despawn ring around the player.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace ShadowCity
{
    public class Vehicle : MonoBehaviour
    {
        public VehicleDef Def;
        public float Speed;
        public float Throttle, Steer;
        public bool Handbrake;
        public string Owner = "ambient";
        public PlayerController Driver;
        public float HP;
        public bool Destroyed;
        public bool LightsOn;

        bool hasAIModel;
        Transform[] wheels = new Transform[4];
        Renderer bodyR; Material headM, tailM;
        float wheelSpin;
        ParticleSystem exhaustFX, smokeFX;

        // Ambient AI state
        public bool AIActive;
        Vector2 aiDir; float aiSpeed;

        public static Vehicle Create(VehicleDef def, Vector3 pos, float headingDeg, int colorIdx)
        {
            var go = new GameObject("veh_" + def.Id);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, headingDeg, 0));
            var v = go.AddComponent<Vehicle>();
            v.Def = def; v.HP = 100;
            v.BuildMesh(def.Colors[colorIdx % def.Colors.Length]);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = new Vector3(0, def.H / 2, 0);
            bc.size = new Vector3(def.W, def.H, def.L);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;             // we integrate manually (web-proven model)
            return v;
        }

        void BuildMesh(Color color)
        {
            // AI model swap: use Resources/Models/<id> when available
            var ai = ModelLibrary.TrySpawn(Def.Id, transform);
            if (ai != null)
            {
                hasAIModel = true;
                var shb = ShaderLib.Lit;
                headM = new Material(shb) { color = new Color(0.1f, 0.1f, 0.08f) };
                tailM = new Material(shb) { color = new Color(0.13f, 0.04f, 0.04f) };
                return;   // wheels/lights skipped; model is one piece
            }

            var sh = ShaderLib.Lit;
            Material Mk(Color c, float met = 0.7f, float sm = 0.6f)
            {
                var m = new Material(sh) { color = c };
                m.SetFloat("_Metallic", met); m.SetFloat("_Smoothness", sm);
                return m;
            }
            GameObject Box(string n, Vector3 pos, Vector3 size, Material m)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(b.GetComponent<Collider>());
                b.name = n; b.transform.SetParent(transform, false);
                b.transform.localPosition = pos; b.transform.localScale = size;
                b.GetComponent<Renderer>().sharedMaterial = m;
                return b;
            }

            var bodyMat = Mk(color);
            var body = Box("body", new Vector3(0, Def.H * 0.36f, 0),
                new Vector3(Def.W, Def.H * 0.55f, Def.L), bodyMat);
            bodyR = body.GetComponent<Renderer>();
            Box("cabin", new Vector3(0, Def.H * 0.8f, -Def.L * 0.05f),
                new Vector3(Def.W * 0.85f, Def.H * 0.5f, Def.L * 0.5f),
                Mk(new Color(0.06f, 0.08f, 0.1f), 0.9f, 0.85f));

            var wheelMat = Mk(new Color(0.08f, 0.08f, 0.09f), 0.1f, 0.3f);
            float wz = Def.L * 0.34f, wx = Def.W * 0.48f;
            var wp = new[] { new Vector3(-wx, 0.33f, wz), new Vector3(wx, 0.33f, wz),
                             new Vector3(-wx, 0.33f, -wz), new Vector3(wx, 0.33f, -wz) };
            for (int i = 0; i < 4; i++)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(w.GetComponent<Collider>());
                w.name = "wheel" + i;
                w.transform.SetParent(transform, false);
                w.transform.localPosition = wp[i];
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
                w.transform.localScale = new Vector3(0.66f, 0.13f, 0.66f);
                w.GetComponent<Renderer>().sharedMaterial = wheelMat;
                wheels[i] = w.transform;
            }

            headM = Mk(new Color(0.1f, 0.1f, 0.08f), 0, 0.9f);
            tailM = Mk(new Color(0.13f, 0.04f, 0.04f), 0, 0.9f);
            Box("hl", new Vector3(-Def.W * 0.32f, Def.H * 0.42f, Def.L / 2 + 0.02f), new Vector3(0.3f, 0.14f, 0.06f), headM);
            Box("hr", new Vector3(Def.W * 0.32f, Def.H * 0.42f, Def.L / 2 + 0.02f), new Vector3(0.3f, 0.14f, 0.06f), headM);
            Box("tl", new Vector3(-Def.W * 0.32f, Def.H * 0.42f, -Def.L / 2 - 0.02f), new Vector3(0.34f, 0.12f, 0.06f), tailM);
            Box("tr", new Vector3(Def.W * 0.32f, Def.H * 0.42f, -Def.L / 2 - 0.02f), new Vector3(0.34f, 0.12f, 0.06f), tailM);
            if (Def.Id == "police")
                Box("bar", new Vector3(0, Def.H * 1.06f, -Def.L * 0.05f), new Vector3(0.7f, 0.12f, 0.3f),
                    Mk(new Color(0.9f, 0.1f, 0.1f), 0.2f, 0.8f));
            if (Def.Id == "taxi")
                Box("sign", new Vector3(0, Def.H * 1.06f, 0), new Vector3(0.5f, 0.16f, 0.24f),
                    Mk(new Color(1f, 0.84f, 0.37f), 0.1f, 0.6f));
        }

        public void Tick(float dt)
        {
            if (Destroyed)
            {
                if (exhaustFX != null) { exhaustFX.Stop(); exhaustFX = null; }
                return;
            }
            // exhaust only while player drives; damage smoke below 40% HP
            bool driven = Driver != null;
            if (driven && exhaustFX == null)
                exhaustFX = ParticleFX.Exhaust(transform, Def.L);
            else if (!driven && exhaustFX != null)
            { exhaustFX.Stop(); Destroy(exhaustFX.gameObject, 2f); exhaustFX = null; }
            if (HP < 40 && smokeFX == null)
                smokeFX = ParticleFX.DamageSmoke(transform, Def.H);

            if (Driver != null) TickPhysics(dt);
            else if (AIActive) TickAI(dt);

            // visuals
            if (hasAIModel)
            {
                // one-piece AI model: subtle body lean only
                transform.localEulerAngles = new Vector3(0, transform.localEulerAngles.y, 0);
                return;
            }
            wheelSpin += Speed * dt / 0.33f * Mathf.Rad2Deg;
            for (int i = 0; i < 4; i++)
            {
                if (wheels[i] == null) continue;
                float steerY = i < 2 ? Steer * 26f : 0;
                wheels[i].localRotation = Quaternion.Euler(wheelSpin, steerY, 90);
            }
            bool braking = Throttle < -0.05f && Speed > 0.5f;
            LightsOn = DayNight.I != null && DayNight.I.Darkness() > 0.4f;
            headM.SetColor("_EmissionColor", LightsOn ? new Color(1f, 0.95f, 0.78f) * 2f : Color.black);
            headM.EnableKeyword("_EMISSION");
            tailM.SetColor("_EmissionColor",
                braking ? Color.red * 3f : LightsOn ? new Color(0.54f, 0.08f, 0.06f) * 2f : Color.black);
            tailM.EnableKeyword("_EMISSION");
        }

        void TickPhysics(float dt)
        {
            float grip = Def.Grip * (Handbrake ? 0.35f : 1f);
            float top = Def.TopSpeed * (1f + RPG.DrivingBoost());

            if (Throttle > 0.05f)
                Speed += Def.Accel * (1f - Mathf.Clamp01(Speed / top)) * Throttle * dt;
            else if (Throttle < -0.05f)
                Speed = Speed > 0.5f ? Speed - Def.Brake * dt
                       : Mathf.Max(Speed + Def.Accel * 0.5f * Throttle * dt, -top * 0.3f);
            else
                Speed = Mathf.MoveTowards(Speed, 0, (2.2f + Mathf.Abs(Speed) * 0.08f) * dt);
            if (Handbrake) Speed = Mathf.MoveTowards(Speed, 0, Def.Brake * 0.7f * dt);

            float steerEff = Steer * Def.TurnRate * Mathf.Clamp01(Mathf.Abs(Speed) / 8f)
                             * Mathf.Sign(Speed) * grip;
            transform.Rotate(0, steerEff * dt * Mathf.Rad2Deg, 0);

            Vector3 before = transform.position;
            Vector3 next = before + transform.forward * Speed * dt;

            // sweep against buildings
            if (Physics.BoxCast(before + Vector3.up * Def.H / 2,
                new Vector3(Def.W / 2, Def.H / 2 - 0.1f, 0.2f),
                transform.forward, out var hit, transform.rotation,
                Mathf.Abs(Speed * dt) + Def.L / 2))
            {
                if (!hit.collider.transform.IsChildOf(transform) && hit.distance < Def.L / 2 + Mathf.Abs(Speed * dt))
                {
                    float impact = Mathf.Abs(Speed);
                    if (impact > 6f)
                    {
                        Damage(impact * 1.1f);
                        Driver?.Damage(impact * 0.4f, "crash");
                        ParticleFX.CrashDebris(transform.position + transform.forward * Def.L / 2);
                        AudioManager.I?.Play("sfx_crash", Mathf.Clamp01(impact / 30f));
                    }
                    Speed *= -0.25f;
                    next = before + transform.forward * Speed * dt;
                }
            }
            transform.position = next;
        }

        void TickAI(float dt)
        {
            // grid-following: drive straight, stop for player, turn at nodes
            float b = GameConfig.BlockSize;
            Vector3 pos = transform.position;
            float target = 9.5f;

            // stop if player/anything directly ahead
            Vector3 look = pos + transform.forward * 8f + Vector3.up;
            if (PlayerController.I != null &&
                (look - PlayerController.I.transform.position).sqrMagnitude < 12f) target = 0;

            aiSpeed = Mathf.MoveTowards(aiSpeed, target, 12f * dt);
            Speed = aiSpeed;
            transform.position += transform.forward * aiSpeed * dt;

            // at a node? maybe turn (deterministic-ish wobble)
            Vector2 node = CityGenerator.NearestNode(pos.x, pos.z);
            if (Mathf.Abs(pos.x - node.x) < 1.2f && Mathf.Abs(pos.z - node.y) < 1.2f)
            {
                if (Random.value < 0.012f)
                    transform.Rotate(0, Random.value < 0.5f ? 90 : -90, 0);
            }
            // keep in world
            if (pos.x < 8 || pos.z < 8 || pos.x > GameConfig.WorldSize - 8 || pos.z > GameConfig.WorldSize - 8)
                transform.Rotate(0, 180, 0);
        }

        public void Damage(float amount)
        {
            if (Destroyed) return;
            HP -= amount;
            if (HP <= 0)
            {
                Destroyed = true;
                if (bodyR != null) bodyR.material.color = new Color(0.1f, 0.1f, 0.11f);
                ParticleFX.Explosion(transform.position);
                AudioManager.I?.Play("sfx_explosion", 1f);
                if (Driver != null) { Driver.Damage(55f, "explosion"); Driver.ExitVehicle(); }
                GameEvents.Emit(GameEvents.CrimeCommitted, "DESTROY_VEHICLE");
            }
        }

        public int SpeedKmh => Mathf.Abs(Mathf.RoundToInt(Speed * 3.6f));
    }

    // ========================================================================
    public static class TrafficSystem
    {
        public static readonly List<Vehicle> All = new();
        static Transform root;
        static float spawnTimer;
        static SRandom rng;

        public static void Init(CityPlan plan)
        {
            rng = new SRandom(GameConfig.Seed ^ 0x7247);
            root = new GameObject("Traffic").transform;
            // parked cars at kerbs
            int parked = Mathf.Min(plan.VehicleSpawns.Count, GameConfig.Tier.Traffic);
            for (int i = 0; i < parked; i++)
            {
                var sp = plan.VehicleSpawns[i * plan.VehicleSpawns.Count / parked];
                var def = GameConfig.Vehicles[rng.Int(0, 3)];
                var v = Vehicle.Create(def, new Vector3(sp.x, 0, sp.y),
                    rng.Chance(0.5f) ? 0 : 90, rng.Int(0, 2));
                v.transform.SetParent(root);
                All.Add(v);
            }
        }

        public static void Tick(float dt)
        {
            var pp = PlayerController.I != null ? PlayerController.I.transform.position : Vector3.zero;
            spawnTimer -= dt;

            int moving = 0;
            foreach (var v in All) if (v != null && v.AIActive && !v.Destroyed) moving++;

            if (spawnTimer <= 0 && moving < GameConfig.Tier.Traffic / 2)
            {
                spawnTimer = 1.4f;
                // spawn a mover on a road ring around the player
                float ang = rng.Range(0, Mathf.PI * 2);
                float dist = rng.Range(70, 140);
                float x = pp.x + Mathf.Sin(ang) * dist, z = pp.z + Mathf.Cos(ang) * dist;
                if (x > 20 && z > 20 && x < GameConfig.WorldSize - 20 && z < GameConfig.WorldSize - 20)
                {
                    Vector2 node = CityGenerator.NearestNode(x, z);
                    var def = GameConfig.Vehicles[rng.Int(0, 3)];
                    bool horizontal = rng.Chance(0.5f);
                    var v = Vehicle.Create(def,
                        new Vector3(horizontal ? x : node.x + GameConfig.LaneOffset, 0,
                                    horizontal ? node.y + GameConfig.LaneOffset : z),
                        horizontal ? 90 : 0, rng.Int(0, 2));
                    v.AIActive = true;
                    v.transform.SetParent(root);
                    All.Add(v);
                }
            }

            for (int i = All.Count - 1; i >= 0; i--)
            {
                var v = All[i];
                if (v == null) { All.RemoveAt(i); continue; }
                v.Tick(dt);
                // despawn far movers
                if (v.AIActive && v.Driver == null &&
                    (v.transform.position - pp).sqrMagnitude > 260f * 260f)
                {
                    All.RemoveAt(i);
                    Object.Destroy(v.gameObject);
                }
            }
        }

        public static void ReleaseToPlayer(Vehicle v) => v.AIActive = false;
    }
}
