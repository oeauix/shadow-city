// ============================================================================
// SHADOW CITY — Core/GameEvents.cs + Core/GameInput.cs
// Event bus (mirrors web SC.Events) and unified input (keyboard/mouse/touch).
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowCity
{
    /// <summary>Global event bus. Exception-safe like the web build.</summary>
    public static class GameEvents
    {
        static readonly Dictionary<string, List<Action<object>>> map = new();

        public const string PhaseChanged = "time.phase";
        public const string HourTick = "time.hour";
        public const string DistrictEntered = "world.district";
        public const string ResonanceChanged = "res.changed";
        public const string ResonanceThreshold = "res.threshold";
        public const string PulseFired = "pulse.fired";
        public const string XPGained = "rpg.xp";
        public const string LevelUp = "rpg.level";
        public const string CashChanged = "eco.cash";
        public const string CrimeCommitted = "crime";
        public const string WantedChanged = "police.wanted";
        public const string PlayerDied = "player.died";
        public const string PlayerBusted = "police.busted";
        public const string EnterVehicle = "player.enterVehicle";
        public const string ExitVehicle = "player.exitVehicle";
        public const string VehicleStolen = "vehicle.stolen";
        public const string MissionStarted = "mission.started";
        public const string MissionCompleted = "mission.completed";
        public const string MissionFailed = "mission.failed";
        public const string Notify = "ui.notify";
        public const string Subtitle = "ui.subtitle";
        public const string LanguageChanged = "l10n.changed";

        public static Action On(string name, Action<object> fn)
        {
            if (!map.TryGetValue(name, out var list)) map[name] = list = new();
            list.Add(fn);
            return () => list.Remove(fn);
        }

        public static void Emit(string name, object arg = null)
        {
            if (!map.TryGetValue(name, out var list)) return;
            var snapshot = list.ToArray();
            foreach (var fn in snapshot)
            {
                try { fn(arg); }
                catch (Exception e) { Debug.LogError($"[Events] {name}: {e}"); }
            }
        }

        public static void Clear() => map.Clear();
    }

    /// <summary>
    /// Unified input: desktop keys/mouse + mobile touch overlay write into the
    /// same state, so gameplay code never branches on platform.
    /// </summary>
    public static class GameInput
    {
        // Written by TouchControls on mobile; merged with keyboard on desktop.
        public static Vector2 TouchMove;      // virtual joystick [-1..1]
        public static Vector2 TouchLook;      // drag delta (pixels)
        public static bool TouchSprint, TouchJump, TouchInteract, TouchPulse,
                           TouchFire, TouchAim, TouchBrake;

        static bool prevInteract, prevJump, prevPulse, prevFire;
        public static bool InteractPressed, JumpPressed, PulsePressed, FirePressed;

        public static bool IsMobile =>
            Application.isMobilePlatform ||
            (Application.platform == RuntimePlatform.WebGLPlayer && Input.touchSupported && Screen.width < 1300);

        public static Vector2 Move
        {
            get
            {
                var kb = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                var v = kb.sqrMagnitude > 0.01f ? kb : TouchMove;
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static Vector2 Look
        {
            get
            {
                var m = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 2.2f;
                return m.sqrMagnitude > 0.0001f ? m : TouchLook * 0.13f;
            }
        }

        public static bool Sprint => Input.GetKey(KeyCode.LeftShift) || TouchSprint;
        public static bool Aim => Input.GetMouseButton(1) || TouchAim;
        public static bool FireHeld => (Input.GetMouseButton(0) && !UIOverPointer()) || TouchFire;
        public static bool Brake => Input.GetKey(KeyCode.Space) || TouchBrake;

        /// <summary>Call once per frame from Bootstrap before all systems.</summary>
        public static void Frame()
        {
            bool i = Input.GetKey(KeyCode.E) || TouchInteract;
            bool j = Input.GetKey(KeyCode.Space) || TouchJump;
            bool p = Input.GetKey(KeyCode.Q) || TouchPulse;
            bool f = FireHeld;
            InteractPressed = i && !prevInteract;
            JumpPressed = j && !prevJump;
            PulsePressed = p && !prevPulse;
            FirePressed = f && !prevFire;
            prevInteract = i; prevJump = j; prevPulse = p; prevFire = f;
            TouchLook = Vector2.zero;   // consumed each frame
        }

        static bool UIOverPointer()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            return es != null && es.IsPointerOverGameObject();
        }
    }
}
