using CampanhaRio.Kayak;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampanhaRio.Kayak
{
    /// <summary>
    /// The only class that talks to the Input System: the local player's devices. Reads the "Kayak" map of
    /// Input/CampanhaRioInput.inputactions and produces a <see cref="KayakInputState"/> per physics step.
    /// It lives on the LocalPlayer kayak only. The debug keys (R, F1–F4, Esc) are read here too.
    /// Camera look/zoom always come from here, even while the autopilot drives the kayak.
    /// </summary>
    public class KayakInput : MonoBehaviour, IKayakInputSource
    {
        public InputActionAsset actions;
        [Tooltip("Keyboard steer ramp (units/second): digital keys reach full steer in 1/this seconds. The analog stick is used raw.")]
        public float keyboardSteerResponse = 12f;

        InputActionMap map;
        InputAction steer, paddle, backPaddle, roll, boost, spin, restart, toggleDebug, look, zoom, toggleAutopilot, unlockCursor, swapModel, spawnBots, confirm, freeRide;

        // Presses are latched in Update and consumed by the physics step, so a quick tap
        // that falls between two FixedUpdates is never lost.
        bool paddleTapped, backPaddleTapped, rollTapped;
        float keyboardSteer;
        bool steerFromKeyboard;

        public float Steer => steer != null ? steer.ReadValue<float>() : 0f;
        public bool PaddleHeld => paddle != null && paddle.IsPressed();
        public bool BackPaddleHeld => backPaddle != null && backPaddle.IsPressed();
        /// <summary>Reserved for a spin trick (Q / E / left shoulder). Boost has Shift and the right shoulder.</summary>
        public bool SpinPressed => spin != null && spin.WasPressedThisFrame();
        public bool RestartPressed => restart != null && restart.WasPressedThisFrame();
        public bool ToggleDebugPressed => toggleDebug != null && toggleDebug.WasPressedThisFrame();
        public bool ToggleAutopilotPressed => toggleAutopilot != null && toggleAutopilot.WasPressedThisFrame();
        public bool UnlockCursorPressed => unlockCursor != null && unlockCursor.WasPressedThisFrame();
        /// <summary>F4: swap the kayak simulation model (legacy / physics) for A/B comparison.</summary>
        public bool SwapModelPressed => swapModel != null && swapModel.WasPressedThisFrame();
        /// <summary>F3: spawn the test bots.</summary>
        public bool SpawnBotsPressed => spawnBots != null && spawnBots.WasPressedThisFrame();
        /// <summary>Enter / gamepad Start: the default button on the results card (Retry).</summary>
        public bool ConfirmPressed => confirm != null && confirm.WasPressedThisFrame();
        /// <summary>F / left stick press: toggle free ride (no timer, no gates).</summary>
        public bool FreeRidePressed => freeRide != null && freeRide.WasPressedThisFrame();
        /// <summary>Camera look this frame (mouse delta / right stick, already scaled by the action processors).</summary>
        public Vector2 Look => look != null ? look.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Camera zoom this frame (+ = in).</summary>
        public float Zoom => zoom != null ? zoom.ReadValue<float>() : 0f;
        /// <summary>True when the look input comes from a gamepad stick (a rate, not a per-frame delta).</summary>
        public bool LookIsStick => look != null && look.activeControl != null && look.activeControl.device is Gamepad;

        void Awake()
        {
            if (!actions)
            {
                Debug.LogError("[Campanha] KayakInput has no Input Actions asset assigned.", this);
                return;
            }
            map = actions.FindActionMap("Kayak", true);
            steer = map.FindAction("Steer", true);
            paddle = map.FindAction("Paddle", true);
            backPaddle = map.FindAction("BackPaddle", true);
            roll = map.FindAction("Roll", false);
            boost = map.FindAction("Boost", false);
            spin = map.FindAction("Spin", true);
            restart = map.FindAction("Restart", true);
            toggleDebug = map.FindAction("ToggleDebug", true);
            look = map.FindAction("Look", false);
            zoom = map.FindAction("Zoom", false);
            toggleAutopilot = map.FindAction("ToggleAutopilot", false);
            unlockCursor = map.FindAction("UnlockCursor", false);
            swapModel = map.FindAction("SwapModel", false);
            spawnBots = map.FindAction("SpawnBots", false);
            confirm = map.FindAction("Confirm", false);
            freeRide = map.FindAction("FreeRide", false);
        }

        // The action map is shared by every KayakInput (the asset's): count them, so disabling one (a bot, a remote proxy)
        // never switches off the local player's controls
        static int enabledCount;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetCount() => enabledCount = 0;
        void OnEnable() { if (map != null && enabledCount++ == 0) map.Enable(); }
        void OnDisable() { if (map != null && --enabledCount <= 0) { enabledCount = 0; map.Disable(); } }

        void Update()
        {
            if (map == null) return;
            // Snappy but not binary on keys; the stick already has its own analog value
            if (steer.activeControl != null) steerFromKeyboard = steer.activeControl.device is Keyboard;
            float raw = steer.ReadValue<float>();
            keyboardSteer = steerFromKeyboard ? Mathf.MoveTowards(keyboardSteer, raw, keyboardSteerResponse * Time.deltaTime) : raw;
            paddleTapped |= paddle.WasPressedThisFrame();
            backPaddleTapped |= backPaddle.WasPressedThisFrame();
            if (roll != null) rollTapped |= roll.WasPressedThisFrame();
        }

        /// <summary>Snapshot for one physics step. Clears the latched taps.</summary>
        public KayakInputState ReadInput()
        {
            var state = new KayakInputState
            {
                steer = steerFromKeyboard ? keyboardSteer : Steer,
                paddle = PaddleHeld || paddleTapped,
                backPaddle = BackPaddleHeld || backPaddleTapped,
                roll = rollTapped,
                boost = boost != null && boost.IsPressed(),
                look = Look,
                zoom = Zoom,
            };
            paddleTapped = backPaddleTapped = rollTapped = false;
            return state;
        }
    }
}
