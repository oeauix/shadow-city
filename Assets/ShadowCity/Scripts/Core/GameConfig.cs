// ============================================================================
// SHADOW CITY — Core/GameConfig.cs
// Single source of truth for every tunable. Values ported 1:1 from the
// web build that passed 840 assertions — treat as tested balance data.
// ============================================================================
using UnityEngine;

namespace ShadowCity
{
    public static class GameConfig
    {
        // ------------------------------- WORLD --------------------------------
        public const int Seed = 20260728;
        public const float WorldSize = 760f;        // metres (half of web: mobile perf)
        public const float BlockSize = 76f;
        public const float RoadWidth = 14f;
        public const float ArterialWidth = 20f;
        public const float SidewalkWidth = 4.2f;
        public const float LaneOffset = 3.4f;

        // ------------------------------- TIME ---------------------------------
        public const float DayLengthRealSeconds = 24f * 60f;  // 24 min = 24 h
        public const float StartHour = 17.4f;                 // golden dusk
        public const float SunriseHour = 6f, SunsetHour = 19f;

        // ------------------------------ PLAYER --------------------------------
        public const float WalkSpeed = 3.4f, RunSpeed = 6.4f, SprintSpeed = 9.6f;
        public const float JumpVelocity = 6.8f, Gravity = 21.5f;
        public const float MaxHealth = 100f, MaxStamina = 100f, MaxFocus = 100f;
        public const float StaminaSprintDrain = 16f, StaminaRegen = 12f;
        public const float FocusRegen = 4.5f;
        public const float InteractRadius = 3.2f;

        // ------------------------------- PULSE --------------------------------
        public const float PulseBaseCost = 25f, PulseMinCost = 8f;
        public const float PulseBaseRadius = 34f, PulseRadiusPerRank = 9f;
        public const float PulseExpandTime = 1.15f, PulseRevealBase = 4f;
        public const float PulseRevealPerRank = 1.25f, PulseCooldown = 1.4f;

        // ----------------------------- RESONANCE ------------------------------
        public const float ResonanceDecayPerGameHour = 0.55f;
        public const float PriceFactorAtFear = 1.5f, PriceFactorAtRespect = 0.8f;
        public static readonly Color NeonNeutral = new Color(0.44f, 0.84f, 1f);
        public static readonly Color NeonRespect = new Color(1f, 0.77f, 0.4f);
        public static readonly Color NeonFear    = new Color(1f, 0.13f, 0.22f);

        // -------------------------------- RPG ----------------------------------
        public const int MaxLevel = 30, SkillMaxRank = 5;
        public static int XPForLevel(int level) =>
            Mathf.RoundToInt(100f * Mathf.Pow(level, 1.6f) / 10f) * 10;
        public const float PhaseXPBonus = 0.25f;   // Dual Clock: day/night tree bonus

        // ------------------------------ ECONOMY --------------------------------
        public const int StartCash = 250, HospitalFee = 120, BustedFine = 200;

        // ------------------------------ POLICE ---------------------------------
        public const int MaxStars = 5;
        public static readonly float[] EvadeSeconds = { 0, 14, 22, 32, 45, 60 };
        public static readonly int[] UnitsPerStar  = { 0, 1, 2, 3, 4, 6 };
        public const float BustRadius = 2.6f, BustStillSeconds = 1.6f;

        // ------------------------------ VEHICLES -------------------------------
        // id, topSpeed, accel, brake, grip, turnRate, w, h, l
        public static readonly VehicleDef[] Vehicles =
        {
            new VehicleDef("sedan",  34f,  9.5f, 26f, 0.90f, 2.15f, 1.85f, 1.42f, 4.5f,
                new[]{ new Color(0.54f,0.14f,0.19f), new Color(0.17f,0.29f,0.48f), new Color(0.21f,0.22f,0.25f) }),
            new VehicleDef("sports", 47f, 15.5f, 32f, 1.02f, 2.6f,  1.9f,  1.18f, 4.4f,
                new[]{ new Color(0.84f,0.15f,0.24f), new Color(1f,0.77f,0f), new Color(0f,0.7f,0.78f) }),
            new VehicleDef("taxi",   33f,  9.0f, 26f, 0.92f, 2.2f,  1.85f, 1.46f, 4.55f,
                new[]{ new Color(0.96f,0.71f,0f) }),
            new VehicleDef("van",    28f,  7.0f, 22f, 0.82f, 1.8f,  2.0f,  2.1f,  5.2f,
                new[]{ new Color(0.87f,0.87f,0.87f), new Color(0.36f,0.3f,0.54f) }),
            new VehicleDef("police", 40f, 12.5f, 30f, 0.98f, 2.4f,  1.9f,  1.45f, 4.7f,
                new[]{ new Color(0.08f,0.09f,0.12f) }),
        };

        // --------------------------- QUALITY TIERS -----------------------------
        // drawDistance, traffic, peds, windowDensity, shadows
        public static readonly QualityTier[] Tiers =
        {
            new QualityTier("LOW",    260f,  8, 12, 0.4f, false),
            new QualityTier("MEDIUM", 380f, 14, 22, 0.7f, false),
            new QualityTier("HIGH",   520f, 20, 34, 1.0f, true),
        };
        public static int CurrentTier = 1;
        public static QualityTier Tier => Tiers[Mathf.Clamp(CurrentTier, 0, Tiers.Length - 1)];
    }

    public class VehicleDef
    {
        public readonly string Id;
        public readonly float TopSpeed, Accel, Brake, Grip, TurnRate, W, H, L;
        public readonly Color[] Colors;
        public VehicleDef(string id, float top, float acc, float brake, float grip,
                          float turn, float w, float h, float l, Color[] colors)
        { Id = id; TopSpeed = top; Accel = acc; Brake = brake; Grip = grip;
          TurnRate = turn; W = w; H = h; L = l; Colors = colors; }
    }

    public class QualityTier
    {
        public readonly string Name;
        public readonly float DrawDistance;
        public readonly int Traffic, Peds;
        public readonly float WindowDensity;
        public readonly bool Shadows;
        public QualityTier(string n, float dd, int tr, int pd, float wd, bool sh)
        { Name = n; DrawDistance = dd; Traffic = tr; Peds = pd; WindowDensity = wd; Shadows = sh; }
    }
}
