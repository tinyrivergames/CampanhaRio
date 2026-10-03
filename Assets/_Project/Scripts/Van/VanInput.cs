using UnityEngine;
using UnityEngine.InputSystem;

namespace CampanhaRio.Van
{
    /// <summary>
    /// The Van action map (Stage 11), on while the local player sits in the van: throttle/brake (W/S, triggers), steer
    /// (A/D, stick), handbrake (Space), horn (H), radio (R), exit (E), look, lean out of the window (hold Q), wave (G), the
    /// auto-drive (T), take the wheel (C). Taps latch until read. Tests drive it through the Scripted fields.
    /// </summary>
    public class VanInput : MonoBehaviour
    {
        public InputActionAsset actions;

        InputActionMap map;
        InputAction throttle, steer, handbrake, horn, radio, exit, look, lean, wave, auto, swap, zoom;
        bool hornTap, radioTap, exitTap, waveTap, autoTap, swapTap;

        [Header("Scripted (tests)")]
        public bool scripted;
        public VanControls scriptedControls;

        public VanControls Controls => scripted ? scriptedControls : map != null && map.enabled
            ? new VanControls { throttle = throttle.ReadValue<float>(), steer = steer.ReadValue<float>(), handbrake = handbrake.IsPressed() }
            : default;
        public Vector2 Look => !scripted && map != null && map.enabled ? look.ReadValue<Vector2>() : Vector2.zero;
        public bool LookIsStick => look != null && look.activeControl != null && look.activeControl.device is Gamepad;
        public float Zoom => !scripted && map != null && map.enabled ? zoom.ReadValue<float>() : 0f;
        public bool Leaning => !scripted && map != null && map.enabled && lean.IsPressed();

        public bool UnlockKey() => map != null && map.enabled && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

        public bool HornPressed => Take(ref hornTap);
        public bool RadioPressed => Take(ref radioTap);
        public bool ExitPressed => Take(ref exitTap);
        public bool WavePressed => Take(ref waveTap);
        public bool AutoPressed => Take(ref autoTap);
        public bool SwapPressed => Take(ref swapTap);

        /// <summary>Tests: a tap as if the key had been pressed.</summary>
        public void Press(string what)
        {
            switch (what) { case "Horn": hornTap = true; break; case "Radio": radioTap = true; break; case "Exit": exitTap = true; break; case "Wave": waveTap = true; break; case "Auto": autoTap = true; break; case "Swap": swapTap = true; break; }
        }

        static bool Take(ref bool tap) { bool t = tap; tap = false; return t; }

        void Awake()
        {
            if (!actions) return;
            map = actions.FindActionMap("Van", true);
            throttle = map.FindAction("Throttle"); steer = map.FindAction("Steer"); handbrake = map.FindAction("Handbrake");
            horn = map.FindAction("Horn"); radio = map.FindAction("Radio"); exit = map.FindAction("Exit"); look = map.FindAction("Look");
            lean = map.FindAction("Lean"); wave = map.FindAction("Wave"); auto = map.FindAction("AutoDrive"); swap = map.FindAction("Swap");
            zoom = map.FindAction("Zoom");
        }

        public void SetOn(bool on)
        {
            if (map == null) return;
            if (on) map.Enable(); else map.Disable();
            hornTap = radioTap = exitTap = waveTap = autoTap = swapTap = false;
        }

        void OnEnable() => SetOn(false);
        void OnDisable() { if (map != null) map.Disable(); }

        void Update()
        {
            if (map == null || !map.enabled) return;
            if (horn.WasPressedThisFrame()) hornTap = true;
            if (radio.WasPressedThisFrame()) radioTap = true;
            if (exit.WasPressedThisFrame()) exitTap = true;
            if (wave.WasPressedThisFrame()) waveTap = true;
            if (auto.WasPressedThisFrame()) autoTap = true;
            if (swap != null && swap.WasPressedThisFrame()) swapTap = true;
        }
    }
}
