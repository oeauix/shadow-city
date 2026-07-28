// ============================================================================
// SHADOW CITY — Entities/Pedestrian.cs + PoliceSystem.cs
// Crowd with flee/greet resonance behavior; police with the last-known-
// position pursuit model (the design that made evasion fair in the web build).
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace ShadowCity
{
    public class Pedestrian : MonoBehaviour
    {
        public CharacterRig Rig;
        public string State = "WALK";
        public float Health = 40;
        float stateT; Vector2 dir; float speed;
        static SRandom rng = new(GameConfig.Seed ^ 0x9ed5);

        public static Pedestrian Create(Vector3 pos)
        {
            var go = new GameObject("ped");
            go.transform.position = pos;
            var p = go.AddComponent<Pedestrian>();
            p.Rig = CharacterRig.Create(go.transform, rng.Int(0, 3));
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.35f; col.height = 1.7f; col.center = new Vector3(0, 0.85f, 0);
            p.dir = new Vector2(rng.Chance(0.5f) ? 1 : -1, 0);
            if (rng.Chance(0.5f)) p.dir = new Vector2(0, p.dir.x);
            p.speed = rng.Range(1.2f, 1.8f);
            p.stateT = rng.Range(2, 8);
            return p;
        }

        public void Tick(float dt, Vector3 playerPos)
        {
            if (State == "DEAD") { Rig.Tick(dt, 0, 1); return; }
            stateT -= dt;
            float distSq = (transform.position - playerPos).sqrMagnitude;

            // Threat / resonance reaction
            if (State != "FLEE" && distSq < 16f * 16f)
            {
                float res = Resonance.Get(PlayerController.I?.District ?? "DOWNTOWN");
                bool threat = (PoliceSystem.Stars > 0) ||
                              (PlayerController.I != null && PlayerController.I.HasPistol && GameInput.Aim);
                if (threat || (res < -30 && rng.Chance(0.5f * dt)))
                { State = "FLEE"; stateT = rng.Range(4, 7); }
                else if (res > 30 && distSq < 5.5f * 5.5f && State == "WALK" && rng.Chance(0.3f * dt * 60))
                { State = "GREET"; stateT = 1.6f; }
            }

            float moveSpeed = 0;
            switch (State)
            {
                case "WALK":
                    moveSpeed = speed;
                    transform.position += new Vector3(dir.x, 0, dir.y) * speed * dt;
                    transform.rotation = Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0);
                    if (stateT <= 0)
                    {
                        if (rng.Chance(0.4f)) dir = -dir;
                        State = rng.Chance(0.5f) ? "IDLE" : "WALK";
                        stateT = rng.Range(2, 7);
                    }
                    var p = transform.position;
                    if (p.x < 4 || p.z < 4 || p.x > GameConfig.WorldSize - 4 || p.z > GameConfig.WorldSize - 4)
                        dir = -dir;
                    break;
                case "IDLE":
                case "GREET":
                    if (State == "GREET")
                    {
                        var look = playerPos - transform.position; look.y = 0;
                        if (look.sqrMagnitude > 0.1f)
                            transform.rotation = Quaternion.LookRotation(look);
                    }
                    if (stateT <= 0) { State = "WALK"; stateT = rng.Range(3, 8); }
                    break;
                case "FLEE":
                    moveSpeed = 5.2f;
                    var away = transform.position - playerPos; away.y = 0;
                    away = away.sqrMagnitude > 0.1f ? away.normalized : Vector3.forward;
                    transform.position += away * 5.2f * dt;
                    transform.rotation = Quaternion.LookRotation(away);
                    if (stateT <= 0 && distSq > 900) { State = "WALK"; stateT = rng.Range(4, 8); }
                    break;
            }
            Rig.Tick(dt, moveSpeed, 5.2f);
        }

        public void Damage(float amount)
        {
            if (State == "DEAD") return;
            Health -= amount;
            if (Health <= 0)
            {
                State = "DEAD"; Rig.Die();
                GameEvents.Emit(GameEvents.CrimeCommitted, "KILL_CIVILIAN");
            }
            else { State = "FLEE"; stateT = 6; }
        }
    }

    public static class PedestrianSystem
    {
        public static readonly List<Pedestrian> All = new();
        static Transform root; static float spawnT;
        static SRandom rng = new(GameConfig.Seed ^ 0x111);

        public static void Init() { root = new GameObject("Pedestrians").transform; }

        public static void Tick(float dt)
        {
            var pp = PlayerController.I != null ? PlayerController.I.transform.position : Vector3.zero;
            spawnT -= dt;
            int cap = GameConfig.Tier.Peds;
            if (DayNight.I != null && DayNight.I.IsNight) cap = (int)(cap * 0.6f);

            if (spawnT <= 0 && All.Count < cap)
            {
                spawnT = 0.5f;
                float ang = rng.Range(0, Mathf.PI * 2), dist = rng.Range(35, 110);
                float x = pp.x + Mathf.Sin(ang) * dist, z = pp.z + Mathf.Cos(ang) * dist;
                if (x > 6 && z > 6 && x < GameConfig.WorldSize - 6 && z < GameConfig.WorldSize - 6
                    && !CityGenerator.IsOnRoad(x, z))
                {
                    var ped = Pedestrian.Create(new Vector3(x, 0, z));
                    ped.transform.SetParent(root);
                    All.Add(ped);
                }
            }

            for (int i = All.Count - 1; i >= 0; i--)
            {
                var a = All[i];
                if (a == null) { All.RemoveAt(i); continue; }
                a.Tick(dt, pp);
                if ((a.transform.position - pp).sqrMagnitude > 190f * 190f)
                { All.RemoveAt(i); Object.Destroy(a.gameObject); }
            }
        }

        public static void Panic(Vector3 pos, float radius)
        {
            foreach (var a in All)
                if (a != null && a.State != "DEAD" &&
                    (a.transform.position - pos).sqrMagnitude < radius * radius)
                { a.State = "FLEE"; }
        }
    }

    // ========================================================================
    public class PoliceOfficer : MonoBehaviour
    {
        public CharacterRig Rig;
        public float Health = 60;
        float fireT = 1.5f;

        public static PoliceOfficer Create(Vector3 pos)
        {
            var go = new GameObject("officer");
            go.transform.position = pos;
            var o = go.AddComponent<PoliceOfficer>();
            o.Rig = CharacterRig.Create(go.transform, 0, police: true);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.4f; col.height = 1.75f; col.center = new Vector3(0, 0.87f, 0);
            return o;
        }

        public void Tick(float dt, Vector3 targetPos, bool hasLOS)
        {
            if (Health <= 0) { Rig.Tick(dt, 0, 1); return; }
            var to = targetPos - transform.position; to.y = 0;
            float dist = to.magnitude;
            float speed = 0;
            if (dist > 2f)
            {
                speed = 4.6f;
                transform.position += to.normalized * speed * dt;
            }
            if (to.sqrMagnitude > 0.1f) transform.rotation = Quaternion.LookRotation(to);
            Rig.Tick(dt, speed, 6.4f);

            // shoot at 2★+ with LOS
            if (PoliceSystem.Stars >= 2 && hasLOS && dist < 28f)
            {
                fireT -= dt;
                if (fireT <= 0)
                {
                    fireT = Random.Range(0.7f, 1.6f);
                    AudioManager.I?.Play("sfx_gunshot", 0.35f);
                    if (Random.value < 0.5f) PlayerController.I?.Damage(7f, "police");
                }
            }
        }

        public void Damage(float amount)
        {
            Health -= amount;
            if (Health <= 0)
            {
                Rig.Die();
                GameEvents.Emit(GameEvents.CrimeCommitted, "KILL_OFFICER");
            }
        }
    }

    public static class PoliceSystem
    {
        public static int Stars;
        public static float EvadeTimer;
        public static bool HasLOS;
        static readonly List<PoliceOfficer> officers = new();
        /// <summary>Read-only view for UI (minimap blips).</summary>
        public static List<PoliceOfficer> Officers => officers;
        static Vector3 lastKnown;                 // ★ the fairness mechanic
        static int crimeCount;
        static float spawnT, bustT;
        static Transform root;

        public static void Init()
        {
            root = new GameObject("Police").transform;
            Stars = 0; crimeCount = 0;
            GameEvents.On(GameEvents.CrimeCommitted, crime =>
            {
                int min = crime switch
                {
                    "CAR_THEFT" => 1, "WEAPON_FIRE" => 1, "ASSAULT" => 1,
                    "ROBBERY" => 2, "DESTROY_VEHICLE" => 2,
                    "KILL_CIVILIAN" => 3, "KILL_OFFICER" => 3, _ => 0
                };
                if (min == 0) return;
                crimeCount++;
                int target = Mathf.Max(Stars, min);
                if (crimeCount >= 3) { crimeCount = 0; target = Mathf.Min(GameConfig.MaxStars, target + 1); }
                if (PlayerController.I != null) lastKnown = PlayerController.I.transform.position;
                SetStars(target);
                EvadeTimer = GameConfig.EvadeSeconds[Stars];
            });
        }

        public static void SetStars(int n)
        {
            n = Mathf.Clamp(n, 0, GameConfig.MaxStars);
            if (n == Stars) return;
            int prev = Stars; Stars = n;
            GameEvents.Emit(GameEvents.WantedChanged, n);
            if (n > prev)
            {
                GameEvents.Emit(GameEvents.Notify, L10N.T("police.wantedUp"));
                if (prev == 0) AudioManager.I?.SirenStart();
            }
            if (n == 0)
            {
                crimeCount = 0;
                AudioManager.I?.SirenStop();
                foreach (var o in officers) if (o != null) Object.Destroy(o.gameObject);
                officers.Clear();
            }
        }

        public static void Tick(float dt)
        {
            var p = PlayerController.I;
            if (Stars == 0 || p == null || p.Dead) return;
            Vector3 pp = p.transform.position;

            // unit maintenance around LAST-KNOWN position
            spawnT -= dt;
            int want = GameConfig.UnitsPerStar[Stars];
            if (spawnT <= 0 && officers.Count < want && p.CurrentVehicle == null)
            {
                spawnT = 2.2f;
                Vector2 node = CityGenerator.NearestNode(
                    lastKnown.x + Random.Range(-40f, 40f), lastKnown.z + Random.Range(-40f, 40f));
                var o = PoliceOfficer.Create(new Vector3(node.x + Random.Range(-3f, 3f), 0,
                                                          node.y + Random.Range(-3f, 3f)));
                o.transform.SetParent(root);
                officers.Add(o);
            }

            // LOS check
            HasLOS = false;
            foreach (var o in officers)
            {
                if (o == null || o.Health <= 0) continue;
                if ((o.transform.position - pp).sqrMagnitude < 60f * 60f &&
                    !Physics.Linecast(o.transform.position + Vector3.up * 1.5f,
                                      pp + Vector3.up * 1.2f))
                { HasLOS = true; break; }
            }

            if (HasLOS) { lastKnown = pp; EvadeTimer = GameConfig.EvadeSeconds[Stars]; }
            else
            {
                EvadeTimer -= dt;
                if (EvadeTimer <= 0)
                {
                    SetStars(0);
                    GameEvents.Emit(GameEvents.Notify, L10N.T("police.evaded"));
                    RPG.AddXP(30, "night");
                    return;
                }
            }

            Vector3 chaseTarget = HasLOS ? pp : lastKnown;
            foreach (var o in officers)
                if (o != null) o.Tick(dt, chaseTarget, HasLOS);

            // busted: officer adjacent + player still + on foot
            if (p.CurrentVehicle == null)
            {
                bool close = false;
                foreach (var o in officers)
                    if (o != null && o.Health > 0 &&
                        (o.transform.position - pp).sqrMagnitude <
                        GameConfig.BustRadius * GameConfig.BustRadius) { close = true; break; }
                bool still = p.CC.velocity.sqrMagnitude < 1.4f;
                if (close && still)
                {
                    bustT += dt;
                    if (bustT >= GameConfig.BustStillSeconds)
                    { bustT = 0; SetStars(0); GameEvents.Emit(GameEvents.PlayerBusted); }
                }
                else bustT = 0;
            }
        }

        public static bool DamageOfficerNear(Vector3 pos, float radius, float amount)
        {
            foreach (var o in officers)
                if (o != null && o.Health > 0 &&
                    (o.transform.position - pos).sqrMagnitude < radius * radius)
                { o.Damage(amount); return true; }
            return false;
        }
    }
}
