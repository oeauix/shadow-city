// ============================================================================
// SHADOW CITY — UI/Minimap.cs
// GTA-style minimap, ported from the web build:
//   • city texture generated once from CityPlan (roads/parks/buildings/districts)
//   • scrolling zoomed window centered on the player (RawImage.uvRect)
//   • rotating player arrow, mission blip (edge-clamped), police blips
// 100% code — no textures or prefabs on disk.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShadowCity
{
    public class Minimap : MonoBehaviour
    {
        public static Minimap I { get; private set; }

        const int TexSize = 512;
        const float Window = 0.30f;          // fraction of world visible
        const float PanelSize = 170f;

        RawImage map;
        RectTransform panel, arrow, missionBlip;
        readonly List<RectTransform> copBlips = new();
        Texture2D tex;
        bool built;

        public static Minimap Create(Transform hudParent)
        {
            var go = new GameObject("Minimap", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(hudParent, false);
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 0);
            rt.offsetMin = new Vector2(18, 92);
            rt.offsetMax = new Vector2(18 + PanelSize, 92 + PanelSize);
            var mm = go.AddComponent<Minimap>();
            mm.BuildUI(rt);
            return mm;
        }

        void Awake() { I = this; }

        void BuildUI(RectTransform rt)
        {
            panel = rt;

            // border frame
            var frame = rt.gameObject.AddComponent<Image>();
            frame.color = new Color(0.06f, 0.08f, 0.12f, 0.9f);

            // map image (inset 3px)
            var mgo = new GameObject("map", typeof(RectTransform));
            var mrt = mgo.GetComponent<RectTransform>();
            mrt.SetParent(rt, false);
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one;
            mrt.offsetMin = new Vector2(3, 3); mrt.offsetMax = new Vector2(-3, -3);
            map = mgo.AddComponent<RawImage>();
            map.color = Color.white;

            arrow = Blip("player", new Color(0.4f, 0.95f, 1f), 14, MakeArrowSprite());
            missionBlip = Blip("mission", new Color(1f, 0.85f, 0.3f), 9, null);
            missionBlip.gameObject.SetActive(false);
        }

        RectTransform Blip(string name, Color col, float size, Sprite sprite)
        {
            var go = new GameObject("blip_" + name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(panel, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(-size / 2, -size / 2);
            rt.offsetMax = new Vector2(size / 2, size / 2);
            var img = go.AddComponent<Image>();
            img.color = col;
            if (sprite != null) img.sprite = sprite;
            return rt;
        }

        static Sprite MakeArrowSprite()
        {
            const int S = 16;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    // upward triangle: |x-center| grows with distance from top
                    float half = (S - 1 - y) * 0.42f;
                    bool inside = Mathf.Abs(x - (S - 1) / 2f) <= half && y > 1;
                    px[y * S + x] = inside ? new Color32(255, 255, 255, 255)
                                           : new Color32(0, 0, 0, 0);
                }
            t.SetPixels32(px); t.Apply();
            t.filterMode = FilterMode.Bilinear;
            return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }

        // --------------------------- CITY TEXTURE ------------------------------
        void BuildTexture(CityPlan plan)
        {
            built = true;
            tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[TexSize * TexSize];
            float world = GameConfig.WorldSize;
            float scale = TexSize / world;

            Color32 C32(int r, int g, int b) => new((byte)r, (byte)g, (byte)b, 235);
            var tint = new Dictionary<string, Color32>
            {
                { "DOWNTOWN",    C32(24, 29, 38) },
                { "NEON_STRIP",  C32(34, 24, 42) },
                { "OLD_QUARTER", C32(38, 33, 24) },
                { "HARBOR",      C32(22, 32, 38) },
                { "HILLS",       C32(26, 35, 26) },
            };

            // base: district tint
            for (int y = 0; y < TexSize; y++)
            {
                float wz = (y + 0.5f) / scale;
                for (int x = 0; x < TexSize; x++)
                {
                    float wx = (x + 0.5f) / scale;
                    string d = CityGenerator.DistrictAt(wx, wz);
                    px[y * TexSize + x] = tint.TryGetValue(d, out var c) ? c : C32(20, 22, 26);
                }
            }

            void FillRect(float x0, float z0, float x1, float z1, Color32 c)
            {
                int ax = Mathf.Max(0, (int)(x0 * scale)), bx = Mathf.Min(TexSize - 1, (int)(x1 * scale));
                int az = Mathf.Max(0, (int)(z0 * scale)), bz = Mathf.Min(TexSize - 1, (int)(z1 * scale));
                for (int y = az; y <= bz; y++)
                    for (int x = ax; x <= bx; x++)
                        px[y * TexSize + x] = c;
            }

            // lots: parks + building footprints
            var parkC = C32(30, 62, 36);
            var bldgC = C32(52, 58, 70);
            foreach (var lot in plan.Lots)
            {
                float hw = lot.W * 0.5f, hd = lot.D * 0.5f;
                if (lot.IsPark)
                    FillRect(lot.X - hw, lot.Z - hd, lot.X + hw, lot.Z + hd, parkC);
                else
                    FillRect(lot.X - hw * 0.62f, lot.Z - hd * 0.62f,
                             lot.X + hw * 0.62f, lot.Z + hd * 0.62f, bldgC);
            }

            // roads on top
            var roadC = C32(66, 71, 80);
            var artC = C32(88, 94, 106);
            foreach (var r in plan.RoadsH)
            {
                float w = (r.Arterial ? GameConfig.ArterialWidth : GameConfig.RoadWidth) * 0.5f;
                FillRect(0, r.Pos - w, world, r.Pos + w, r.Arterial ? artC : roadC);
            }
            foreach (var r in plan.RoadsV)
            {
                float w = (r.Arterial ? GameConfig.ArterialWidth : GameConfig.RoadWidth) * 0.5f;
                FillRect(r.Pos - w, 0, r.Pos + w, world, r.Arterial ? artC : roadC);
            }

            tex.SetPixels32(px);
            tex.Apply();
            map.texture = tex;
        }

        // ------------------------------- FRAME ----------------------------------
        public void Tick()
        {
            var p = PlayerController.I;
            if (p == null) return;
            if (!built)
            {
                if (CityGenerator.Plan == null) return;
                BuildTexture(CityGenerator.Plan);
            }

            float world = GameConfig.WorldSize;
            Vector3 pp = p.transform.position;

            // scroll window centered on player, clamped to map bounds
            float cx = Mathf.Clamp(pp.x / world - Window / 2f, 0f, 1f - Window);
            float cz = Mathf.Clamp(pp.z / world - Window / 2f, 0f, 1f - Window);
            map.uvRect = new Rect(cx, cz, Window, Window);

            // player arrow (rotates with heading; north-up map)
            float heading = p.CurrentVehicle != null
                ? p.CurrentVehicle.transform.eulerAngles.y : p.Heading;
            arrow.localEulerAngles = new Vector3(0, 0, -heading);
            PlaceBlip(arrow, pp, cx, cz, false);

            // mission blip: active objective, else nearest offer
            Vector3? target = null;
            if (Missions.Current != null && Missions.Current.Objectives.Count > Missions.Current.Stage)
                target = Missions.Current.Objectives[Missions.Current.Stage].Pos;
            else if (Missions.Offers.Count > 0)
            {
                float best = float.MaxValue;
                foreach (var o in Missions.Offers)
                {
                    float d = (o.Pos - pp).sqrMagnitude;
                    if (d < best) { best = d; target = o.Pos; }
                }
            }
            missionBlip.gameObject.SetActive(target.HasValue);
            if (target.HasValue) PlaceBlip(missionBlip, target.Value, cx, cz, true);

            // police blips while wanted
            var officers = PoliceSystem.Officers;
            int need = PoliceSystem.Stars > 0 ? officers.Count : 0;
            while (copBlips.Count < need)
                copBlips.Add(Blip("cop" + copBlips.Count, new Color(1f, 0.28f, 0.32f), 7, null));
            for (int i = 0; i < copBlips.Count; i++)
            {
                bool on = i < need && officers[i] != null;
                copBlips[i].gameObject.SetActive(on);
                if (on) PlaceBlip(copBlips[i], officers[i].transform.position, cx, cz, true);
            }
        }

        void PlaceBlip(RectTransform blip, Vector3 world, float cx, float cz, bool clampEdge)
        {
            float ws = GameConfig.WorldSize;
            float rx = (world.x / ws - cx) / Window - 0.5f;   // −0.5..0.5 inside view
            float rz = (world.z / ws - cz) / Window - 0.5f;
            float half = PanelSize * 0.5f - 10f;
            float bx = rx * (PanelSize - 6f);
            float bz = rz * (PanelSize - 6f);
            if (clampEdge)
            {
                bx = Mathf.Clamp(bx, -half, half);
                bz = Mathf.Clamp(bz, -half, half);
            }
            blip.anchoredPosition = new Vector2(bx, bz);
        }
    }
}
