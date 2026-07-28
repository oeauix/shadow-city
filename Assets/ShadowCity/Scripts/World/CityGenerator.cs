// ============================================================================
// SHADOW CITY — World/CityGenerator.cs
// Seeded deterministic city plan (pure data, no Unity objects) — direct port
// of the web build's citygen that passed determinism + connectivity tests.
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowCity
{
    public class CityPlan
    {
        public int BlocksPerSide;
        public List<Lot> Lots = new();
        public List<RoadLine> RoadsH = new(), RoadsV = new();
        public List<Vector2> VehicleSpawns = new();
        public Vector3 PlayerSpawn;
        public Dictionary<string, Vector2> DistrictAnchors = new();
        public List<Vector2> ShopSpots = new();
    }

    public class Lot
    {
        public float X, Z, W, D;          // center + size
        public string District;
        public bool IsPark;
        public int Floors;
        public int PaletteIndex;
    }

    public class RoadLine { public float Pos; public bool Arterial; }

    /// <summary>Deterministic RNG — mulberry32, same as web build.</summary>
    public class SRandom
    {
        uint s;
        public SRandom(int seed) { s = (uint)seed; if (s == 0) s = 1; }
        public float Next()
        {
            s += 0x6D2B79F5u;
            uint t = s;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return ((t ^ (t >> 14)) & 0xFFFFFFu) / 16777216f;
        }
        public float Range(float a, float b) => a + Next() * (b - a);
        public int Int(int a, int b) => a + (int)(Next() * (b - a + 1));
        public bool Chance(float p) => Next() < p;
        public T Pick<T>(T[] arr) => arr[(int)(Next() * arr.Length) % arr.Length];
    }

    public static class CityGenerator
    {
        public static CityPlan Plan { get; private set; }

        // District zones in normalized map space (from web CONFIG)
        static readonly (string id, float x0, float z0, float x1, float z1)[] Zones =
        {
            ("HILLS",       0.00f, 0.00f, 1.00f, 0.28f),
            ("OLD_QUARTER", 0.00f, 0.28f, 0.30f, 0.86f),
            ("DOWNTOWN",    0.30f, 0.30f, 0.70f, 0.66f),
            ("NEON_STRIP",  0.30f, 0.66f, 0.86f, 0.86f),
            ("HARBOR",      0.00f, 0.86f, 1.00f, 1.00f),
        };

        public static string DistrictAt(float x, float z)
        {
            float nx = Mathf.Clamp01(x / GameConfig.WorldSize);
            float nz = Mathf.Clamp01(z / GameConfig.WorldSize);
            string found = "DOWNTOWN";
            foreach (var (id, x0, z0, x1, z1) in Zones)
                if (nx >= x0 && nx <= x1 && nz >= z0 && nz <= z1) found = id;
            return found;
        }

        // District skyline recipes: minFloors, maxFloors
        static (int lo, int hi) FloorsFor(string d) => d switch
        {
            "DOWNTOWN" => (6, 18),
            "NEON_STRIP" => (2, 6),
            "OLD_QUARTER" => (2, 5),
            "HARBOR" => (1, 3),
            _ => (1, 2),
        };

        public static CityPlan Generate(int seed)
        {
            var rng = new SRandom(seed);
            var p = new CityPlan();
            float size = GameConfig.WorldSize, block = GameConfig.BlockSize;
            int n = Mathf.FloorToInt(size / block);
            p.BlocksPerSide = n;

            for (int i = 0; i <= n; i++)
            {
                p.RoadsH.Add(new RoadLine { Pos = i * block, Arterial = i % 3 == 0 });
                p.RoadsV.Add(new RoadLine { Pos = i * block, Arterial = i % 3 == 0 });
            }

            // Lots: 2×2 per block with margins
            for (int bz = 0; bz < n; bz++)
                for (int bx = 0; bx < n; bx++)
                {
                    float rw0 = (p.RoadsV[bx].Arterial ? GameConfig.ArterialWidth : GameConfig.RoadWidth) / 2;
                    float rw1 = (p.RoadsV[bx + 1].Arterial ? GameConfig.ArterialWidth : GameConfig.RoadWidth) / 2;
                    float rh0 = (p.RoadsH[bz].Arterial ? GameConfig.ArterialWidth : GameConfig.RoadWidth) / 2;
                    float rh1 = (p.RoadsH[bz + 1].Arterial ? GameConfig.ArterialWidth : GameConfig.RoadWidth) / 2;
                    float x0 = bx * block + rw0 + GameConfig.SidewalkWidth;
                    float x1 = (bx + 1) * block - rw1 - GameConfig.SidewalkWidth;
                    float z0 = bz * block + rh0 + GameConfig.SidewalkWidth;
                    float z1 = (bz + 1) * block - rh1 - GameConfig.SidewalkWidth;
                    if (x1 - x0 < 10 || z1 - z0 < 10) continue;

                    string district = DistrictAt((x0 + x1) / 2, (z0 + z1) / 2);
                    bool park = rng.Chance(0.07f) && district != "HARBOR";
                    var (lo, hi) = FloorsFor(district);

                    if (park)
                    {
                        p.Lots.Add(new Lot { X = (x0 + x1) / 2, Z = (z0 + z1) / 2,
                            W = x1 - x0, D = z1 - z0, District = district, IsPark = true });
                        continue;
                    }
                    for (int lz = 0; lz < 2; lz++)
                        for (int lx = 0; lx < 2; lx++)
                        {
                            float lw = (x1 - x0) / 2, ld = (z1 - z0) / 2;
                            float m = rng.Range(1.5f, 3.5f);
                            p.Lots.Add(new Lot
                            {
                                X = x0 + lw * (lx + 0.5f), Z = z0 + ld * (lz + 0.5f),
                                W = lw - m * 2, D = ld - m * 2,
                                District = district,
                                Floors = rng.Int(lo, hi),
                                PaletteIndex = rng.Int(0, 3),
                            });
                        }
                }

            // Vehicle spawn kerbs along roads
            for (int i = 1; i < n; i++)
                for (float t = block; t < size - block; t += block * 1.5f)
                {
                    if (rng.Chance(0.5f))
                        p.VehicleSpawns.Add(new Vector2(i * block + GameConfig.LaneOffset, t));
                    else
                        p.VehicleSpawns.Add(new Vector2(t, i * block + GameConfig.LaneOffset));
                }

            // Player spawn: Neon Strip road node + sidewalk offset
            float psx = size * 0.58f, psz = size * 0.76f;
            float nodeX = Mathf.Round(psx / block) * block;
            float nodeZ = Mathf.Round(psz / block) * block;
            p.PlayerSpawn = new Vector3(nodeX + 3.5f, 1.2f,
                nodeZ + GameConfig.RoadWidth / 2 + GameConfig.SidewalkWidth / 2);

            // District anchors (mission/shop placement)
            foreach (var (id, x0, z0, x1, z1) in Zones)
            {
                float ax = size * (x0 + x1) / 2, az = size * (z0 + z1) / 2;
                float snapX = Mathf.Round(ax / block) * block + block / 2;
                float snapZ = Mathf.Round(az / block) * block + block / 2;
                p.DistrictAnchors[id] = new Vector2(snapX, snapZ);
                p.ShopSpots.Add(new Vector2(snapX + 8, snapZ + GameConfig.BlockSize / 2 - 6));
            }

            Plan = p;
            return p;
        }

        /// <summary>Nearest road-grid node — cars snap here, AI navigates grid.</summary>
        public static Vector2 NearestNode(float x, float z)
        {
            float b = GameConfig.BlockSize;
            return new Vector2(Mathf.Round(x / b) * b, Mathf.Round(z / b) * b);
        }

        public static bool IsOnRoad(float x, float z)
        {
            float b = GameConfig.BlockSize;
            float lx = Mathf.Abs(x - Mathf.Round(x / b) * b);
            float lz = Mathf.Abs(z - Mathf.Round(z / b) * b);
            return lx < GameConfig.ArterialWidth / 2 || lz < GameConfig.ArterialWidth / 2;
        }
    }
}
