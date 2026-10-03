using System;
using CampanhaRio.Core;
using CampanhaRio.Kayak;
using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Kayak
{
    /// <summary>A kayak simulation model. Writes its results into the owning KayakController.</summary>
    [Serializable]
    public abstract class KayakSimulation
    {
        [NonSerialized] protected KayakController kayak;
        [NonSerialized] protected Rigidbody rb;
        [NonSerialized] protected CapsuleCollider hull;

        /// <summary>Seconds between strokes while Paddle is held (the paddle animation syncs to it).</summary>
        public abstract float StrokeInterval { get; }

        /// <summary>Takes over the Rigidbody. Position and velocity are kept (that is how a respawn keeps the body).</summary>
        public virtual void Enter(KayakController owner, Rigidbody body, CapsuleCollider hullCollider)
        {
            kayak = owner; rb = body; hull = hullCollider;
        }

        public virtual void Exit() { }

        /// <summary>Forgets everything remembered between steps (after a respawn or teleport).</summary>
        public abstract void ResetState();

        /// <summary>Stage 12: the kayak moved onto another river; forget the lookup hints into the old one.</summary>
        public virtual void ResetRiverHints() { }

        public abstract void Step(float dt, in KayakInputState input, Vector3 nudge);

        /// <summary>A collision on the kayak (forwarded from OnCollisionEnter, and OnCollisionStay with enter = false).</summary>
        public virtual void OnCollision(Collision collision, bool enter) { }

        /// <summary>0 upright .. 1 upside down.</summary>
        public virtual float CapsizeProgress => 0f;

        protected static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }

    /// <summary>
    /// The kayak: the one component the rest of the game talks to (camera, HUD, animation, FX, bots).
    /// It owns the shared state and events and hands each physics step to the selected simulation model.
    /// Input comes only through <see cref="InputSource"/> (a KayakInputState per step), so a player, a bot or the
    /// network can drive it the same way. Every kayak registers itself in <see cref="KayakRegistry"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class KayakController : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        public RiverPath river;
        [Tooltip("The local player's devices. Only on the LocalPlayer kayak; also handles the debug key R.")]
        public KayakInput input;
        [Tooltip("Optional. Used for the fade when restarting.")]
        public ScreenFader fader;

        [Header("Simulation")]
        [Tooltip("Scales the river current for this kayak (the flow field itself lives on RiverPath).")]
        public float currentInfluence = 1f;
        [Tooltip("Layers treated as obstacles (rocks, pier).")]
        public LayerMask obstacleLayers;
        [Tooltip("The Stage 7 model (Rigidbody, point buoyancy, forces).")]
        public KayakPhysics physics = new KayakPhysics();

        [Header("Respawn")]
        public bool respawnAtRiverEnd = true;
        public float riverEndMargin = 12f;

        /// <summary>Who drives this kayak. Defaults to the KayakInput on the same object; the autopilot takes over while active.</summary>
        public IKayakInputSource InputSource { get; set; }

        /// <summary>The compact state (what Etapa 8 sends over the network). Updated every physics step.</summary>
        public KayakState State => state;
        public KayakMode Mode { get; internal set; } = KayakMode.Normal;
        public KayakActivity Activity { get; private set; }

        // Read by camera, visuals and HUD. Written by the simulation model.
        public Vector3 Velocity { get; internal set; }
        public float Speed => new Vector2(Velocity.x, Velocity.z).magnitude;
        public float CurrentSpeed { get; internal set; }
        /// <summary>Speed across the river (+ = toward the right bank).</summary>
        public float LateralSpeed { get; internal set; }
        /// <summary>Degrees per second, + = turning right.</summary>
        public float YawRate { get; internal set; }
        public float Steer { get; internal set; }
        public RiverPath.RiverSample RiverSample { get; internal set; }
        public bool IsAirborne { get; internal set; }
        /// <summary>Seconds in the air for the current (or last) jump.</summary>
        public float AirTime { get; internal set; }
        /// <summary>Downward speed at the last landing (m/s).</summary>
        public float LastLandingImpact { get; private set; }
        public int StrokeCount { get; private set; }
        /// <summary>Side of the last stroke: -1 left, +1 right.</summary>
        public int LastStrokeSide { get; private set; } = 1;
        /// <summary>Whether the last stroke was a back stroke.</summary>
        public bool LastStrokeBack { get; private set; }
        public bool InObstacleContact { get; internal set; }
        /// <summary>Sideways current difference bow minus stern (m/s): what spins the kayak on an eddy line.</summary>
        public float Shear { get; internal set; }
        /// <summary>The full current at the kayak including boils (m/s).</summary>
        public Vector3 CurrentVector { get; internal set; }
        /// <summary>Name of the river feature under the kayak ("-" if none), for debug and logs.</summary>
        public string FeatureName { get; internal set; } = "-";
        /// <summary>Simulated seconds since the start (drives boils and the bob).</summary>
        public float SimTime { get; private set; }
        /// <summary>0..1 while boosting (the camera widens and the streaks tint; set by the boost, Stage 8).</summary>
        public float BoostVisual { get; internal set; }
        /// <summary>0..1 while the water surges the kayak forward (Natural game feel: the FX and the camera ease on it).</summary>
        public float SurgeVisual { get; internal set; }
        /// <summary>Held in place (lined up for the countdown, Stage 8). Released at push-off.</summary>
        public bool Held
        {
            get => held;
            set { if (value && rb) { heldPosition = rb.position; heldRotation = rb.rotation; } held = value; } // always the pose now (a late joiner is held twice)
        }
        bool held;
        Vector3 heldPosition;
        Quaternion heldRotation;
        /// <summary>When set (a run in progress), R restarts the run instead of respawning this kayak.</summary>
        public static Action RestartOverride;
        public KayakSimulation Simulation => active;
        public float StrokeInterval => active != null ? active.StrokeInterval : physics.strokeInterval;

        public event Action Respawned;
        /// <summary>A paddle stroke starts: (side -1/+1, isBackStroke).</summary>
        public event Action<int, bool> OnStroke;
        /// <summary>Hit something: (level, intensity 0..1, contact point, normal pointing away from what was hit).</summary>
        public event Action<ImpactLevel, float, Vector3, Vector3> OnImpact;
        /// <summary>Starts rolling over (Capsizing).</summary>
        public event Action OnCapsize;
        /// <summary>Roll pressed while capsized: true = in the sweet spot (a fast eskimo roll).</summary>
        public event Action<bool> OnRollAttempt;
        /// <summary>Upright again after a capsize.</summary>
        public event Action OnRecovered;
        /// <summary>A skill moment (Stage 8): perfect stroke, slingshot, boof, spin, clean landing, near miss, boost…</summary>
        public event Action<SkillEvent> OnSkill;
        /// <summary>Touched down after a jump: (seconds in the air). The impact speed is in LastLandingImpact.</summary>
        public event Action<float> OnLand;
        /// <summary>Left the water (the start of a jump; sync jumps and the network use it).</summary>
        public event Action OnTakeOff;

        Rigidbody rb;
        CapsuleCollider capsule;
        KayakSimulation active;
        KayakState state;
        Vector3 pendingNudge;
        Vector3 startPosition;
        Quaternion startRotation;
        bool initialized;
        float lastStrokeTime = -10f;

        void Awake() => Init();

        void Init()
        {
            if (initialized) return;
            initialized = true;
            rb = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            if (obstacleLayers.value == 0) obstacleLayers = LayerMask.GetMask("Obstacle");
            if (!river) river = RiverPath.Resolve(transform.position);
            if (!input) input = GetComponent<KayakInput>();
            if (!fader) fader = FindAnyObjectByType<ScreenFader>();
            if (InputSource == null && input) InputSource = input; // (not "= input" when missing: a fake-null component would pass a null check)
            startPosition = transform.position;
            startRotation = transform.rotation;
            active = physics;
            active.Enter(this, rb, capsule);
        }

        void OnEnable() => KayakRegistry.Register(this);
        void OnDisable() => KayakRegistry.Unregister(this);

        void OnCollisionEnter(Collision collision) { if (active != null && !IsRemote) active.OnCollision(collision, true); } // a proxy's owner resolves its contacts
        void OnCollisionStay(Collision collision) { if (active != null && !IsRemote) active.OnCollision(collision, false); }

        /// <summary>Back to the player's own input (the autopilot calls this when it lets go).</summary>
        public void ResetInputSource() => InputSource = input ? input : null;

        void Update()
        {
            if (!input) return;
            if (input.RestartPressed) { if (RestartOverride != null) RestartOverride(); else RequestRespawn(); }
        }

        void FixedUpdate()
        {
            KeepOnTheRightRiver();
            if (IsRemote) return; // a friend's kayak: its owner simulates it, the network component moves this proxy
            Simulate(Time.fixedDeltaTime, InputSource != null ? InputSource.ReadInput() : default);
        }

        // ------------------------------------------------------------------ several rivers (Stage 12)

        float nextRiverCheck;

        /// <summary>
        /// A track complex has several rivers meeting in shared lakes. A kayak keeps its river while it is on its water (a
        /// racer stays on its track through the pools); once it leaves that water it takes the water it is on.
        /// </summary>
        void KeepOnTheRightRiver()
        {
            if (RiverPath.All.Count < 2 || Time.time < nextRiverCheck) return;
            nextRiverCheck = Time.time + 0.25f;
            if (river && river.Contains(transform.position, 1f)) return;
            var r = RiverPath.Resolve(transform.position);
            if (r && r != river) SwitchRiver(r);
        }

        /// <summary>Put this kayak on another river (a track's line-up, or leaving one water for another).</summary>
        public void SwitchRiver(RiverPath r)
        {
            if (!r || r == river) return;
            river = r;
            active?.ResetRiverHints();
        }

        // ------------------------------------------------------------------ remote kayaks (Stage 9)

        /// <summary>
        /// A proxy of a kayak simulated on another machine: kinematic, no simulation, no input. The network component moves it
        /// and writes its state here (ApplyRemote), and raises its events, so the paddler, the FX, the audio, drafting and the
        /// local collisions see it like any kayak.
        /// </summary>
        public bool IsRemote { get; private set; }

        public void SetRemote(bool remote)
        {
            Init();
            IsRemote = remote;
            rb.isKinematic = remote;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (remote) { InputSource = null; Held = false; }
            else ResetInputSource();
        }

        /// <summary>One synced frame for a proxy. The pose itself is moved by the caller (MovePosition on the kinematic body).</summary>
        internal void ApplyRemote(in KayakState s, bool airborne, float airTime, float boost, int strokeCount, bool strokeBack, float dt)
        {
            if (!river) return;
            SimTime += dt;
            Velocity = s.velocity;
            YawRate = s.angularVelocity.y * Mathf.Rad2Deg;
            Steer = s.steer;
            Mode = s.mode;
            IsAirborne = airborne;
            AirTime = airTime;
            BoostVisual = boost;
            var sample = river.Sample(s.position);
            RiverSample = sample;
            CurrentVector = sample.waterVelocity * currentInfluence;
            CurrentSpeed = CurrentVector.magnitude;
            LateralSpeed = Vector3.Dot(s.velocity, sample.right);
            // A stroke counter instead of a stroke event: a lost packet never loses a stroke animation
            if (remoteStrokeCount >= 0 && strokeCount != remoteStrokeCount) RaiseStroke(s.strokeSide >= 0 ? 1 : -1, strokeBack);
            remoteStrokeCount = strokeCount;
            Activity = airborne ? KayakActivity.Airborne
                : s.strokePhase < 0.99f ? KayakActivity.Paddling // the phase saturates at 1 once the next stroke is due
                : Mathf.Abs(Steer) > 0.05f ? KayakActivity.Steering
                : Speed < 0.3f && CurrentSpeed < 0.3f ? KayakActivity.Resting
                : KayakActivity.Carried;
            state = s;
        }

        int remoteStrokeCount = -1;

        internal void RaiseTakeOff() => OnTakeOff?.Invoke();

        /// <summary>
        /// A safety net: no kayak is ever faster than this (m/s), or rises faster than Max Rise. Something infinitely heavy (a
        /// friend's kinematic proxy jumping) could otherwise fling it into the sky. Logged once.
        /// </summary>
        public static float MaxSpeed = 60f, MaxRise = 20f;
        static bool warnedRunaway;

        void ClampRunaway()
        {
            Vector3 v = rb.linearVelocity;
            bool bad = !float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z);
            if (!bad && v.sqrMagnitude <= MaxSpeed * MaxSpeed && v.y <= MaxRise) return;
            if (!warnedRunaway) { warnedRunaway = true; Debug.LogWarning($"[Campanha] {name}: runaway velocity {v} clamped."); }
            if (bad) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; return; }
            v = Vector3.ClampMagnitude(v, MaxSpeed);
            v.y = Mathf.Min(v.y, MaxRise);
            rb.linearVelocity = v;
        }

        public void Simulate(float dt, in KayakInputState inputState)
        {
            Init();
            if (!river) return;
            if (Held)
            {
                // Waiting on the start line: stay put (the countdown releases it). The pose is pinned: without the water's
                // lift gravity would sink it, and the buoyancy would launch it at push-off.
                rb.position = heldPosition;
                rb.rotation = heldRotation;
                RiverSample = river.Sample(heldPosition); // where it waits (not where it was before the line-up)
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                return;
            }
            SimTime += dt;

            Vector3 nudge = pendingNudge;
            pendingNudge = Vector3.zero;
            active.Step(dt, inputState, nudge);
            ClampRunaway();

            if (Mode == KayakMode.Normal || Mode == KayakMode.Airborne) Mode = IsAirborne ? KayakMode.Airborne : KayakMode.Normal;
            Activity = IsAirborne ? KayakActivity.Airborne
                : SimTime - lastStrokeTime < StrokeInterval * 1.5f ? KayakActivity.Paddling
                : Mathf.Abs(Steer) > 0.05f ? KayakActivity.Steering
                : Speed < 0.3f && CurrentSpeed < 0.3f ? KayakActivity.Resting
                : KayakActivity.Carried;

            state = new KayakState
            {
                position = rb.position,
                rotation = rb.rotation,
                velocity = rb.linearVelocity,
                angularVelocity = rb.angularVelocity,
                mode = Mode,
                strokeSide = (sbyte)LastStrokeSide,
                strokePhase = Mathf.Clamp01((SimTime - lastStrokeTime) / Mathf.Max(StrokeInterval, 0.01f)),
                capsizeProgress = active.CapsizeProgress,
                steer = Steer,
            };

            if (respawnAtRiverEnd && RiverSample.distanceAlong > river.Length - riverEndMargin) RequestRespawn();
        }

        // Called by the simulation models (events can only be raised here)
        internal void RaiseStroke(int side, bool back)
        {
            LastStrokeSide = side;
            LastStrokeBack = back;
            StrokeCount++;
            lastStrokeTime = SimTime;
            OnStroke?.Invoke(side, back);
        }

        internal void RaiseLand(float impact, float airTime)
        {
            LastLandingImpact = impact;
            OnLand?.Invoke(airTime);
        }

        internal void RaiseImpact(ImpactLevel level, float intensity, Vector3 point, Vector3 normal) => OnImpact?.Invoke(level, intensity, point, normal);

        /// <summary>A hit from something outside the physics contacts (a drifting log): the event only, no forces.</summary>
        public void ExternalImpact(ImpactLevel level, float intensity, Vector3 point, Vector3 normal) => RaiseImpact(level, intensity, point, normal);
        internal void RaiseCapsize() => OnCapsize?.Invoke();
        internal void RaiseRollAttempt(bool success) => OnRollAttempt?.Invoke(success);
        internal void RaiseRecovered() => OnRecovered?.Invoke();

        // ------------------------------------------------------------------ the boost meter (Stage 8)

        /// <summary>The boost meter, 0..MeterMax. Filled by good play (skills, drafting, pickups), spent by Boost.</summary>
        public float Meter { get; private set; }
        public const float MeterMax = 100f;

        /// <summary>Adds (or, negative, spends) meter; the skill event, when given, is raised for the score and the FX.</summary>
        public void AddMeter(float amount)
        {
            Meter = Mathf.Clamp(Meter + amount, 0f, MeterMax);
            // Natural game feel (Stage 9.5): no boost button; what you earn pushes you right away, the water surging
            var feel = KayakFeel.Current;
            if (amount > 0f && !feel.manualBoost) PendingSurge = Mathf.Min(PendingSurge + amount * feel.surgePerMeter, feel.maxSurge);
        }

        /// <summary>Speed still to be given by the surge (m/s); the simulation hands it out over a fraction of a second.</summary>
        public float PendingSurge { get; internal set; }

        internal void RaiseSkill(SkillType type, float value, float meter)
        {
            AddMeter(meter);
            OnSkill?.Invoke(new SkillEvent(type, value, meter));
        }

        /// <summary>For things outside the kayak (pickups, gates, drafting, sync jumps): meter plus the event.</summary>
        public void AwardSkill(SkillType type, float value, float meter) => RaiseSkill(type, value, meter);

        /// <summary>A soft push from something floating (a drifting log): added to the velocity on the next physics step.</summary>
        public void Nudge(Vector3 velocityChange) { velocityChange.y = 0f; pendingNudge += velocityChange; }

        /// <summary>Back to the start, behind a fade when a ScreenFader exists (only the local player's kayak fades).</summary>
        public void RequestRespawn()
        {
            if (fader && fader.isActiveAndEnabled && KayakRegistry.Local == this)
            {
                if (!fader.IsFading) fader.FadeThrough(Respawn);
            }
            else Respawn();
        }

        public void Respawn()
        {
            Teleport(startPosition, startRotation, Vector3.zero);
            Respawned?.Invoke();
        }

        /// <summary>Where Respawn puts the kayak (bots start here too, offset sideways).</summary>
        public void SetStart(Vector3 position, Quaternion rotation) { startPosition = position; startRotation = rotation; }

        /// <summary>Moves the kayak instantly, without a fade (benchmarks, bot spawns). Resets the simulation's memory.</summary>
        public void Teleport(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            Init();
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            heldPosition = position; heldRotation = rotation; // a held kayak waits where it was put, never at an older spot
            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;
            if (river) RiverSample = river.Sample(position); // a stale sample would still be at the finish line
            Steer = 0f;
            pendingNudge = Vector3.zero;
            IsAirborne = false;
            InObstacleContact = false;
            Mode = KayakMode.Normal;
            Meter = 0f;
            PendingSurge = 0f;
            SurgeVisual = 0f;
            BoostVisual = 0f;
            active.ResetState();
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            Vector3 origin = transform.position + Vector3.up * 1.5f;
            Gizmos.color = Color.cyan;   // water current
            Gizmos.DrawLine(origin, origin + RiverSample.waterVelocity);
            Gizmos.color = Color.yellow; // kayak velocity
            Gizmos.DrawLine(origin, origin + Velocity);
        }
    }
}
