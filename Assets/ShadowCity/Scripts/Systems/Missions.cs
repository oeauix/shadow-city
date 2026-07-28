// ============================================================================
// SHADOW CITY — Systems/Missions.cs
// Mission engine: offers at district anchors gated by the Dual Clock,
// objective state machine (goto/pickup/hold/escape/pulse/checkpoints),
// rewards feeding cash + XP + resonance. Ported from the tested web engine.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace ShadowCity
{
    public static class Missions
    {
        public class Offer
        {
            public string Id, District;
            public Vector3 Pos;
            public GameObject Marker;
            public bool IsStory;
            public string Title() => L10N.T("mission." + Id);
        }

        public class Objective
        {
            public string Type;         // goto | hold | escape | pulse
            public Vector3 Pos;
            public float Radius = 6, Seconds, TimeLimit;
            public string TextKey = "mission.goto";
            public bool NeedsVehicle;
        }

        public class Active
        {
            public string Id, District;
            public List<Objective> Objectives = new();
            public int Stage;
            public int Pay; public int XP; public string Tree;
            public float ResonanceDelta;
            public float Timer, HoldT;
            public bool IsStory;
            public GameObject Marker;
        }

        public static readonly List<Offer> Offers = new();
        public static Active Current;
        public static int StoryIndex;
        static float refreshT;
        static SRandom rng;
        static Transform root;
        static bool pulseFiredFlag;

        static readonly string[] DayMissions = { "taxi", "delivery", "courier" };
        static readonly string[] NightMissions = { "race", "robbery", "smuggle" };

        public static void Init()
        {
            rng = new SRandom(GameConfig.Seed ^ 0x315);
            root = new GameObject("Missions").transform;
            Offers.Clear(); Current = null; StoryIndex = 0;
            GameEvents.On(GameEvents.PulseFired, _ => pulseFiredFlag = true);
        }

        public static Offer OfferNear(Vector3 pos, float radius)
        {
            foreach (var o in Offers)
                if ((o.Pos - pos).sqrMagnitude < radius * radius) return o;
            return null;
        }

        public static void Tick(float dt)
        {
            refreshT -= dt;
            if (refreshT <= 0) { refreshT = 3f; RefreshOffers(); }
            foreach (var o in Offers)
                if (o.Marker != null) o.Marker.transform.Rotate(0, 90 * dt, 0);
            if (Current != null) TickActive(dt);
        }

        static void RefreshOffers()
        {
            bool day = DayNight.I.IsDay;
            // drop out-of-phase offers
            for (int i = Offers.Count - 1; i >= 0; i--)
            {
                bool isDayM = System.Array.IndexOf(DayMissions, Offers[i].Id) >= 0;
                if (!Offers[i].IsStory && isDayM != day) RemoveOffer(i);
            }
            if (Offers.Count >= 2 || Current != null) return;

            var plan = CityGenerator.Plan;
            // story S1 first
            if (StoryIndex == 0 && !Offers.Exists(o => o.IsStory))
            { SpawnOffer("s1", "NEON_STRIP", true); return; }

            var pool = day ? DayMissions : NightMissions;
            string id = pool[rng.Int(0, pool.Length - 1)];
            if (!Offers.Exists(o => o.Id == id))
            {
                var districts = new List<string>(plan.DistrictAnchors.Keys);
                SpawnOffer(id, districts[rng.Int(0, districts.Count - 1)], false);
            }
        }

        static void SpawnOffer(string id, string district, bool story)
        {
            var anchor = CityGenerator.Plan.DistrictAnchors[district];
            var o = new Offer
            {
                Id = id, District = district, IsStory = story,
                Pos = new Vector3(anchor.x + rng.Range(-5, 5), 0, anchor.y + rng.Range(-5, 5)),
            };
            o.Marker = MakeMarker(o.Pos, story ? new Color(0.49f, 0.78f, 1f) : new Color(1f, 0.82f, 0.31f), 3.4f);
            o.Marker.transform.SetParent(root);
            Offers.Add(o);
        }

        static GameObject MakeMarker(Vector3 pos, Color col, float y)
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(m.GetComponent<Collider>());
            m.transform.position = pos + Vector3.up * y;
            m.transform.localScale = new Vector3(1.2f, 0.7f, 1.2f);
            var sh = ShaderLib.Lit;
            var mat = new Material(sh) { color = col };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", col * 2f);
            m.GetComponent<Renderer>().sharedMaterial = mat;
            return m;
        }

        static void RemoveOffer(int i)
        {
            if (Offers[i].Marker != null) Object.Destroy(Offers[i].Marker);
            Offers.RemoveAt(i);
        }

        // ------------------------------ BUILDERS -------------------------------
        public static void Accept(Offer offer)
        {
            if (Current != null) return;
            var plan = CityGenerator.Plan;
            var a = new Active { Id = offer.Id, District = offer.District, IsStory = offer.IsStory };

            Vector3 RandomAnchor(string exclude)
            {
                var keys = new List<string>(plan.DistrictAnchors.Keys);
                keys.Remove(exclude);
                var k = keys[rng.Int(0, keys.Count - 1)];
                var v = plan.DistrictAnchors[k];
                return new Vector3(v.x, 0, v.y);
            }

            switch (offer.Id)
            {
                case "s1":
                    a.Objectives.Add(new Objective { Type = "goto", Pos = offer.Pos + new Vector3(40, 0, 0), Radius = 8 });
                    a.Objectives.Add(new Objective { Type = "pulse", TextKey = "mission.usePulse" });
                    a.Pay = 100; a.XP = 200; a.Tree = null; a.ResonanceDelta = 0;
                    GameEvents.Emit(GameEvents.Subtitle, L10N.T("story.s1.line"));
                    AudioManager.I?.PlayDialog("dlg_s1");
                    break;
                case "taxi":
                    for (int f = 0; f < 2; f++)
                    {
                        a.Objectives.Add(new Objective { Type = "goto", Pos = RandomAnchor(null), Radius = 8, TextKey = "mission.pickup", NeedsVehicle = true });
                        a.Objectives.Add(new Objective { Type = "goto", Pos = RandomAnchor(null), Radius = 8, TextKey = "mission.dropoff", NeedsVehicle = true });
                    }
                    a.Pay = 140; a.XP = 140; a.Tree = "day"; a.ResonanceDelta = 4;
                    break;
                case "delivery":
                    a.Objectives.Add(new Objective { Type = "goto", Pos = offer.Pos, Radius = 6, TextKey = "mission.pickup" });
                    var dest = RandomAnchor(offer.District);
                    float d = Vector3.Distance(offer.Pos, dest);
                    a.Objectives.Add(new Objective { Type = "goto", Pos = dest, Radius = 6, TextKey = "mission.deliver", TimeLimit = 40 + d * 0.35f });
                    a.Pay = 120; a.XP = 100; a.Tree = "day"; a.ResonanceDelta = 3;
                    break;
                case "courier":
                    for (int s = 0; s < 3; s++)
                        a.Objectives.Add(new Objective { Type = "goto", Pos = RandomAnchor(null), Radius = 5, TextKey = "mission.deliver" });
                    a.Pay = 175; a.XP = 120; a.Tree = "day"; a.ResonanceDelta = 4;
                    break;
                case "race":
                    Vector3 last = offer.Pos;
                    for (int c = 0; c < 5; c++)
                    {
                        Vector2 node = CityGenerator.NearestNode(
                            last.x + rng.Range(-1, 1) * GameConfig.BlockSize * 3,
                            last.z + rng.Range(-1, 1) * GameConfig.BlockSize * 3);
                        node.x = Mathf.Clamp(node.x, GameConfig.BlockSize, GameConfig.WorldSize - GameConfig.BlockSize);
                        node.y = Mathf.Clamp(node.y, GameConfig.BlockSize, GameConfig.WorldSize - GameConfig.BlockSize);
                        last = new Vector3(node.x, 0, node.y);
                        a.Objectives.Add(new Objective { Type = "goto", Pos = last, Radius = 9, TextKey = "mission.checkpoint", NeedsVehicle = true, TimeLimit = 35 });
                    }
                    a.Pay = 260; a.XP = 320; a.Tree = "night"; a.ResonanceDelta = -4;
                    break;
                case "robbery":
                    a.Objectives.Add(new Objective { Type = "goto", Pos = offer.Pos, Radius = 5 });
                    a.Objectives.Add(new Objective { Type = "hold", Pos = offer.Pos, Radius = 6, Seconds = 6, TextKey = "mission.hold" });
                    a.Objectives.Add(new Objective { Type = "escape", Pos = offer.Pos, Radius = 110, TextKey = "mission.escape" });
                    a.Pay = 190; a.XP = 200; a.Tree = "night"; a.ResonanceDelta = -11;
                    break;
                case "smuggle":
                    var harbor = plan.DistrictAnchors["HARBOR"];
                    a.Objectives.Add(new Objective { Type = "goto", Pos = new Vector3(harbor.x, 0, harbor.y), Radius = 7, TextKey = "mission.pickup" });
                    a.Objectives.Add(new Objective { Type = "goto", Pos = RandomAnchor("HARBOR"), Radius = 7, TextKey = "mission.deliver", TimeLimit = 95 });
                    a.Pay = 240; a.XP = 320; a.Tree = "night"; a.ResonanceDelta = -7;
                    break;
            }

            Current = a;
            int idx = Offers.FindIndex(o => o == offer);
            if (idx >= 0) RemoveOffer(idx);
            BeginStage();
            GameEvents.Emit(GameEvents.MissionStarted, a);
        }

        static void BeginStage()
        {
            var obj = Current.Objectives[Current.Stage];
            Current.Timer = obj.TimeLimit;
            Current.HoldT = 0;
            pulseFiredFlag = false;
            if (Current.Marker != null) Object.Destroy(Current.Marker);
            Current.Marker = null;
            if (obj.Type != "pulse")
            {
                Current.Marker = MakeMarker(obj.Pos,
                    Current.IsStory ? new Color(0.49f, 0.78f, 1f) : new Color(1f, 0.9f, 0.5f), 20f);
                Current.Marker.transform.localScale = new Vector3(obj.Radius, 30f, obj.Radius);
                var mat = Current.Marker.GetComponent<Renderer>().sharedMaterial;
                var c = mat.color; c.a = 0.16f; mat.color = c;
                Current.Marker.transform.SetParent(root);
            }

            // robbery heat
            if (obj.Type == "hold") PoliceSystem.SetStars(Mathf.Max(PoliceSystem.Stars, 2));
        }

        static void TickActive(float dt)
        {
            var p = PlayerController.I;
            if (p == null) return;
            if (p.Dead) { Fail(); return; }

            var obj = Current.Objectives[Current.Stage];
            if (obj.TimeLimit > 0)
            {
                Current.Timer -= dt;
                if (Current.Timer <= 0) { Fail(); return; }
            }

            Vector3 pp = p.transform.position;
            float distSq = (new Vector3(obj.Pos.x, pp.y, obj.Pos.z) - pp).sqrMagnitude;

            switch (obj.Type)
            {
                case "goto":
                    if (obj.NeedsVehicle && p.CurrentVehicle == null) break;
                    if (distSq < obj.Radius * obj.Radius) Advance();
                    break;
                case "hold":
                    if (distSq < obj.Radius * obj.Radius)
                    {
                        Current.HoldT += dt;
                        if (Current.HoldT >= obj.Seconds) Advance();
                    }
                    else Current.HoldT = Mathf.Max(0, Current.HoldT - dt * 2);
                    break;
                case "escape":
                    if (distSq > obj.Radius * obj.Radius &&
                        (PoliceSystem.Stars == 0 || !PoliceSystem.HasLOS)) Advance();
                    break;
                case "pulse":
                    if (pulseFiredFlag) Advance();
                    break;
            }
        }

        static void Advance()
        {
            Current.Stage++;
            if (Current.Stage >= Current.Objectives.Count) Succeed();
            else BeginStage();
        }

        static void Succeed()
        {
            var m = Current;
            Cleanup();
            Economy.Earn(m.Pay);
            RPG.AddXP(m.XP, m.Tree);
            if (m.ResonanceDelta != 0) Resonance.Shift(m.District, m.ResonanceDelta);
            if (m.IsStory) StoryIndex++;
            GameEvents.Emit(GameEvents.MissionCompleted, m);
            GameEvents.Emit(GameEvents.Notify,
                L10N.T("mission.reward", L10N.Money(m.Pay), m.XP));
            AudioManager.I?.Play("sfx_pickup", 0.9f);
        }

        static void Fail()
        {
            Cleanup();
            GameEvents.Emit(GameEvents.MissionFailed, null);
        }

        static void Cleanup()
        {
            if (Current?.Marker != null) Object.Destroy(Current.Marker);
            Current = null;
        }

        public static string HudLine()
        {
            if (Current == null) return null;
            var obj = Current.Objectives[Current.Stage];
            string line = L10N.T(obj.TextKey);
            if (obj.Type == "hold")
                line = L10N.T("mission.hold", Mathf.CeilToInt(obj.Seconds - Current.HoldT));
            if (obj.TimeLimit > 0)
                line += "  ·  " + L10N.T("mission.timeLeft", Mathf.CeilToInt(Current.Timer));
            return line;
        }
    }
}
