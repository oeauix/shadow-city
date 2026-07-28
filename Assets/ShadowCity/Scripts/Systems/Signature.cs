// ============================================================================
// SHADOW CITY — Systems/Signature.cs
// Resonance + Echo Pulse + RPG + Economy — the interlocked loop from the
// philosophy doc, values from the tested web build.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace ShadowCity
{
    // ------------------------------ RESONANCE ------------------------------
    public static class Resonance
    {
        static readonly Dictionary<string, float> map = new();
        public static readonly string[] Districts =
            { "DOWNTOWN", "NEON_STRIP", "OLD_QUARTER", "HARBOR", "HILLS" };

        public static void Init()
        {
            map.Clear();
            foreach (var d in Districts) map[d] = 0;

            GameEvents.On(GameEvents.CrimeCommitted, crime =>
            {
                float delta = (string)crime switch
                {
                    "CAR_THEFT" => -4, "WEAPON_FIRE" => -3, "ASSAULT" => -3,
                    "ROBBERY" => -11, "KILL_CIVILIAN" => -14, "KILL_OFFICER" => -9,
                    "DESTROY_VEHICLE" => -9, _ => 0
                };
                if (delta != 0 && PlayerController.I != null)
                    Shift(PlayerController.I.District, delta);
            });
            GameEvents.On(GameEvents.HourTick, _ =>
            {
                foreach (var d in Districts)
                    map[d] = Mathf.MoveTowards(map[d], 0, GameConfig.ResonanceDecayPerGameHour);
            });
        }

        public static float Get(string district) =>
            map.TryGetValue(district, out var v) ? v : 0;

        public static void Shift(string district, float delta)
        {
            if (string.IsNullOrEmpty(district) || !map.ContainsKey(district)) return;
            float before = map[district];
            map[district] = Mathf.Clamp(before + delta, -100, 100);
            GameEvents.Emit(GameEvents.ResonanceChanged, district);

            foreach (int level in new[] { 30, 60 })
            {
                if (before < level && map[district] >= level)
                    GameEvents.Emit(GameEvents.Notify,
                        L10N.T("res.respected", L10N.T("district." + district)));
                if (before > -level && map[district] <= -level)
                    GameEvents.Emit(GameEvents.Notify,
                        L10N.T("res.feared", L10N.T("district." + district)));
            }
        }

        public static float PriceFactor(string district)
        {
            float v = Get(district) / 100f;
            return v >= 0 ? Mathf.Lerp(1f, GameConfig.PriceFactorAtRespect, v)
                          : Mathf.Lerp(1f, GameConfig.PriceFactorAtFear, -v);
        }

        public static float[] Serialize()
        {
            var a = new float[Districts.Length];
            for (int i = 0; i < Districts.Length; i++) a[i] = map[Districts[i]];
            return a;
        }
        public static void Deserialize(float[] a)
        {
            if (a == null) return;
            for (int i = 0; i < Districts.Length && i < a.Length; i++)
                map[Districts[i]] = a[i];
        }
    }

    // ------------------------------ ECHO PULSE ------------------------------
    public static class Pulse
    {
        static GameObject ring; static Material ringMat;
        static float t = -1, radius;
        static readonly List<(Transform tr, GameObject glow, float until)> reveals = new();
        static float cooldown;

        public static float Cost() =>
            Mathf.Max(GameConfig.PulseMinCost,
                GameConfig.PulseBaseCost - 2f * (RPG.Rank(3) + RPG.Rank(0)) / 2f);

        public static float Radius() =>
            GameConfig.PulseBaseRadius + GameConfig.PulseRadiusPerRank * RPG.Rank(3);

        public static void Fire()
        {
            var p = PlayerController.I;
            if (p == null || cooldown > 0 || t >= 0 || p.Focus < Cost()) return;
            p.Focus -= Cost();
            t = 0; cooldown = GameConfig.PulseCooldown;
            radius = Radius();

            if (ring == null)
            {
                ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.Destroy(ring.GetComponent<Collider>());
                var sh = ShaderLib.Lit;
                ringMat = new Material(sh) { color = new Color(0.2f, 0.88f, 1f, 0.5f) };
                ringMat.EnableKeyword("_EMISSION");
                ringMat.SetColor("_EmissionColor", new Color(0.2f, 0.88f, 1f) * 2f);
                SetTransparent(ringMat);
                ring.GetComponent<Renderer>().sharedMaterial = ringMat;
            }
            ring.SetActive(true);
            ring.transform.position = p.transform.position + Vector3.up * 0.2f;
            ParticleFX.PulseMotes(p.transform.position, radius);
            AudioManager.I?.Play("sfx_pulse", 0.9f);
            GameEvents.Emit(GameEvents.PulseFired);
        }

        static void SetTransparent(Material m) => ShaderLib.MakeTransparent(m);

        public static void Tick(float dt)
        {
            if (cooldown > 0) cooldown -= dt;
            if (t >= 0)
            {
                t += dt / GameConfig.PulseExpandTime;
                float r = Mathf.Pow(Mathf.Clamp01(t), 0.6f) * radius;
                ring.transform.localScale = new Vector3(r * 2, 0.05f, r * 2);
                var c = ringMat.color; c.a = 0.5f * (1 - t); ringMat.color = c;

                if (t >= 1)
                {
                    t = -1; ring.SetActive(false);
                    RevealTargets();
                }
            }
            // expire reveals
            for (int i = reveals.Count - 1; i >= 0; i--)
            {
                var (tr, glow, until) = reveals[i];
                if (tr == null || Time.time > until)
                {
                    if (glow != null) Object.Destroy(glow);
                    reveals.RemoveAt(i);
                }
                else glow.transform.position = tr.position + Vector3.up * 1.2f;
            }
        }

        static void RevealTargets()
        {
            var p = PlayerController.I;
            float dur = GameConfig.PulseRevealBase + GameConfig.PulseRevealPerRank * RPG.Rank(3);
            float r2 = radius * radius;

            void Glow(Transform target, Color col)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.Destroy(g.GetComponent<Collider>());
                g.transform.localScale = Vector3.one * 1.6f;
                var sh = ShaderLib.Lit;
                var m = new Material(sh) { color = new Color(col.r, col.g, col.b, 0.3f) };
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", col * 2.4f);
                SetTransparent(m);
                g.GetComponent<Renderer>().sharedMaterial = m;
                reveals.Add((target, g, Time.time + dur));
            }

            foreach (var v in TrafficSystem.All)
                if (v != null && !v.Destroyed && v.Owner != "player" &&
                    (v.transform.position - p.transform.position).sqrMagnitude < r2)
                    Glow(v.transform, new Color(0.31f, 0.85f, 1f));
            foreach (var o in Missions.Offers)
                if (o.Marker != null &&
                    (o.Marker.transform.position - p.transform.position).sqrMagnitude < r2)
                    Glow(o.Marker.transform, new Color(1f, 0.82f, 0.31f));
        }
    }

    // --------------------------------- RPG ----------------------------------
    public static class RPG
    {
        public static int Level = 1, XP, SkillPoints;
        // 0 charm, 1 trade, 2 endurance, 3 stealth, 4 gunplay, 5 driving
        public static readonly int[] Ranks = new int[6];
        public static readonly string[] SkillKeys =
            { "skill.charm", "skill.trade", "skill.endurance", "skill.stealth", "skill.gunplay", "skill.driving" };

        public static void Init()
        { Level = 1; XP = 0; SkillPoints = 0; System.Array.Clear(Ranks, 0, 6); }

        public static int Rank(int i) => Ranks[i];
        public static float FocusMult() => 1f + (Ranks[0] + Ranks[3]) * 0.05f;
        public static float DrivingBoost() => Ranks[5] * 0.04f;

        public static void AddXP(int amount, string tree = null)
        {
            if (Level >= GameConfig.MaxLevel) return;
            float bonus = 1f;
            if (tree == "day" && DayNight.I != null && DayNight.I.IsDay) bonus += GameConfig.PhaseXPBonus;
            if (tree == "night" && DayNight.I != null && DayNight.I.IsNight) bonus += GameConfig.PhaseXPBonus;
            int granted = Mathf.RoundToInt(amount * bonus);
            XP += granted;
            GameEvents.Emit(GameEvents.XPGained, granted);

            while (Level < GameConfig.MaxLevel && XP >= GameConfig.XPForLevel(Level))
            {
                XP -= GameConfig.XPForLevel(Level);
                Level++; SkillPoints++;
                GameEvents.Emit(GameEvents.LevelUp, Level);
                GameEvents.Emit(GameEvents.Notify, L10N.T("rpg.levelUp", Level));
                AudioManager.I?.Play("sfx_levelup", 0.8f);
            }
        }

        public static bool Upgrade(int skill)
        {
            if (SkillPoints <= 0 || Ranks[skill] >= GameConfig.SkillMaxRank) return false;
            SkillPoints--; Ranks[skill]++;
            return true;
        }
    }

    // ------------------------------- ECONOMY --------------------------------
    public static class Economy
    {
        public static int Cash;
        static readonly List<Vector2> shops = new();

        public static void Init(CityPlan plan)
        {
            Cash = GameConfig.StartCash;
            shops.Clear();
            shops.AddRange(plan.ShopSpots);
        }

        public static void Earn(int n)
        { Cash += n; GameEvents.Emit(GameEvents.CashChanged, Cash); }

        public static bool Spend(int n)
        {
            if (n > Cash) { GameEvents.Emit(GameEvents.Notify, L10N.T("shop.noCash")); return false; }
            Cash -= n; GameEvents.Emit(GameEvents.CashChanged, Cash);
            return true;
        }

        public static bool ShopNear(Vector3 pos, float radius)
        {
            foreach (var s in shops)
                if ((new Vector3(s.x, pos.y, s.y) - pos).sqrMagnitude < radius * radius)
                    return true;
            return false;
        }

        public static int Price(int basePrice) =>
            Mathf.Max(1, Mathf.RoundToInt(basePrice *
                Resonance.PriceFactor(PlayerController.I?.District ?? "DOWNTOWN") *
                (1f - 0.03f * RPG.Rank(0))));
    }
}
