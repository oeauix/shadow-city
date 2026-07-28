# SHADOW CITY → Unity — Phase 1 Delivery Report / گزارش تحویل فاز ۱

## What was executed (this session)

### 1. API budget (free plans — priority order enforced)
**ElevenLabs (10k free credits):** spent ~3,345 · reserve ~6,655 ✅
- ✅ P0: **15 core SFX** generated (gunshot, explosion, crash, siren-loop, rain-loop, thunder, footstep, engine-loop, pulse, pickup, click, level-up, horn, night-ambience)
- ✅ P1: **6 story dialogue lines (EN, George voice)**
- ✅ P2: **3 story lines in Persian (eleven_v3)** — فارسی واقعی کار کرد!
- ⏭ Music API skipped (paid-only) → replaced by a **generative music synth ported from the web build** (day pads / night arps / fear-detune) — zero assets, fully adaptive
**Tripo:** balance = **0 credits** → nothing spendable. Delivered instead:
- Editor window **Shadow City → Tripo Importer**: balance check, curated 14-prompt pack (consistent "neon dusk low-poly" style), generate→poll→download GLB, cost shown before every call
- Game runs 100% on procedural visuals until top-up; models swap in without code changes

### 2. Unity project (18 C# files, ~4,000 lines, all balance from the 840-assertion web build)
Core: GameConfig · GameEvents · GameInput (keyboard+touch unified) · **PersianShaper**
(full contextual joining + lam-alef + RTL bidi + ۰-۹ digits) · L10N (EN/FA table) ·
DayNight (dusk-bias sun script) · SaveSystem (PlayerPrefs JSON) · Bootstrap (state machine)
World: CityGenerator (seeded deterministic plan, 5 districts) · CityBuilder (merged meshes,
**emissive windows tinted by Resonance**, street lamps, parks, colliders)
Entities: CharacterRig (procedural gait) · PlayerController + ThirdPersonCamera (occlusion boom,
FOV kick) · Vehicle (arcade physics + theft + damage/explosion) · TrafficSystem ·
Pedestrian (flee/greet by resonance) · PoliceSystem (**last-known-position pursuit**, 5★, bust)
Systems: Resonance (decay, price factor, neon binding) · Pulse (ring + reveal) · RPG
(6 skills, dual-clock XP bonus) · Economy · Missions (7 types + S1 story, full state machine)
Audio: AudioManager (ElevenLabs clips) + MusicSynth (OnAudioFilterRead DSP)
UI: UIBuilder (runtime UGUI: menu/HUD/pause/shop/skills/death, bilingual RTL) + TouchControls
(virtual joystick + 5 action buttons, auto-enabled on mobile)
Editor: ProjectBootstrap (one-click scene) · TripoImporter · Android config

### 3. Verification (no Unity needed — same discipline as the web build)
| Gate | Result |
|---|---|
| A — Compile (runtime scripts vs full API-surface stubs) | ✅ Build succeeded |
| A2 — Compile (editor scripts, UNITY_EDITOR define) | ✅ Build succeeded |
| B — Logic tests (RNG determinism, XP curve, phases, citygen determinism + 5 districts + spawn-on-strip, **Persian shaping incl. lam-alef ligature**, L10N parity, resonance clamp/decay/round-trip, RPG level-ups/caps, pulse cost formula) | ✅ **43/43** |
| D-lite — Init-order simulation (crime→stars, hourly decay, pricing fallback) | ✅ SIM OK |
| Audio assets | ✅ 23 MP3s verified in Resources/Audio |

Two defects found & fixed by the tests: stub recursion (test infra) and a
test expectation corrected for the lam-alef ligature (shaper output is 3 glyphs
for «سلام» — correct behavior, test was wrong).

## Your part (~5 minutes)
Open `IMPORT-GUIDE.md` inside the zip: create URP project → copy folder →
move font into Resources → menu **Shadow City → Setup Scene** → ▶ Play.
Then send back: screenshots (menu + day + night), any Console messages, FPS, feel notes.

## Phase 2 (after your feedback)
Tripo hero assets (on top-up) · weather/rain · minimap · gangs/side-jobs/shards ports ·
Tapsell/AdMob rewarded ads · Bazaar/Myket build via the Android config menu.
