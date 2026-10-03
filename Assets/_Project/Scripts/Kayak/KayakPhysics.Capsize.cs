using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Kayak
{
    /// <summary>
    /// Risk vs. reward (Stage 7 Part D): impact levels, spin-outs, capsizes and the eskimo roll.
    /// Failure is funny and brief, never punishing: the paddler never leaves the kayak, nothing is damaged, the kayak
    /// keeps drifting with the current while upside down, and it always comes back up by itself.
    ///   Impact (Stage 8): glancing = Scrape (speed loss); head-on: Bump &lt; Spin Out Speed &lt; SpinOut &lt; Capsize Speed &lt; Capsize (rare).
    ///   Sloppy landings spin out; only extreme ones, a long broach or being rolled past Capsize Roll Angle capsize.
    ///   Capsizing (~0.4 s roll over) → Capsized (floats upside down; a timing ring after 0.5 s; Roll in the sweet spot =
    ///   a fast eskimo roll, otherwise it rolls up slowly by itself) → Rolling → Recovering (immune, steering fades in).
    /// The self-righting PD simply follows a different roll target in each mode.
    /// </summary>
    public partial class KayakPhysics
    {
        [Header("Impacts (Stage 8: speed loss, not capsizes)")]
        [Tooltip("Head-on share of the approach below which a hit is a glancing Scrape (0 = sliding along, 1 = straight in).")]
        [Range(0f, 1f)] public float glancingHeadOn = 0.6f;
        [Tooltip("Scrape: speed lost (share), from a light brush to a hard scrape at Scrape Full Speed (m/s into the rock).")]
        public Vector2 scrapeSpeedLoss = new Vector2(0.1f, 0.3f);
        public float scrapeFullSpeed = 4f;
        [Tooltip("Head-on below this (m/s into the obstacle): a bump. Above: a spin-out.")]
        public float spinOutSpeed = 2.5f;
        [Tooltip("Speed lost at a head-on spin-out (share).")]
        [Range(0f, 1f)] public float spinOutSpeedLoss = 0.35f;
        [Tooltip("Head-on above this (m/s into a rock or log): a capsize. Rare by design.")]
        public float capsizeSpeed = 6f;
        [Tooltip("Kayak-vs-kayak contacts: the sideways shove apart (m/s at full intensity).")]
        public float kayakBumpPush = 1.2f;
        [Tooltip("What can capsize you when hit hard enough (rocks, logs). Banks top out at a spin-out; other kayaks only scrape.")]
        public LayerMask capsizeLayers;
        [Tooltip("Yaw kick away from the obstacle at a spin-out (deg/s).")]
        public float spinOutYawKick = 110f;
        [Tooltip("How long a spin-out lasts (s) and the share of steering left during it.")]
        public float spinOutDuration = 0.8f;
        [Range(0f, 1f)] public float spinOutSteer = 0.3f;

        [Header("Landings and other capsize triggers")]
        [Tooltip("Landing rolled more than this (degrees), or this sideways (degrees between heading and travel) above Bad Landing Speed: a spin-out.")]
        public float badLandingRoll = 30f;
        public float badLandingSideways = 45f;
        public float badLandingSpeed = 3f;
        [Tooltip("A capsize only for an extreme landing: rolled past this, or this sideways, after at least Capsize Landing Air seconds of air.")]
        public float capsizeLandingRoll = 70f;
        public float capsizeLandingSideways = 80f;
        public float capsizeLandingAir = 0.6f;
        [Tooltip("Broach: this sideways to the flow (degrees) on the wave train or the ledge, in a current above Broach Min Current, for Broach Time (s).")]
        public float broachAngle = 65f;
        public float broachMinCurrent = 4f;
        public float broachTime = 0.6f;
        [Tooltip("Rolled past this (degrees) by anything at all: the kayak goes over.")]
        public float capsizeRollAngle = 100f;

        [Header("Capsize and roll-up")]
        [Tooltip("Seconds to roll upside down.")]
        public float capsizeDuration = 0.4f;
        [Tooltip("Roll impulse toward the impact side when it starts (rad/s).")]
        public float capsizeRollKick = 5f;
        [Tooltip("The timing ring appears this long after the capsize starts (s) and fills over Roll Ring Duration (s).")]
        public float rollPromptDelay = 0.5f;
        public float rollRingDuration = 1f;
        [Tooltip("The sweet spot on the ring (fill share, from..to). Roll inside it = a fast eskimo roll.")]
        public Vector2 rollSweetSpot = new Vector2(0.45f, 0.8f);
        [Tooltip("A successful roll takes this long (s).")]
        public float rollUpDuration = 0.45f;
        [Tooltip("Without a good roll, it rolls up by itself this long after the capsize started (s), slowly and clumsily (s).")]
        public float autoRollDelay = 2.8f;
        public float autoRollDuration = 0.9f;
        [Tooltip("After coming up: capsize immunity (s) and the time for steering to fade back in (s).")]
        public float recoverDuration = 1.5f;
        public float steerFadeIn = 1f;
        [Tooltip("Roll controller while going over and coming back up: stiffness (1/s²), damping (1/s), max torque (N·m).")]
        public float capsizeStrength = 80f;
        public float capsizeDamping = 14f;
        public float capsizeTorque = 4000f;

        [Header("Never stuck")]
        [Tooltip("Pinned against an obstacle below this speed (m/s) for Stuck Time (s): pushed off along the flow at Unstick Speed (m/s).")]
        public float stuckSpeed = 0.2f;
        public float stuckTime = 2f;
        public float unstickSpeed = 1.5f;

        /// <summary>The timing ring while capsized: -1 hidden, else 0..1 fill. The HUD draws it.</summary>
        public float RollRing { get; private set; } = -1f;
        /// <summary>Did the last roll attempt hit the sweet spot?</summary>
        public bool LastRollSucceeded { get; private set; }
        /// <summary>Capsize immunity left (s).</summary>
        public bool Immune => kayak && (kayak.Mode == KayakMode.Capsizing || kayak.Mode == KayakMode.Capsized
                                        || kayak.Mode == KayakMode.Rolling || kayak.Mode == KayakMode.Recovering);

        float modeTime, capsizeStart, rollSide = 1f, rollDuration, broachTimer, stuckTimer;
        bool rollAttempted, clumsyRoll;
        IRiverModifier lastFeature;
        float lastFeatureWeight;
        Vector3 lastObstacleNormal;

        void ResetModes()
        {
            if (kayak) kayak.Mode = KayakMode.Normal;
            modeTime = broachTimer = stuckTimer = 0f;
            rollAttempted = false;
            RollRing = -1f;
        }

        void SetMode(KayakMode mode)
        {
            kayak.Mode = mode;
            modeTime = 0f;
        }

        bool NormalMode => kayak.Mode == KayakMode.Normal || kayak.Mode == KayakMode.Airborne;

        /// <summary>Roll angle (degrees, -180..180): + = right side down.</summary>
        static float RollAngle(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            Vector3 worldUp = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (worldUp.sqrMagnitude < 1e-4f) return 0f; // pointing straight up or down
            return Vector3.SignedAngle(worldUp, rotation * Vector3.up, forward);
        }

        /// <summary>How much of the paddler's control is left: 1 normally, less in a spin-out, 0 while capsized.</summary>
        float ControlShare()
        {
            switch (kayak.Mode)
            {
                case KayakMode.SpinOut: return spinOutSteer;
                case KayakMode.Capsizing:
                case KayakMode.Capsized:
                case KayakMode.Rolling: return 0f;
                case KayakMode.Recovering: return Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(modeTime / Mathf.Max(steerFadeIn, 0.01f)));
                default: return 1f;
            }
        }

        void UpdateModes(float dt, in KayakInputState input, float roll, float waterSpeed, Vector3 flowDir, Vector3 right, bool airborne)
        {
            modeTime += dt;
            RollRing = -1f;
            switch (kayak.Mode)
            {
                case KayakMode.Normal:
                case KayakMode.Airborne:
                case KayakMode.SpinOut:
                    if (kayak.Mode == KayakMode.SpinOut && modeTime >= spinOutDuration) SetMode(KayakMode.Normal);
                    if (Mathf.Abs(roll) > capsizeRollAngle) { StartCapsize(Mathf.Sign(roll)); break; }
                    if (!airborne && Broaching(dt, waterSpeed, flowDir, right)) StartCapsize(Vector3.Dot(right, flowDir) > 0f ? -1f : 1f); // the upstream edge catches
                    break;

                case KayakMode.Capsizing:
                    if (modeTime >= capsizeDuration) SetMode(KayakMode.Capsized);
                    break;

                case KayakMode.Capsized:
                    float sinceCapsize = kayak.SimTime - capsizeStart;
                    float ring = (sinceCapsize - rollPromptDelay) / Mathf.Max(rollRingDuration, 0.01f);
                    if (ring >= 0f && ring <= 1f && !rollAttempted) RollRing = ring;
                    if (input.roll && !rollAttempted && ring >= 0f)
                    {
                        rollAttempted = true;
                        LastRollSucceeded = ring >= rollSweetSpot.x && ring <= rollSweetSpot.y;
                        kayak.RaiseRollAttempt(LastRollSucceeded);
                        if (LastRollSucceeded) { StartRolling(false); break; }
                    }
                    if (sinceCapsize >= autoRollDelay) StartRolling(true);
                    break;

                case KayakMode.Rolling:
                    if (modeTime >= rollDuration)
                    {
                        SetMode(KayakMode.Recovering);
                        kayak.RaiseRecovered();
                    }
                    break;

                case KayakMode.Recovering:
                    if (modeTime >= recoverDuration) SetMode(KayakMode.Normal);
                    break;
            }
        }

        /// <summary>Sideways to the flow on a big standing wave or the ledge hydraulic, at speed, for a moment.</summary>
        bool Broaching(float dt, float waterSpeed, Vector3 flowDir, Vector3 right)
        {
            bool feature = lastFeature is RiverFeature f && (f.type == RiverFeatureType.ChuteWaveTrain || f.type == RiverFeatureType.Ledge) && lastFeatureWeight > 0.2f;
            float sideways = Vector3.Angle(flowDir, new Vector3(-right.z, 0f, right.x)); // angle between the flow and the bow
            bool broadside = sideways > broachAngle && sideways < 180f - broachAngle;
            broachTimer = feature && broadside && waterSpeed > broachMinCurrent ? broachTimer + dt : Mathf.Max(0f, broachTimer - 2f * dt);
            return broachTimer > broachTime;
        }

        /// <summary>Touching down rolled over or skidding sideways at speed.</summary>
        void CheckLanding(Quaternion rotation, Vector3 velocity, Vector3 forward)
        {
            if (!NormalMode) return;
            float roll = RollAngle(rotation);
            Vector3 flat = Flat(velocity);
            float sideways = flat.sqrMagnitude > 0.01f ? Vector3.Angle(forward, flat) : 0f;
            bool sidewaysAtSpeed = flat.magnitude > badLandingSpeed;
            bool skidding = sidewaysAtSpeed && sideways > badLandingSideways && sideways < 180f - badLandingSideways;
            bool extreme = kayak.AirTime >= capsizeLandingAir
                           && (Mathf.Abs(roll) > capsizeLandingRoll || (sidewaysAtSpeed && sideways > capsizeLandingSideways && sideways < 180f - capsizeLandingSideways));
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            if (extreme) StartCapsize(Mathf.Abs(roll) > capsizeLandingRoll ? Mathf.Sign(roll) : Vector3.Dot(flat, right) > 0f ? 1f : -1f);
            else if (Mathf.Abs(roll) > badLandingRoll || skidding) SpinOut(Vector3.Dot(flat, right) > 0f ? 1f : -1f, 0.6f, spinOutSpeedLoss * 0.5f);
        }

        /// <summary>
        /// Sorts a hit by contact angle and speed: glancing = Scrape (speed loss, slides along), head-on = Bump / SpinOut /
        /// (very fast, rocks and logs only) Capsize. Kayak-vs-kayak contacts are only ever bumps and scrapes.
        /// </summary>
        void HandleImpact(float speed, float headOn, Vector3 point, Vector3 normal, bool otherKayak, bool rock, bool gentle = false)
        {
            if (kayak.SimTime - lastImpactTime < bumpCooldown) return;
            lastObstacleNormal = normal;
            bool capsized = !NormalMode && kayak.Mode != KayakMode.SpinOut;
            bool glancing = headOn < glancingHeadOn;
            ImpactLevel level;
            if (capsized || gentle) level = ImpactLevel.Bump;
            else if (otherKayak) level = speed >= spinOutSpeed * 0.5f ? ImpactLevel.Scrape : ImpactLevel.Bump;
            else if (glancing) level = ImpactLevel.Scrape;
            else if (speed >= capsizeSpeed && rock && !Immune) level = ImpactLevel.Capsize;
            else if (speed >= spinOutSpeed) level = ImpactLevel.SpinOut;
            else level = ImpactLevel.Bump;

            float intensity = level switch
            {
                ImpactLevel.Bump => Mathf.Clamp01(speed / Mathf.Max(spinOutSpeed, 0.01f)),
                ImpactLevel.Scrape => Mathf.Clamp01(speed / Mathf.Max(scrapeFullSpeed, 0.01f)),
                ImpactLevel.SpinOut => 0.5f + 0.5f * Mathf.InverseLerp(spinOutSpeed, capsizeSpeed, speed),
                _ => 1f,
            };
            if (level <= ImpactLevel.Scrape && intensity < 0.05f) return;
            lastImpactTime = kayak.SimTime;
            kayak.RaiseImpact(level, intensity, point, normal);

            Vector3 right = rb.rotation * Vector3.right;
            float away = Mathf.Sign(Vector3.Dot(normal, right)); // + = the obstacle was on the left
            if (level == ImpactLevel.Scrape)
            {
                // Lose some speed and slide along it: no spin (the contact's own yaw kick is mostly taken back)
                float loss = Mathf.Lerp(scrapeSpeedLoss.x, scrapeSpeedLoss.y, Mathf.Max(intensity, headOn / Mathf.Max(glancingHeadOn, 0.01f) * 0.5f));
                if (otherKayak) loss *= 0.3f;
                LoseSpeed(loss);
                Vector3 w = rb.angularVelocity;
                rb.angularVelocity = new Vector3(w.x, Mathf.Lerp(w.y, stepYaw, 0.8f), w.z);
            }
            else if (level == ImpactLevel.SpinOut) SpinOut(away, intensity, spinOutSpeedLoss);
            // Kayak-vs-kayak: a light shove apart, never more (a friend, not a rock)
            if (otherKayak && Flat(normal).sqrMagnitude > 0.0025f)
                rb.AddForce(Flat(normal).normalized * (kayakBumpPush * (0.5f + 0.5f * intensity)), ForceMode.VelocityChange);
            else if (level == ImpactLevel.Capsize) StartCapsize(-away); // rolls toward the impact side
        }

        void LoseSpeed(float share)
        {
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(v.x * (1f - share), v.y, v.z * (1f - share));
        }

        /// <summary>The bow is knocked around, steering is reduced for a moment, and speed is lost.</summary>
        void SpinOut(float away, float intensity, float speedLoss)
        {
            LoseSpeed(speedLoss);
            // The bow is knocked away from the obstacle
            rb.AddTorque(Vector3.up * (inertia.y * spinOutYawKick * Mathf.Deg2Rad * intensity * away), ForceMode.Impulse);
            if (kayak.Mode != KayakMode.SpinOut) SetMode(KayakMode.SpinOut);
            else modeTime = 0f;
        }

        void StartCapsize(float side)
        {
            if (Immune) return;
            rollSide = side >= 0f ? 1f : -1f;
            capsizeStart = kayak.SimTime;
            rollAttempted = false;
            broachTimer = 0f;
            SetMode(KayakMode.Capsizing);
            rb.AddTorque(rb.rotation * Vector3.forward * (inertia.z * capsizeRollKick * rollSide), ForceMode.Impulse);
            kayak.RaiseCapsize();
        }

        void StartRolling(bool clumsy)
        {
            clumsyRoll = clumsy;
            rollDuration = clumsy ? autoRollDuration : rollUpDuration;
            SetMode(KayakMode.Rolling);
        }

        /// <summary>The roll target and controller for the current mode.</summary>
        void RollGains(out float target, out float kp, out float kd, out float maxTorque)
        {
            switch (kayak.Mode)
            {
                case KayakMode.Capsizing:
                    target = rollSide * 180f * Smooth(modeTime / Mathf.Max(capsizeDuration, 0.01f));
                    kp = capsizeStrength; kd = capsizeDamping; maxTorque = capsizeTorque;
                    return;
                case KayakMode.Capsized:
                    target = rollSide * 180f; // floats upside down, holding its breath
                    kp = rightingStrength * 0.6f; kd = rightingDamping; maxTorque = capsizeTorque * 0.4f;
                    return;
                case KayakMode.Rolling:
                    float t = modeTime / Mathf.Max(rollDuration, 0.01f);
                    target = rollSide * 180f * (1f - Smooth(t));
                    if (clumsyRoll) target += Mathf.Sin(t * Mathf.PI * 3f) * 18f * (1f - t); // wobbly on the way up
                    kp = capsizeStrength; kd = capsizeDamping; maxTorque = capsizeTorque;
                    return;
                default:
                    target = 0f; kp = rightingStrength; kd = rightingDamping; maxTorque = maxRightingTorque;
                    return;
            }
        }

        /// <summary>Never pinned: stopped against an obstacle in a current for a while, it is pushed off along the flow.</summary>
        void NeverStuck(float dt, Vector3 velocity, Vector3 flowDir, float waterSpeed)
        {
            bool pinned = Flat(velocity).magnitude < stuckSpeed && kayak.SimTime - lastObstacleContact < 0.3f && waterSpeed > 0.3f;
            stuckTimer = pinned ? stuckTimer + dt : 0f;
            if (stuckTimer < stuckTime) return;
            stuckTimer = 0f;
            Vector3 away = Flat(lastObstacleNormal);
            Vector3 push = (flowDir + away * 0.5f).normalized * unstickSpeed;
            rb.AddForce(push, ForceMode.VelocityChange);
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    }
}
