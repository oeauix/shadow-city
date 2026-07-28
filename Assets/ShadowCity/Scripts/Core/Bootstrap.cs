// ============================================================================
// SHADOW CITY — Core/Bootstrap.cs
// Composition root: the ONLY component you place in the scene.
// Builds world + systems + UI, owns the game state machine and main loop.
// ============================================================================
using UnityEngine;

namespace ShadowCity
{
    public class Bootstrap : MonoBehaviour
    {
        public static Bootstrap I { get; private set; }

        /// <summary>
        /// Self-bootstrap: the game constructs itself on startup, so scenes
        /// need zero manual wiring (works identically in Editor and CI builds).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (I == null && FindObjectOfType<Bootstrap>() == null)
                new GameObject("Bootstrap").AddComponent<Bootstrap>();
        }
        public string State = "MENU";   // MENU | PLAYING | PAUSED | DEAD | BUSTED
        bool worldBuilt;
        float autosaveT = 180f;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }

        void Start()
        {
            // Camera
            var cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            cam.farClipPlane = GameConfig.Tier.DrawDistance;
            cam.gameObject.AddComponent<ThirdPersonCamera>();

            // Sun
            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;

            // Core systems
            var dn = new GameObject("DayNight").AddComponent<DayNight>();
            dn.Sun = sun;
            AudioManager.Create();
            UIBuilder.Create();

            // Menu-facing world (build immediately so the menu shows the city)
            BuildWorld();
            PositionMenuCamera();

            GameEvents.On(GameEvents.PlayerDied, _ =>
            { State = "DEAD"; UIBuilder.I.ShowDead(true, false); });
            GameEvents.On(GameEvents.PlayerBusted, _ =>
            {
                State = "BUSTED";
                Economy.Spend(Mathf.Min(Economy.Cash, GameConfig.BustedFine));
                UIBuilder.I.ShowDead(true, true);
            });
        }

        void BuildWorld()
        {
            if (worldBuilt) return;
            worldBuilt = true;
            var plan = CityGenerator.Generate(GameConfig.Seed);
            var cityGo = new GameObject("City");
            var builder = cityGo.AddComponent<CityBuilder>();
            builder.Build(plan);

            Resonance.Init();
            RPG.Init();
            Economy.Init(plan);
            Missions.Init();
            PoliceSystem.Init();
            PedestrianSystem.Init();
            TrafficSystem.Init(plan);
            GameEvents.On(GameEvents.VehicleStolen, v => TrafficSystem.ReleaseToPlayer((Vehicle)v));
        }

        void PositionMenuCamera()
        {
            float s = GameConfig.WorldSize;
            var cam = Camera.main.transform;
            cam.position = new Vector3(s * 0.3f, 70, s * 0.3f);
            cam.LookAt(new Vector3(s * 0.55f, 10, s * 0.6f));
        }

        // ------------------------------- FLOWS ----------------------------------
        public void NewGame()
        {
            SaveSystem.Delete();
            StartSession(null);
        }

        public void ContinueGame() => StartSession(SaveSystem.Load());

        void StartSession(SaveSystem.SaveBlob blob)
        {
            var plan = CityGenerator.Plan;
            bool isNewGame = blob == null;
            if (PlayerController.I == null)
                PlayerController.Spawn(plan.PlayerSpawn);

            var p = PlayerController.I;
            if (blob != null)
            {
                p.transform.position = new Vector3(blob.px, blob.py + 0.5f, blob.pz);
                p.Heading = blob.heading;
                p.Health = blob.health; p.Stamina = blob.stamina; p.Focus = blob.focus;
                p.HasPistol = blob.hasPistol; p.Ammo = blob.ammo;
                Economy.Cash = blob.cash;
                RPG.Level = blob.level; RPG.XP = blob.xp; RPG.SkillPoints = blob.skillPoints;
                for (int i = 0; i < 6; i++) RPG.Ranks[i] = blob.skillRanks[i];
                Resonance.Deserialize(blob.resonance);
                Missions.StoryIndex = blob.storyIndex;
                DayNight.I.Deserialize(blob.time);
                L10N.SetLanguage(blob.lang);
                GameConfig.CurrentTier = blob.qualityTier;
            }
            else
            {
                p.Respawn(plan.PlayerSpawn);
                DayNight.I.Hour = GameConfig.StartHour; DayNight.I.Day = 1;
                GameEvents.Emit(GameEvents.Subtitle, L10N.T("hud.pulseHint"));
            }

            State = "PLAYING";
            UIBuilder.I.ShowMenu(false);
            if (!GameInput.IsMobile) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

            // opening cinematic on a fresh start (web-build parity)
            if (isNewGame) Cinematic.Begin();
        }

        public void SaveGame()
        {
            var p = PlayerController.I;
            if (p == null) return;
            var blob = new SaveSystem.SaveBlob
            {
                time = DayNight.I.Serialize(),
                px = p.transform.position.x, py = p.transform.position.y, pz = p.transform.position.z,
                heading = p.Heading,
                health = p.Health, stamina = p.Stamina, focus = p.Focus,
                cash = Economy.Cash,
                level = RPG.Level, xp = RPG.XP, skillPoints = RPG.SkillPoints,
                storyIndex = Missions.StoryIndex,
                hasPistol = p.HasPistol, ammo = p.Ammo,
                lang = L10N.Lang, qualityTier = GameConfig.CurrentTier,
                resonance = Resonance.Serialize(),
            };
            for (int i = 0; i < 6; i++) blob.skillRanks[i] = RPG.Ranks[i];
            SaveSystem.Save(blob);
        }

        public void Pause()
        {
            if (State != "PLAYING") return;
            State = "PAUSED";
            UIBuilder.I.ShowPause(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void PauseForMenu()
        {
            if (State != "PLAYING") return;
            State = "PAUSED";
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void Resume()
        {
            State = "PLAYING";
            UIBuilder.I.ShowPause(false);
            UIBuilder.I.ShowSkills(false);
            if (!GameInput.IsMobile) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        public void ToMenu()
        {
            SaveGame();
            State = "MENU";
            UIBuilder.I.ShowPause(false);
            UIBuilder.I.ShowMenu(true);
            PositionMenuCamera();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void Respawn()
        {
            bool busted = State == "BUSTED";
            if (!busted) Economy.Spend(Mathf.Min(Economy.Cash, GameConfig.HospitalFee));
            if (busted) { PlayerController.I.HasPistol = false; PlayerController.I.Ammo = 0; }
            PlayerController.I.Respawn(CityGenerator.Plan.PlayerSpawn);
            PoliceSystem.SetStars(0);
            UIBuilder.I.ShowDead(false, false);
            State = "PLAYING";
        }

        // ------------------------------ MAIN LOOP --------------------------------
        void Update()
        {
            GameInput.Frame();
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == "PLAYING") Pause();
                else if (State == "PAUSED") Resume();
            }

            // World always breathes (menu cinematic too)
            DayNight.I.Tick(State == "PLAYING" ? dt : dt * 0.25f);
            CityBuilder.I.Tick();

            if (State == "CINEMATIC")
            {
                Cinematic.Tick(dt);
                TrafficSystem.Tick(dt);
                PedestrianSystem.Tick(dt);
            }
            else if (State == "PLAYING")
            {
                PlayerController.I?.Tick(dt);
                TrafficSystem.Tick(dt);
                PedestrianSystem.Tick(dt);
                PoliceSystem.Tick(dt);
                Pulse.Tick(dt);
                Missions.Tick(dt);

                autosaveT -= dt;
                if (autosaveT <= 0) { autosaveT = 180f; SaveGame(); }
            }
            else if (State == "MENU")
            {
                // slow cinematic orbit
                float s = GameConfig.WorldSize;
                float t = Time.time * 0.03f;
                var cam = Camera.main.transform;
                cam.position = new Vector3(
                    s * 0.5f + Mathf.Cos(t) * s * 0.28f, 65f + Mathf.Sin(t * 0.7f) * 12f,
                    s * 0.55f + Mathf.Sin(t) * s * 0.28f);
                cam.LookAt(new Vector3(s * 0.5f, 12, s * 0.6f));
            }

            UIBuilder.I?.TickHUD();
        }

        void LateUpdate()
        {
            if (State == "PLAYING")
                ThirdPersonCamera.I?.Tick(Mathf.Min(Time.deltaTime, 0.05f));
        }
    }
}
