// ============================================================================
// SHADOW CITY — Core/DayNight.cs + Core/SaveSystem.cs
// The Dual Clock heartbeat: game hour, phases, sun control, sky colors.
// Save system: JSON in PlayerPrefs (works on Android + WebGL).
// ============================================================================
using System;
using UnityEngine;

namespace ShadowCity
{
    public class DayNight : MonoBehaviour
    {
        public static DayNight I { get; private set; }

        public float Hour = GameConfig.StartHour;
        public int Day = 1;
        public string Phase = "DUSK";
        public Light Sun;

        // Color script keyed by sun elevation (ported from web sky script)
        static readonly (float e, Color sky, Color fog, Color sun, float sunI, float amb)[] Script =
        {
            (-0.35f, C(0x05,0x08,0x10), C(0x07,0x0b,0x14), C(0x80,0x90,0xb0), 0.05f, 0.18f),
            ( 0.00f, C(0x10,0x1a,0x30), C(0x24,0x1a,0x2c), C(0xff,0x70,0x40), 0.35f, 0.30f),
            ( 0.10f, C(0x2c,0x44,0x70), C(0x58,0x40,0x38), C(0xff,0xb0,0x60), 0.85f, 0.45f),
            ( 0.30f, C(0x3f,0x6a,0x9e), C(0x70,0x64,0x5c), C(0xff,0xd0,0x90), 1.05f, 0.60f),
            ( 1.00f, C(0x56,0x88,0xcc), C(0x98,0xaa,0xbb), C(0xff,0xf8,0xe8), 1.20f, 0.80f),
        };
        static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

        int lastHourInt = -1;

        void Awake() { I = this; }

        public void Tick(float dt)
        {
            float dh = dt * 24f / GameConfig.DayLengthRealSeconds;
            Hour += dh;
            if (Hour >= 24f) { Hour -= 24f; Day++; }

            int hi = Mathf.FloorToInt(Hour);
            if (hi != lastHourInt) { lastHourInt = hi; GameEvents.Emit(GameEvents.HourTick, hi); }

            string ph = PhaseAt(Hour);
            if (ph != Phase) { Phase = ph; GameEvents.Emit(GameEvents.PhaseChanged, ph); }

            ApplyLighting();
        }

        public static string PhaseAt(float h)
        {
            if (h >= 5 && h < 7) return "DAWN";
            if (h >= 7 && h < 17) return "DAY";
            if (h >= 17 && h < 20) return "DUSK";
            return "NIGHT";
        }
        public bool IsDay => Phase == "DAY" || Phase == "DAWN";
        public bool IsNight => Phase == "NIGHT";

        /// <summary>Sun elevation −1..1 with the dusk-bias from the art direction.</summary>
        public float SunElevation()
        {
            float t = (Hour - GameConfig.SunriseHour) / (GameConfig.SunsetHour - GameConfig.SunriseHour);
            if (t >= 0f && t <= 1f)
            {
                float arc = Mathf.Sin(t * Mathf.PI);
                float bias = 1f - 0.35f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.6f) / 0.4f));
                return Mathf.Max(0.02f, arc * bias);
            }
            float nt = Hour > GameConfig.SunsetHour
                ? (Hour - GameConfig.SunsetHour) / (24f - GameConfig.SunsetHour + GameConfig.SunriseHour)
                : (Hour + 24f - GameConfig.SunsetHour) / (24f - GameConfig.SunsetHour + GameConfig.SunriseHour);
            return -0.28f * Mathf.Sin(nt * Mathf.PI) - 0.04f;
        }

        public float Darkness() =>
            Mathf.Clamp01(Mathf.InverseLerp(0.16f, -0.05f, SunElevation()));

        void ApplyLighting()
        {
            float e = SunElevation();
            // sample script
            var lo = Script[0]; var hi2 = Script[^1];
            for (int i = 0; i < Script.Length - 1; i++)
                if (e >= Script[i].e && e <= Script[i + 1].e) { lo = Script[i]; hi2 = Script[i + 1]; break; }
            float t = Mathf.Approximately(hi2.e, lo.e) ? 0 : Mathf.Clamp01((e - lo.e) / (hi2.e - lo.e));

            Color sky = Color.Lerp(lo.sky, hi2.sky, t);
            Color fog = Color.Lerp(lo.fog, hi2.fog, t);
            Color sunC = Color.Lerp(lo.sun, hi2.sun, t);
            float sunI = Mathf.Lerp(lo.sunI, hi2.sunI, t);
            float amb = Mathf.Lerp(lo.amb, hi2.amb, t);

            if (Sun != null)
            {
                float az = Mathf.Lerp(-135f, 135f, Mathf.Clamp01((Hour - GameConfig.SunriseHour) /
                          (GameConfig.SunsetHour - GameConfig.SunriseHour)));
                Sun.transform.rotation = Quaternion.Euler(Mathf.Max(e, -0.1f) * 80f + 8f, az, 0);
                Sun.color = sunC;
                Sun.intensity = Mathf.Max(sunI, 0.02f);
                Sun.shadows = GameConfig.Tier.Shadows && e > 0.03f
                    ? LightShadows.Soft : LightShadows.None;
            }
            RenderSettings.ambientLight = sky * amb + Color.white * 0.06f;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = Mathf.Lerp(0.0035f, 0.0075f, Darkness());
            if (Camera.main != null) Camera.main.backgroundColor = sky;
        }

        [Serializable] public class SaveData { public float hour; public int day; }
        public SaveData Serialize() => new() { hour = Hour, day = Day };
        public void Deserialize(SaveData d) { if (d == null) return; Hour = d.hour; Day = d.day; }
    }

    // ========================================================================
    public static class SaveSystem
    {
        const string KEY = "shadowcity.save.v1";

        [Serializable]
        public class SaveBlob
        {
            public DayNight.SaveData time;
            public float px, py, pz, heading;
            public float health, stamina, focus;
            public int cash, level, xp, skillPoints;
            public int[] skillRanks = new int[6];
            public float[] resonance = new float[5];
            public int storyIndex;
            public bool hasPistol; public int ammo;
            public string lang = "en"; public int qualityTier = 1;
        }

        public static bool Exists => PlayerPrefs.HasKey(KEY);

        public static void Save(SaveBlob blob)
        {
            PlayerPrefs.SetString(KEY, JsonUtility.ToJson(blob));
            PlayerPrefs.Save();
            GameEvents.Emit(GameEvents.Notify, L10N.T("notif.saved"));
        }

        public static SaveBlob Load()
        {
            if (!Exists) return null;
            try { return JsonUtility.FromJson<SaveBlob>(PlayerPrefs.GetString(KEY)); }
            catch { return null; }
        }

        public static void Delete() => PlayerPrefs.DeleteKey(KEY);
    }
}
