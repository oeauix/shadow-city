// ============================================================================
// SHADOW CITY — Audio/AudioManager.cs
// Plays ElevenLabs clips from Resources/Audio + the generative music engine
// ported from the web build (day pads / night arps, fear-detune) rendered
// via OnAudioFilterRead — zero music assets needed.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace ShadowCity
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I { get; private set; }

        readonly Dictionary<string, AudioClip> clips = new();
        AudioSource sfx, engine, siren, rain, ambience, dialog;
        MusicSynth music;

        public static AudioManager Create()
        {
            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            var am = go.AddComponent<AudioManager>();
            return am;
        }

        void Awake()
        {
            I = this;
            foreach (var clip in Resources.LoadAll<AudioClip>("Audio"))
                clips[clip.name] = clip;

            AudioSource Mk(bool loop, float vol)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.loop = loop; s.volume = vol; s.playOnAwake = false;
                return s;
            }
            sfx = Mk(false, 0.9f);
            dialog = Mk(false, 1f);
            engine = Mk(true, 0f);
            siren = Mk(true, 0f);
            rain = Mk(true, 0f);
            ambience = Mk(true, 0.35f);

            if (clips.TryGetValue("sfx_engine", out var ec)) engine.clip = ec;
            if (clips.TryGetValue("sfx_siren", out var sc)) siren.clip = sc;
            if (clips.TryGetValue("sfx_rain", out var rc)) rain.clip = rc;
            if (clips.TryGetValue("sfx_ambience", out var ac)) { ambience.clip = ac; ambience.Play(); }

            var mgo = new GameObject("MusicSynth");
            mgo.transform.SetParent(transform);
            mgo.AddComponent<AudioSource>();          // required host for the filter
            music = mgo.AddComponent<MusicSynth>();
        }

        public void Play(string name, float vol = 1f)
        {
            if (clips.TryGetValue(name, out var c)) sfx.PlayOneShot(c, vol);
        }

        public void PlayDialog(string baseName)
        {
            string name = baseName + "_" + L10N.Lang;
            if (!clips.ContainsKey(name)) name = baseName + "_en";
            if (clips.TryGetValue(name, out var c)) dialog.PlayOneShot(c, 1f);
        }

        public void EngineStart() { if (engine.clip != null && !engine.isPlaying) engine.Play(); }
        public void EngineStop() { engine.Stop(); }
        public void SirenStart() { if (siren.clip != null && !siren.isPlaying) { siren.volume = 0.4f; siren.Play(); } }
        public void SirenStop() { siren.Stop(); }

        void Update()
        {
            var p = PlayerController.I;
            if (p != null && p.CurrentVehicle != null && engine.isPlaying)
            {
                float f = Mathf.Clamp01(Mathf.Abs(p.CurrentVehicle.Speed) / p.CurrentVehicle.Def.TopSpeed);
                engine.pitch = 0.8f + f * 1.1f;
                engine.volume = 0.25f + f * 0.3f;
            }
            // night ambience swells at night
            if (DayNight.I != null)
                ambience.volume = 0.15f + DayNight.I.Darkness() * 0.3f;
        }
    }

    // ========================================================================
    /// <summary>
    /// Generative music: warm pads by day, dark arpeggios by night.
    /// Fear resonance detunes the music (nothing is cosmetic).
    /// Pure DSP in OnAudioFilterRead — free and adaptive.
    /// </summary>
    public class MusicSynth : MonoBehaviour
    {
        static readonly float[] DayScale = { 261.6f, 293.7f, 329.6f, 392.0f, 440.0f, 523.3f };
        static readonly float[] NightScale = { 220.0f, 246.9f, 277.2f, 329.6f, 370.0f, 440.0f };

        class Voice { public double phase, freq; public float env, dur, t; }
        readonly List<Voice> voices = new();
        readonly object voiceLock = new();
        double sampleRate;
        float stepTimer; int step;
        float nightBlend, detune;
        System.Random rnd = new(1);

        void Start() { sampleRate = AudioSettings.outputSampleRate; }

        void Update()
        {
            bool night = DayNight.I != null && (DayNight.I.IsNight || DayNight.I.Phase == "DUSK");
            nightBlend = Mathf.MoveTowards(nightBlend, night ? 1 : 0, Time.deltaTime / 6f);

            float targetDetune = 0;
            if (PlayerController.I != null)
            {
                float res = Resonance.Get(PlayerController.I.District);
                if (res < 0) targetDetune = -res / 100f * 0.02f;   // up to 2% flat
            }
            detune = Mathf.Lerp(detune, targetDetune, Time.deltaTime);

            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0)
            {
                stepTimer = Mathf.Lerp(0.9f, 0.42f, nightBlend);
                step++;
                var scale = nightBlend > 0.5f ? NightScale : DayScale;
                float baseF = scale[step * 2 % scale.Length] * (nightBlend > 0.5f ? 0.5f : 1f);
                baseF *= 1f - detune;

                lock (voiceLock)
                {
                    if (nightBlend > 0.5f)
                    {
                        voices.Add(new Voice { freq = baseF, dur = 0.5f });
                        if (step % 4 == 0) voices.Add(new Voice { freq = baseF * 0.5f, dur = 1.8f });
                    }
                    else if (step % 2 == 0)
                    {
                        voices.Add(new Voice { freq = baseF, dur = 2.6f });
                        voices.Add(new Voice { freq = baseF * 1.5f, dur = 2.6f });
                    }
                    if (voices.Count > 10) voices.RemoveAt(0);
                }
            }
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            lock (voiceLock)
            {
                for (int i = 0; i < data.Length; i += channels)
                {
                    float sample = 0;
                    for (int v = voices.Count - 1; v >= 0; v--)
                    {
                        var vo = voices[v];
                        vo.t += (float)(1.0 / sampleRate);
                        if (vo.t >= vo.dur) { voices.RemoveAt(v); continue; }
                        // ADSR-ish: quick attack, slow release
                        float env = Mathf.Min(vo.t * 8f, 1f) * (1f - vo.t / vo.dur);
                        vo.phase += vo.freq / sampleRate;
                        // triangle-ish (soft) oscillator
                        float ph = (float)(vo.phase - System.Math.Floor(vo.phase));
                        float tri = 4f * Mathf.Abs(ph - 0.5f) - 1f;
                        sample += tri * env * 0.055f;
                    }
                    for (int c = 0; c < channels; c++) data[i + c] += sample;
                }
            }
        }
    }
}
