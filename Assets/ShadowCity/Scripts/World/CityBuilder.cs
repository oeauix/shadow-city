// ============================================================================
// SHADOW CITY — World/CityBuilder.cs
// Builds the visible city from the plan: ground, roads (merged mesh),
// buildings with EMISSIVE WINDOWS (the signature look — resonance-tinted),
// neon signs, street lamps, park trees. All meshes combined per-district
// for mobile draw-call budgets. Also registers static colliders.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace ShadowCity
{
    public class CityBuilder : MonoBehaviour
    {
        public static CityBuilder I { get; private set; }

        // Per-district window materials (emission tinted by Resonance at runtime)
        public readonly Dictionary<string, Material> WindowMats = new();
        public readonly List<Light> StreetLamps = new();
        Material buildingMat, roadMat, sidewalkMat, parkMat, trunkMat, leafMat, markMat;

        static readonly Color[][] Palettes =
        {
            new[]{ C(0x3a,0x41,0x50), C(0x5b,0x6a,0x80), C(0x2e,0x29,0x37), C(0x4b,0x3a,0x5a) }, // DOWNTOWN / STRIP
            new[]{ C(0x54,0x47,0x3a), C(0x77,0x63,0x50), C(0x37,0x41,0x3f), C(0x4e,0x5a,0x55) }, // OLD / HARBOR
        };
        static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

        void Awake() { I = this; }

        Material Std(Color c, float metallic = 0f, float smooth = 0.35f)
        {
            var sh = ShaderLib.Lit;
            var m = new Material(sh) { color = c };
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            return m;
        }

        public void Build(CityPlan plan)
        {
            // Building hulls carry per-vertex palette colors + face shading —
            // needs the vertex-color shader (Standard/URP ignore COLOR).
            buildingMat = new Material(ShaderLib.VertexColorLit) { color = Color.white };
            roadMat = Std(C(0x1c, 0x1e, 0x22), 0.05f, 0.42f);
            sidewalkMat = Std(C(0x4a, 0x4c, 0x4e), 0f, 0.2f);
            parkMat = Std(C(0x2e, 0x46, 0x26), 0f, 0.1f);
            trunkMat = Std(C(0x4a, 0x35, 0x24));
            leafMat = Std(C(0x2d, 0x4a, 0x26));
            markMat = Std(C(0xc8, 0xc4, 0xa8), 0f, 0.5f);

            BuildGroundAndRoads(plan);
            BuildBuildings(plan);
            BuildProps(plan);
        }

        // ------------------------------------------------------------------ //
        void BuildGroundAndRoads(CityPlan plan)
        {
            float size = GameConfig.WorldSize;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(size / 2, 0, size / 2);
            ground.transform.localScale = new Vector3(size / 10 * 1.4f, 1, size / 10 * 1.4f);
            ground.GetComponent<Renderer>().sharedMaterial = Std(C(0x22, 0x24, 0x1f), 0, 0.05f);

            // Roads as merged quads (asphalt slightly above ground, markings above roads)
            var road = new MeshBatch("Roads", roadMat, transform);
            var walk = new MeshBatch("Sidewalks", sidewalkMat, transform);
            var mark = new MeshBatch("Markings", markMat, transform);

            foreach (var r in plan.RoadsH)
            {
                float w = r.Arterial ? GameConfig.ArterialWidth : GameConfig.RoadWidth;
                road.AddQuad(-10, r.Pos - w / 2, size + 10, r.Pos + w / 2, 0.02f);
                walk.AddQuad(-10, r.Pos - w / 2 - GameConfig.SidewalkWidth, size + 10, r.Pos - w / 2, 0.06f);
                walk.AddQuad(-10, r.Pos + w / 2, size + 10, r.Pos + w / 2 + GameConfig.SidewalkWidth, 0.06f);
                for (float x = 4; x < size - 4; x += 7)
                    mark.AddQuad(x, r.Pos - 0.14f, x + 3, r.Pos + 0.14f, 0.04f);
            }
            foreach (var r in plan.RoadsV)
            {
                float w = r.Arterial ? GameConfig.ArterialWidth : GameConfig.RoadWidth;
                road.AddQuad(r.Pos - w / 2, -10, r.Pos + w / 2, size + 10, 0.03f);
                walk.AddQuad(r.Pos - w / 2 - GameConfig.SidewalkWidth, -10, r.Pos - w / 2, size + 10, 0.065f);
                walk.AddQuad(r.Pos + w / 2, -10, r.Pos + w / 2 + GameConfig.SidewalkWidth, size + 10, 0.065f);
                for (float z = 4; z < size - 4; z += 7)
                    mark.AddQuad(r.Pos - 0.14f, z, r.Pos + 0.14f, z + 3, 0.045f);
            }
            road.Flush(); walk.Flush(); mark.Flush();
        }

        // ------------------------------------------------------------------ //
        void BuildBuildings(CityPlan plan)
        {
            var rng = new SRandom(GameConfig.Seed ^ 0x0B1D);
            // one batch per district: hull + windows separately
            var hulls = new Dictionary<string, MeshBatch>();
            var windows = new Dictionary<string, MeshBatch>();
            foreach (var d in new[] { "DOWNTOWN", "NEON_STRIP", "OLD_QUARTER", "HARBOR", "HILLS" })
            {
                hulls[d] = new MeshBatch("Hull_" + d, buildingMat, transform, useColors: true);
                var wm = Std(Color.black);
                wm.EnableKeyword("_EMISSION");
                wm.SetColor("_EmissionColor", GameConfig.NeonNeutral * 0f);
                wm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                WindowMats[d] = wm;
                windows[d] = new MeshBatch("Windows_" + d, wm, transform);
            }

            var parks = new MeshBatch("Parks", parkMat, transform);
            float density = GameConfig.Tier.WindowDensity;

            foreach (var lot in plan.Lots)
            {
                if (lot.IsPark)
                {
                    parks.AddQuad(lot.X - lot.W / 2, lot.Z - lot.D / 2,
                                  lot.X + lot.W / 2, lot.Z + lot.D / 2, 0.05f);
                    continue;
                }
                float h = lot.Floors * 3.2f;
                float sx = Mathf.Min(lot.W, rng.Range(11, 20));
                float sz = Mathf.Min(lot.D, rng.Range(10, 18));

                var palette = (lot.District == "OLD_QUARTER" || lot.District == "HARBOR")
                    ? Palettes[1] : Palettes[0];
                Color col = palette[lot.PaletteIndex % palette.Length];

                hulls[lot.District].AddBox(lot.X, h / 2, lot.Z, sx, h, sz, col);

                // collider (one BoxCollider per building — cheap & exact)
                var colGo = new GameObject("bcol");
                colGo.transform.SetParent(transform, false);
                colGo.transform.position = new Vector3(lot.X, h / 2, lot.Z);
                var bc = colGo.AddComponent<BoxCollider>();
                bc.size = new Vector3(sx, h, sz);
                colGo.layer = 0;

                // windows: quads on ±Z and ±X faces
                var wb = windows[lot.District];
                int rows = Mathf.Max(1, (int)(h / 3.6f) - 1);
                int colsX = Mathf.Max(1, (int)(sx / 3.4f) - 1);
                int colsZ = Mathf.Max(1, (int)(sz / 3.4f) - 1);
                for (int r = 1; r <= rows; r++)
                {
                    float wy = r * 3.4f;
                    for (int c2 = 0; c2 < colsX; c2++)
                    {
                        if (rng.Next() > density * 0.5f) continue;
                        float wx = lot.X + (c2 - (colsX - 1) / 2f) * 3.4f;
                        wb.AddWindow(wx, wy, lot.Z + sz / 2 + 0.06f, 0);
                        if (rng.Next() < density * 0.5f)
                            wb.AddWindow(wx, wy, lot.Z - sz / 2 - 0.06f, 2);
                    }
                    for (int c2 = 0; c2 < colsZ; c2++)
                    {
                        if (rng.Next() > density * 0.4f) continue;
                        float wz = lot.Z + (c2 - (colsZ - 1) / 2f) * 3.4f;
                        wb.AddWindow(lot.X + sx / 2 + 0.06f, wy, wz, 1);
                    }
                }
            }

            foreach (var b in hulls.Values) b.Flush();
            foreach (var b in windows.Values) b.Flush();
            parks.Flush();
        }

        // ------------------------------------------------------------------ //
        void BuildProps(CityPlan plan)
        {
            var rng = new SRandom(GameConfig.Seed ^ 0x9807);
            float size = GameConfig.WorldSize;
            var poleBatch = new MeshBatch("LampPoles", Std(C(0x2c, 0x2e, 0x33), 0.6f, 0.5f), transform);
            var trunkBatch = new MeshBatch("Trunks", trunkMat, transform);
            var leafBatch = new MeshBatch("Leaves", leafMat, transform);

            // Street lamps along arterials; a few carry REAL point lights (pool)
            int lampCount = 0;
            foreach (var r in plan.RoadsH)
            {
                if (!r.Arterial) continue;
                for (float x = 19; x < size; x += 57)
                {
                    float z = r.Pos - (r.Arterial ? GameConfig.ArterialWidth : GameConfig.RoadWidth) / 2 - 1.2f;
                    poleBatch.AddBox(x, 3.7f, z, 0.18f, 7.4f, 0.18f, Color.white);
                    if (lampCount++ % 4 == 0 && StreetLamps.Count < 14)
                    {
                        var lgo = new GameObject("lamp");
                        lgo.transform.SetParent(transform, false);
                        lgo.transform.position = new Vector3(x, 6.8f, z + 1.2f);
                        var li = lgo.AddComponent<Light>();
                        li.type = LightType.Point;
                        li.color = new Color(1f, 0.7f, 0.39f);
                        li.range = 26; li.intensity = 0;
                        StreetLamps.Add(li);
                    }
                }
            }

            // Park trees
            foreach (var lot in plan.Lots)
            {
                if (!lot.IsPark) continue;
                int n = rng.Int(3, 7);
                for (int i = 0; i < n; i++)
                {
                    float tx = lot.X + rng.Range(-lot.W / 2 + 2, lot.W / 2 - 2);
                    float tz = lot.Z + rng.Range(-lot.D / 2 + 2, lot.D / 2 - 2);
                    float s = rng.Range(0.8f, 1.5f);
                    trunkBatch.AddBox(tx, 1.3f * s, tz, 0.3f * s, 2.6f * s, 0.3f * s, Color.white);
                    leafBatch.AddBox(tx, 3.4f * s, tz, 2.6f * s, 2.4f * s, 2.6f * s, Color.white);
                }
            }
            poleBatch.Flush(); trunkBatch.Flush(); leafBatch.Flush();
        }

        /// <summary>Per-frame: resonance→window emission, lamps at night.</summary>
        public void Tick()
        {
            float dark = DayNight.I.Darkness();
            foreach (var kv in WindowMats)
            {
                float v = Resonance.Get(kv.Key) / 100f;   // −1..1
                Color target = v >= 0
                    ? Color.Lerp(GameConfig.NeonNeutral, GameConfig.NeonRespect, v)
                    : Color.Lerp(GameConfig.NeonNeutral, GameConfig.NeonFear, -v);
                kv.Value.SetColor("_EmissionColor", target * (dark * 2.2f));
            }
            bool lampsOn = dark > 0.45f;
            foreach (var l in StreetLamps)
                l.intensity = Mathf.MoveTowards(l.intensity, lampsOn ? 1.6f : 0f, Time.deltaTime * 3f);
        }
    }

    // ======================================================================= //
    /// <summary>Accumulates quads/boxes, flushes into combined meshes ≤64k verts.</summary>
    public class MeshBatch
    {
        readonly string name; readonly Material mat; readonly Transform parent;
        readonly bool useColors;
        List<Vector3> v = new(); List<int> t = new(); List<Color> c = new();
        int flushed;

        public MeshBatch(string n, Material m, Transform p, bool useColors = false)
        { name = n; mat = m; parent = p; this.useColors = useColors; }

        void CheckFlush() { if (v.Count > 62000) Flush(); }

        public void AddQuad(float x0, float z0, float x1, float z1, float y)
        {
            int b = v.Count;
            v.Add(new(x0, y, z0)); v.Add(new(x1, y, z0));
            v.Add(new(x1, y, z1)); v.Add(new(x0, y, z1));
            t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            if (useColors) for (int i = 0; i < 4; i++) c.Add(Color.white);
            CheckFlush();
        }

        // face: 0=+Z 1=+X 2=−Z 3=−X (vertical 1.7×2.2 window quad)
        public void AddWindow(float x, float y, float z, int face)
        {
            int b = v.Count;
            float hw = 0.85f, hh = 1.1f;
            switch (face)
            {
                case 0:
                    v.Add(new(x - hw, y - hh, z)); v.Add(new(x + hw, y - hh, z));
                    v.Add(new(x + hw, y + hh, z)); v.Add(new(x - hw, y + hh, z));
                    t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 }); break;
                case 2:
                    v.Add(new(x - hw, y - hh, z)); v.Add(new(x + hw, y - hh, z));
                    v.Add(new(x + hw, y + hh, z)); v.Add(new(x - hw, y + hh, z));
                    t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 }); break;
                default:
                    v.Add(new(x, y - hh, z - hw)); v.Add(new(x, y - hh, z + hw));
                    v.Add(new(x, y + hh, z + hw)); v.Add(new(x, y + hh, z - hw));
                    if (face == 1) t.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
                    else t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                    break;
            }
            if (useColors) for (int i = 0; i < 4; i++) c.Add(Color.white);
            CheckFlush();
        }

        public void AddBox(float cx, float cy, float cz, float sx, float sy, float sz, Color col)
        {
            float hx = sx / 2, hy = sy / 2, hz = sz / 2;
            var center = new Vector3(cx, cy, cz);
            // (normal, tangentU, tangentV, shade) — 6 faces, shading in vertex color
            var faces = new (Vector3 n, Vector3 u, Vector3 w, float shade)[]
            {
                (new( hx,0,0), new(0,0, hz), new(0, hy,0), 0.95f),
                (new(-hx,0,0), new(0,0,-hz), new(0, hy,0), 0.85f),
                (new(0, hy,0), new( hx,0,0), new(0,0, hz), 1.05f),
                (new(0,-hy,0), new(-hx,0,0), new(0,0, hz), 0.60f),
                (new(0,0, hz), new(-hx,0,0), new(0, hy,0), 0.90f),
                (new(0,0,-hz), new( hx,0,0), new(0, hy,0), 0.80f),
            };
            foreach (var (n, u, w, shade) in faces)
            {
                int b = v.Count;
                Vector3 ctr = center + n;
                v.Add(ctr - u - w);
                v.Add(ctr + u - w);
                v.Add(ctr + u + w);
                v.Add(ctr - u + w);
                t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
                Color cc = col * shade; cc.a = 1;
                if (useColors) for (int i = 0; i < 4; i++) c.Add(cc);
            }
            CheckFlush();
        }

        public void Flush()
        {
            if (v.Count == 0) return;
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            if (useColors && c.Count == v.Count) mesh.SetColors(c);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject(name + "_" + flushed++);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = GameConfig.Tier.Shadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;

            v = new(); t = new(); c = new();
        }
    }
}
