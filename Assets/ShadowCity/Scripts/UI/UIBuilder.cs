// ============================================================================
// SHADOW CITY — UI/UIBuilder.cs
// Runtime-built UGUI: main menu, HUD (vitals/cash/clock/stars/mission),
// pause, shop, skills, death screens, notifications — bilingual + RTL,
// plus the mobile touch overlay. No prefabs; everything constructed in code.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace ShadowCity
{
    public class UIBuilder : MonoBehaviour
    {
        public static UIBuilder I { get; private set; }
        public Font Fa, En;
        Canvas canvas;

        // HUD refs
        Text cash, clock, level, wanted, district, missionLine, prompt, subtitle, speed;
        Image healthBar, staminaBar, focusBar, xpBar;
        GameObject hudRoot, menuRoot, pauseRoot, shopRoot, skillsRoot, deadRoot;
        readonly List<(Text t, float until)> notes = new();
        GameObject notesRoot;

        public static UIBuilder Create()
        {
            var go = new GameObject("UI");
            var ui = go.AddComponent<UIBuilder>();
            return ui;
        }

        void Awake()
        {
            I = this;
            Fa = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var custom = Resources.Load<Font>("Vazirmatn-Regular");
            En = Fa;
            if (custom != null) Fa = custom;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            BuildHUD();
            BuildMenus();
            BuildTouchControls();
            ShowMenu(true);

            GameEvents.On(GameEvents.Notify, msg => Notify((string)msg));
            GameEvents.On(GameEvents.Subtitle, msg => { subtitle.text = (string)msg; subtitleUntil = Time.time + 5f; });
            GameEvents.On(GameEvents.LanguageChanged, _ => RefreshLabels());
        }

        // ------------------------------ HELPERS --------------------------------
        Font FontFor() => L10N.IsRTL ? Fa : En;

        RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                           Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        Text Label(string name, Transform parent, Vector2 aMin, Vector2 aMax,
                   Vector2 oMin, Vector2 oMax, int size, TextAnchor align, Color col)
        {
            var rt = Rect(name, parent, aMin, aMax, oMin, oMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = FontFor(); t.fontSize = size; t.alignment = align; t.color = col;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Image Panel(string name, Transform parent, Vector2 aMin, Vector2 aMax,
                    Vector2 oMin, Vector2 oMax, Color col)
        {
            var rt = Rect(name, parent, aMin, aMax, oMin, oMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = col;
            return img;
        }

        Button Btn(string name, Transform parent, Vector2 aMin, Vector2 aMax,
                   Vector2 oMin, Vector2 oMax, string text, System.Action onClick, int fontSize = 22)
        {
            var img = Panel(name, parent, aMin, aMax, oMin, oMax, new Color(0.12f, 0.16f, 0.28f, 0.92f));
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var t = Label(name + "_t", img.transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleCenter, Color.white);
            t.text = text;
            b.onClick.AddListener(() => { AudioManager.I?.Play("sfx_click", 0.6f); onClick(); });
            return b;
        }

        Image Bar(string name, Transform parent, float y, Color col)
        {
            Panel(name + "_bg", parent, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(20, y), new Vector2(240, y + 12), new Color(0, 0, 0, 0.55f));
            var fill = Panel(name + "_fill", parent, new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(21, y + 1), new Vector2(239, y + 11), col);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            return fill;
        }

        // -------------------------------- HUD -----------------------------------
        float subtitleUntil;

        void BuildHUD()
        {
            hudRoot = Rect("HUD", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;

            healthBar = Bar("health", hudRoot.transform, 66, new Color(0.18f, 0.86f, 0.45f));
            staminaBar = Bar("stamina", hudRoot.transform, 48, new Color(1f, 0.79f, 0.25f));
            focusBar = Bar("focus", hudRoot.transform, 30, new Color(0.21f, 0.88f, 1f));
            xpBar = Bar("xp", hudRoot.transform, 12, new Color(0.73f, 0.42f, 1f));

            cash = Label("cash", hudRoot.transform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-260, -50), new Vector2(-20, -12), 26, TextAnchor.UpperRight,
                new Color(0.55f, 1f, 0.69f));
            level = Label("level", hudRoot.transform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-260, -80), new Vector2(-20, -52), 17, TextAnchor.UpperRight,
                new Color(0.73f, 0.78f, 0.87f));
            clock = Label("clock", hudRoot.transform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-260, -108), new Vector2(-20, -82), 16, TextAnchor.UpperRight,
                new Color(0.62f, 0.7f, 0.8f));
            wanted = Label("wanted", hudRoot.transform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-260, -140), new Vector2(-20, -108), 22, TextAnchor.UpperRight,
                new Color(1f, 0.3f, 0.37f));
            district = Label("district", hudRoot.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -46), new Vector2(320, -14), 18, TextAnchor.UpperLeft,
                new Color(0.81f, 0.85f, 0.9f));
            missionLine = Label("mission", hudRoot.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(20, -84), new Vector2(560, -50), 16, TextAnchor.UpperLeft,
                new Color(1f, 0.87f, 0.5f));
            speed = Label("speed", hudRoot.transform, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-240, 18), new Vector2(-20, 52), 22, TextAnchor.LowerRight,
                new Color(0.49f, 0.9f, 1f));
            prompt = Label("prompt", hudRoot.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-260, 150), new Vector2(260, 186), 19, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.95f, 1f));
            subtitle = Label("subtitle", hudRoot.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-360, 70), new Vector2(360, 110), 18, TextAnchor.MiddleCenter, Color.white);

            notesRoot = Rect("Notes", hudRoot.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
                new Vector2(-330, -60), new Vector2(-16, 180)).gameObject;

            Minimap.Create(hudRoot.transform);

            // --- cinematic overlay: letterbox bars + caption (hidden by default)
            cineRoot = Rect("Cinematic", transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero).gameObject;
            Panel("barTop", cineRoot.transform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -90), new Vector2(0, 0), Color.black);
            Panel("barBot", cineRoot.transform, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 0), new Vector2(0, 90), Color.black);
            cineCaption = Label("cineCap", cineRoot.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-460, 100), new Vector2(460, 150), 24, TextAnchor.MiddleCenter,
                new Color(0.92f, 0.96f, 1f));
            var skipHint = Label("cineSkip", cineRoot.transform, new Vector2(1, 0), new Vector2(1, 0),
                new Vector2(-260, 24), new Vector2(-20, 56), 14, TextAnchor.MiddleRight,
                new Color(1, 1, 1, 0.45f));
            skipHint.name = "cine_skip";
            cineRoot.SetActive(false);
        }

        // --------------------------- CINEMATIC API -----------------------------
        GameObject cineRoot;
        Text cineCaption;

        public void ShowCinematic(bool on)
        {
            cineRoot.SetActive(on);
            hudRoot.SetActive(!on);
            touchRoot.SetActive(!on && GameInput.IsMobile);
            if (on)
            {
                var sk = cineRoot.transform.Find("cine_skip");
                if (sk != null)
                {
                    var t = sk.GetComponent<Text>();
                    t.font = FontFor();
                    t.text = L10N.T("cine.skip");
                }
            }
        }

        public void CineText(string line)
        {
            cineCaption.font = FontFor();
            cineCaption.text = line;
        }

        public void Notify(string msg)
        {
            var t = Label("note", notesRoot.transform, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -26 * (notes.Count + 1)), new Vector2(0, -26 * notes.Count),
                15, TextAnchor.MiddleRight, new Color(1, 1, 1, 0.95f));
            t.text = msg;
            notes.Add((t, Time.time + 4.5f));
        }

        // ------------------------------- MENUS ----------------------------------
        void BuildMenus()
        {
            // MAIN MENU
            menuRoot = Panel("Menu", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.07f, 0.94f)).gameObject;
            var title = Label("title", menuRoot.transform, new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.72f),
                new Vector2(-400, -50), new Vector2(400, 50), 54, TextAnchor.MiddleCenter,
                new Color(0.49f, 0.91f, 1f));
            title.name = "menu_title";
            var subt = Label("subtitle", menuRoot.transform, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f),
                new Vector2(-400, -20), new Vector2(400, 20), 17, TextAnchor.MiddleCenter,
                new Color(0.56f, 0.64f, 0.75f));
            subt.name = "menu_subtitle";

            Btn("new", menuRoot.transform, new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.47f),
                new Vector2(-140, -25), new Vector2(140, 25), "", () => Bootstrap.I.NewGame()).name = "btn_new";
            Btn("cont", menuRoot.transform, new Vector2(0.5f, 0.37f), new Vector2(0.5f, 0.37f),
                new Vector2(-140, -25), new Vector2(140, 25), "", () => Bootstrap.I.ContinueGame()).name = "btn_cont";
            Btn("lang", menuRoot.transform, new Vector2(0.5f, 0.27f), new Vector2(0.5f, 0.27f),
                new Vector2(-140, -25), new Vector2(140, 25), "", () =>
                { L10N.SetLanguage(L10N.Lang == "en" ? "fa" : "en"); }).name = "btn_lang";
            Btn("quality", menuRoot.transform, new Vector2(0.5f, 0.17f), new Vector2(0.5f, 0.17f),
                new Vector2(-140, -25), new Vector2(140, 25), "", () =>
                {
                    GameConfig.CurrentTier = (GameConfig.CurrentTier + 1) % GameConfig.Tiers.Length;
                    RefreshLabels();
                }).name = "btn_quality";

            // PAUSE
            pauseRoot = Panel("Pause", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.03f, 0.07f, 0.85f)).gameObject;
            Label("ptitle", pauseRoot.transform, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f),
                new Vector2(-200, -30), new Vector2(200, 30), 40, TextAnchor.MiddleCenter, Color.white)
                .name = "pause_title";
            Btn("resume", pauseRoot.transform, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f),
                new Vector2(-140, -25), new Vector2(140, 25), "", () => Bootstrap.I.Resume()).name = "btn_resume";
            Btn("skills", pauseRoot.transform, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f),
                new Vector2(-140, -25), new Vector2(140, 25), "", () => ShowSkills(true)).name = "btn_skills";
            Btn("save", pauseRoot.transform, new Vector2(0.5f, 0.32f), new Vector2(0.5f, 0.32f),
                new Vector2(-140, -25), new Vector2(140, 25), "", () => Bootstrap.I.SaveGame()).name = "btn_save";
            Btn("quit", pauseRoot.transform, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f),
                new Vector2(-140, -25), new Vector2(140, 25), "", () => Bootstrap.I.ToMenu()).name = "btn_quit";

            // SHOP
            shopRoot = Panel("Shop", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260, -220), new Vector2(260, 220), new Color(0.04f, 0.06f, 0.12f, 0.96f)).gameObject;
            BuildShopContents();

            // SKILLS
            skillsRoot = Panel("Skills", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-330, -250), new Vector2(330, 250), new Color(0.04f, 0.06f, 0.12f, 0.96f)).gameObject;
            BuildSkillContents();

            // DEAD / BUSTED
            deadRoot = Panel("Dead", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.1f, 0.01f, 0.02f, 0.88f)).gameObject;
            Label("dtitle", deadRoot.transform, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f),
                new Vector2(-300, -40), new Vector2(300, 40), 52, TextAnchor.MiddleCenter,
                new Color(1f, 0.3f, 0.37f)).name = "dead_title";
            Btn("respawn", deadRoot.transform, new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.4f),
                new Vector2(-140, -25), new Vector2(140, 25), "", () => Bootstrap.I.Respawn()).name = "btn_respawn";

            pauseRoot.SetActive(false); shopRoot.SetActive(false);
            skillsRoot.SetActive(false); deadRoot.SetActive(false);
            RefreshLabels();
        }

        void BuildShopContents()
        {
            foreach (Transform c in shopRoot.transform) Destroy(c.gameObject);
            Label("stitle", shopRoot.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-200, -50), new Vector2(200, -12), 26, TextAnchor.MiddleCenter, Color.white)
                .text = "🏪";
            int y = -80;
            void Item(string labelKey, int basePrice, System.Action buy)
            {
                int price = Economy.Price(basePrice);
                Btn("item", shopRoot.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(-210, y - 44), new Vector2(210, y),
                    L10N.T(labelKey, L10N.Money(price)),
                    () => { if (Economy.Spend(price)) { buy(); BuildShopContents(); } }, 18);
                y -= 54;
            }
            Item("shop.medkit", 80, () => PlayerController.I.Health =
                Mathf.Min(GameConfig.MaxHealth, PlayerController.I.Health + 60));
            Item("shop.pistol", 450, () =>
                { PlayerController.I.HasPistol = true; PlayerController.I.Ammo += 12; });
            Item("shop.ammo", 40, () => PlayerController.I.Ammo += 24);
            Btn("close", shopRoot.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-100, 14), new Vector2(100, 58), L10N.T("shop.close"),
                () => { shopRoot.SetActive(false); Bootstrap.I.Resume(); }, 18);
        }

        void BuildSkillContents()
        {
            foreach (Transform c in skillsRoot.transform) Destroy(c.gameObject);
            Label("ktitle", skillsRoot.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-300, -46), new Vector2(300, -10), 24, TextAnchor.MiddleCenter, Color.white)
                .text = L10N.T("rpg.skills") + " — " + L10N.T("rpg.points", RPG.SkillPoints);
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                float col = i < 3 ? -1 : 1;
                int row = i % 3;
                string label = L10N.T(RPG.SkillKeys[i]) + "  " +
                    new string('●', RPG.Rank(i)) + new string('○', GameConfig.SkillMaxRank - RPG.Rank(i));
                Btn("sk" + i, skillsRoot.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                    new Vector2(col < 0 ? -310 : 15, -120 - row * 60 - 44),
                    new Vector2(col < 0 ? -15 : 310, -120 - row * 60),
                    label, () => { if (RPG.Upgrade(idx)) BuildSkillContents(); }, 17);
            }
            Btn("kclose", skillsRoot.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-100, 14), new Vector2(100, 56), L10N.T("shop.close"),
                () => ShowSkills(false), 18);
        }

        // --------------------------- TOUCH CONTROLS ------------------------------
        GameObject touchRoot;

        void BuildTouchControls()
        {
            touchRoot = Rect("Touch", transform, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero).gameObject;
            touchRoot.AddComponent<TouchControls>().Build(this);
            touchRoot.SetActive(GameInput.IsMobile);
        }

        // ------------------------------ VISIBILITY -------------------------------
        public void ShowMenu(bool on)
        {
            menuRoot.SetActive(on);
            hudRoot.SetActive(!on);
            touchRoot.SetActive(!on && GameInput.IsMobile);
            if (on) RefreshLabels();
        }
        public void ShowPause(bool on) { pauseRoot.SetActive(on); if (on) RefreshLabels(); }
        public void OpenShop() { Bootstrap.I.PauseForMenu(); BuildShopContents(); shopRoot.SetActive(true); }
        public void ShowSkills(bool on) { if (on) BuildSkillContents(); skillsRoot.SetActive(on); }
        public void ShowDead(bool on, bool busted)
        {
            deadRoot.SetActive(on);
            if (on)
                deadRoot.transform.Find("dead_title").GetComponent<Text>().text =
                    L10N.T(busted ? "police.busted" : "police.wasted");
        }

        void SetBtn(GameObject root, string name, string text)
        {
            // buttons are renamed after creation, so find by the button's final
            // name and grab its Text child (label child keeps its original name)
            var tr = root.transform.Find(name);
            if (tr == null) return;
            var t = tr.GetComponentInChildren<Text>(true);
            if (t != null) { t.text = text; t.font = FontFor(); }
        }

        public void RefreshLabels()
        {
            var f = FontFor();
            foreach (var t in GetComponentsInChildren<Text>(true)) t.font = f;

            menuRoot.transform.Find("menu_title").GetComponent<Text>().text = L10N.T("game.title");
            menuRoot.transform.Find("menu_subtitle").GetComponent<Text>().text = L10N.T("game.subtitle");
            SetBtn(menuRoot, "btn_new", L10N.T("menu.newGame"));
            SetBtn(menuRoot, "btn_cont", L10N.T("menu.continue") + (SaveSystem.Exists ? "" : " —"));
            SetBtn(menuRoot, "btn_lang", L10N.T("menu.language"));
            SetBtn(menuRoot, "btn_quality", L10N.T("menu.quality", GameConfig.Tier.Name));
            pauseRoot.transform.Find("pause_title").GetComponent<Text>().text = L10N.T("menu.paused");
            SetBtn(pauseRoot, "btn_resume", L10N.T("menu.resume"));
            SetBtn(pauseRoot, "btn_skills", L10N.T("rpg.skills"));
            SetBtn(pauseRoot, "btn_save", L10N.T("menu.save"));
            SetBtn(pauseRoot, "btn_quit", L10N.T("menu.quit"));
            SetBtn(deadRoot, "btn_respawn", L10N.T("police.respawn"));
        }

        // -------------------------------- UPDATE ---------------------------------
        public void TickHUD()
        {
            var p = PlayerController.I;
            if (p == null || !hudRoot.activeSelf) return;

            healthBar.fillAmount = p.Health / GameConfig.MaxHealth;
            staminaBar.fillAmount = p.Stamina / GameConfig.MaxStamina;
            focusBar.fillAmount = p.Focus / GameConfig.MaxFocus;
            xpBar.fillAmount = RPG.Level >= GameConfig.MaxLevel ? 1f
                : (float)RPG.XP / GameConfig.XPForLevel(RPG.Level);

            cash.text = L10N.Money(Economy.Cash);
            level.text = L10N.T("hud.level", RPG.Level);
            int hh = Mathf.FloorToInt(DayNight.I.Hour);
            int mm = Mathf.FloorToInt((DayNight.I.Hour - hh) * 60);
            string clockS = $"{hh:00}:{mm:00}";
            if (L10N.IsRTL) clockS = PersianShaper.ToPersianDigits(clockS);
            clock.text = clockS + "  " + L10N.T("phase." + DayNight.I.Phase) +
                         "  ·  " + L10N.T("hud.day", DayNight.I.Day);

            wanted.text = PoliceSystem.Stars > 0
                ? new string('★', PoliceSystem.Stars) + new string('☆', 5 - PoliceSystem.Stars) : "";

            if (p.District != "")
            {
                int res = Mathf.RoundToInt(Resonance.Get(p.District));
                district.text = L10N.T("district." + p.District) +
                    (res != 0 ? $"  {(res > 0 ? "+" : "")}{res}" : "");
                district.color = res > 20 ? new Color(1f, 0.77f, 0.4f)
                    : res < -20 ? new Color(1f, 0.31f, 0.4f) : new Color(0.81f, 0.85f, 0.9f);
            }

            missionLine.text = Missions.HudLine() ?? "";
            prompt.text = p.InteractLabel;
            speed.text = p.CurrentVehicle != null
                ? L10N.T("hud.speed", p.CurrentVehicle.SpeedKmh) : "";

            Minimap.I?.Tick();

            if (Time.time > subtitleUntil) subtitle.text = "";

            for (int i = notes.Count - 1; i >= 0; i--)
                if (Time.time > notes[i].until)
                { Destroy(notes[i].t.gameObject); notes.RemoveAt(i); }
        }
    }

    // ========================================================================
    /// <summary>Virtual joystick + action buttons; writes into GameInput.</summary>
    public class TouchControls : MonoBehaviour
    {
        RectTransform stickBg, stickKnob;
        int stickFinger = -1, lookFinger = -1;
        Vector2 stickCenter, lastLook;

        public void Build(UIBuilder ui)
        {
            Image MkPanel(string n, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, Color c)
            {
                var go = new GameObject(n, typeof(RectTransform));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(transform, false);
                rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
                var img = go.AddComponent<Image>(); img.color = c;
                return img;
            }

            stickBg = MkPanel("stickBg", new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(40, 40), new Vector2(220, 220),
                new Color(1, 1, 1, 0.07f)).rectTransform;
            stickKnob = MkPanel("stickKnob", new Vector2(0, 0), new Vector2(0, 0),
                new Vector2(105, 105), new Vector2(155, 155),
                new Color(1, 1, 1, 0.22f)).rectTransform;

            void ActionBtn(string label, Vector2 pos, System.Action<bool> setter)
            {
                var img = MkPanel("btn" + label, new Vector2(1, 0), new Vector2(1, 0),
                    pos, pos + new Vector2(84, 84), new Color(1, 1, 1, 0.1f));
                var t = new GameObject("t", typeof(RectTransform)).AddComponent<Text>();
                t.transform.SetParent(img.transform, false);
                var rt = t.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                t.font = ui.En; t.fontSize = 26; t.alignment = TextAnchor.MiddleCenter;
                t.color = new Color(1, 1, 1, 0.85f); t.text = label;
                var et = img.gameObject.AddComponent<EventTrigger>();
                void Add(EventTriggerType type, bool v)
                {
                    var e = new EventTrigger.Entry { eventID = type };
                    e.callback.AddListener(_ => setter(v));
                    et.triggers.Add(e);
                }
                Add(EventTriggerType.PointerDown, true);
                Add(EventTriggerType.PointerUp, false);
            }

            ActionBtn("E", new Vector2(-118, 208), v => GameInput.TouchInteract = v);
            ActionBtn("Q", new Vector2(-208, 124), v => GameInput.TouchPulse = v);
            ActionBtn("⇧", new Vector2(-208, 30), v => GameInput.TouchSprint = v);
            ActionBtn("▲", new Vector2(-118, 30), v => { GameInput.TouchJump = v; GameInput.TouchBrake = v; });
            ActionBtn("◉", new Vector2(-118, 118), v => GameInput.TouchFire = v);
        }

        void Update()
        {
            foreach (var t in Input.touches)
            {
                bool leftHalf = t.position.x < Screen.width * 0.42f;
                switch (t.phase)
                {
                    case TouchPhase.Began:
                        if (leftHalf && stickFinger < 0)
                        { stickFinger = t.fingerId; stickCenter = t.position; }
                        else if (!leftHalf && lookFinger < 0 &&
                                 !IsOverButton(t.position))
                        { lookFinger = t.fingerId; lastLook = t.position; }
                        break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        if (t.fingerId == stickFinger)
                        {
                            Vector2 d = (t.position - stickCenter) / (Screen.height * 0.11f);
                            GameInput.TouchMove = Vector2.ClampMagnitude(d, 1f);
                            stickKnob.anchoredPosition = new Vector2(130, 130) +
                                GameInput.TouchMove * 55f - new Vector2(25, 25);
                        }
                        else if (t.fingerId == lookFinger)
                        {
                            GameInput.TouchLook += (t.position - lastLook) * 0.35f;
                            lastLook = t.position;
                        }
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        if (t.fingerId == stickFinger)
                        {
                            stickFinger = -1; GameInput.TouchMove = Vector2.zero;
                            stickKnob.anchoredPosition = new Vector2(105, 105);
                        }
                        if (t.fingerId == lookFinger) lookFinger = -1;
                        break;
                }
            }
        }

        bool IsOverButton(Vector2 pos) =>
            pos.x > Screen.width - 260 * Screen.height / 720f &&
            pos.y < 320 * Screen.height / 720f;
    }
}
