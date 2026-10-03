using CampanhaRio.World;
using UnityEngine;

namespace CampanhaRio.Kayak.Paddler
{
    public enum PaddlerState { Drift, Steer, Paddle, BackPaddle, Rapids, Airborne, Rest, SpinOut, Capsizing, Capsized, Rolling, Recovered }

    /// <summary>
    /// The paddler's small brain: reads gameplay (never changes it) and blends the layers:
    ///   paddle base pose (rest / ready / rudder / lifted), extra visual-only strokes (idle corrections, rapids braces),
    ///   torso lean / twist, landing compression, bump flinch, resting behaviours (stretch, trailing a hand in the water)
    ///   and where the head looks (LookTarget markers, the river ahead, idle glances).
    /// Everything blends over time: state changes only move targets, the values chase them smoothly.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public class PaddlerAnimationController : MonoBehaviour
    {
        public KayakController kayak;
        public PaddleAnimator paddle;
        public PaddlerRig rig;
        [Tooltip("IK targets the hands actually follow (the controller places them: on the grips, or trailing in the water).")]
        public Transform leftHandTarget, rightHandTarget;

        [Header("States")]
        [Tooltip("Nearly stopped for this long (s) = resting.")]
        public float restAfter = 3f;
        [Tooltip("Below this speed (m/s) the kayak counts as nearly stopped.")]
        public float restSpeed = 0.4f;
        [Tooltip("Current (m/s) above which the paddler is alert and braces.")]
        public float rapidsCurrent = 4.8f;
        [Tooltip("Steer input above which the rudder stroke is used.")]
        [Range(0f, 1f)] public float steerThreshold = 0.25f;

        [Header("Visual-only strokes")]
        [Tooltip("Seconds between light idle correction strokes while drifting (random range).")]
        public Vector2 idleStrokeInterval = new Vector2(5f, 10f);
        [Range(0.1f, 1f)] public float idleStrokeScale = 0.45f;
        [Tooltip("Seconds between short bracing strokes in rapids.")]
        public float braceInterval = 0.9f;

        [Header("Torso")]
        [Tooltip("Lean into turns: degrees per degree/second of turning.")]
        public float turnLean = 0.12f;
        [Tooltip("Lean from steering input alone (degrees).")]
        public float steerLean = 6f;
        [Tooltip("Forward reach during the pull of a stroke (degrees).")]
        public float strokeLean = 7f;
        [Tooltip("Share of the paddle yaw the torso follows (power comes from the torso).")]
        [Range(0f, 1.2f)] public float torsoFollow = 0.75f;
        [Tooltip("Forward lean when alert in rapids (degrees).")]
        public float alertLean = 8f;
        public float maxLean = 16f;
        public float leanSmoothness = 6f;

        [Header("Air, landing, bumps")]
        [Tooltip("Lean back on the descent (degrees). A slight crouch forward on the way up.")]
        public float airLeanBack = 9f;
        [Tooltip("Spine dip at a hard landing (degrees) and how fast it recovers (1/s).")]
        public float landingDip = 14f;
        public float landingRecovery = 5f;
        [Tooltip("Flinch toward the impact side at a full-strength bump (degrees).")]
        public float bumpFlinch = 11f;

        [Header("Rest")]
        public Vector2 stretchInterval = new Vector2(8f, 14f);
        public float stretchDuration = 2.4f;
        [Range(0f, 1f)] public float trailHandChance = 0.6f;
        public Vector2 trailDuration = new Vector2(4f, 7f);

        [Header("Looking")]
        public float lookRange = 25f;
        [Tooltip("Targets further than this from straight ahead (degrees) are ignored.")]
        public float maxLookAngle = 110f;
        public float lookSmoothness = 2.5f;
        [Tooltip("Idle glances left/right (degrees).")]
        public float lookAround = 35f;
        [Range(0f, 1f)] public float lookWeight = 0.85f;
        [Tooltip("Other paddlers look at this one (a LookTarget on the kayak, created at runtime). 0 = off.")]
        public float friendInterest = 1.4f;
        public float friendRadius = 14f;

        [Header("Spin-outs and capsizes")]
        [Tooltip("Head whip toward the spin (degrees) and the lean against it (degrees).")]
        public float spinOutLook = 75f;
        public float spinOutLean = 10f;
        [Tooltip("Surprised lean back while going over, tuck forward while upside down (degrees).")]
        public float capsizeLeanBack = 12f;
        public float capsizedTuck = 16f;
        [Tooltip("Hip snap side lean during the roll-up (degrees).")]
        public float hipSnap = 14f;
        [Tooltip("Shaking off the water after coming up: how long (s) and how hard (degrees).")]
        public float recoveredDuration = 1.3f;
        public float shakeAngle = 35f;
        [Tooltip("Hat bob while upside down (m).")]
        public float hatBob = 0.03f;

        [Header("Tricks (Stage 8)")]
        [Tooltip("Tuck forward and lean into a flat spin in the air (degrees); lean into a carve (degrees).")]
        public float spinTuck = 10f;
        public float spinLean = 7f;
        public float carveLean = 9f;

        public PaddlerState State { get; private set; }
        public LookTarget CurrentLookTarget { get; private set; }

        float stillTime, idleTimer, braceTimer, stretchTimer, stretchTime = -1f, trailTime = -1f, trailWeight;
        float lean, sideLean, landing, flinch, flinchSide = 1f;
        int idleSide = 1;
        bool lastStrokeBack;
        float stateTime, recoveredAt = -10f;
        int rollSide = 1;
        PaddlerSecondaryMotion secondary;
        float lookScan;
        Vector3 lookPoint;
        bool lookInitialized;

        void Awake()
        {
            if (!kayak) kayak = GetComponentInParent<KayakController>();
            secondary = kayak ? kayak.GetComponentInChildren<PaddlerSecondaryMotion>() : null;
            idleTimer = Random.Range(idleStrokeInterval.x, idleStrokeInterval.y);
            stretchTimer = Random.Range(stretchInterval.x, stretchInterval.y);
            if (kayak && friendInterest > 0f && Application.isPlaying)
            {
                var friend = new GameObject("Friend Look Target").AddComponent<LookTarget>();
                friend.transform.SetParent(kayak.transform, false);
                friend.offset = Vector3.up * 0.9f;
                friend.interest = friendInterest;
                friend.radius = friendRadius;
            }
        }

        void OnEnable()
        {
            if (!kayak) return;
            kayak.OnLand += OnLanded;
            kayak.OnImpact += OnImpact;
            kayak.OnStroke += OnStroked;
            kayak.OnRecovered += OnRecovered;
        }

        void OnDisable()
        {
            if (!kayak) return;
            kayak.OnLand -= OnLanded;
            kayak.OnImpact -= OnImpact;
            kayak.OnStroke -= OnStroked;
            kayak.OnRecovered -= OnRecovered;
        }

        void OnLanded(float airTime) => landing = Mathf.Max(landing, Mathf.Clamp01(kayak.LastLandingImpact / 3f + airTime * 0.5f));
        void OnStroked(int side, bool back) { lastStrokeBack = back; stillTime = 0f; }
        void OnRecovered() => recoveredAt = Time.time;

        void OnImpact(ImpactLevel level, float intensity, Vector3 point, Vector3 normal)
        {
            flinch = Mathf.Max(flinch, intensity);
            // The normal points away from what was hit: pointing right means the rock was on the left
            int side = Vector3.Dot(normal, kayak.transform.right) > 0f ? -1 : 1;
            flinchSide = side;
            // A quick brace stroke on the impact side
            if (paddle && !paddle.IsStroking) paddle.Play(side, false, paddle.braceScale);
        }

        void Update()
        {
            if (!kayak || !paddle || !rig) return;
            float dt = Time.deltaTime;
            UpdateState(dt);
            UpdatePaddle(dt);
            UpdateTorso(dt);
            UpdateHands(dt);
            UpdateLook(dt);
        }

        void UpdateState(float dt)
        {
            bool nearlyStopped = kayak.Speed < restSpeed && kayak.CurrentSpeed < restSpeed * 2f;
            stillTime = nearlyStopped && Mathf.Abs(kayak.Steer) < 0.1f ? stillTime + dt : 0f;

            var previous = State;
            State = ModeState();
            if (State != previous) { stateTime = 0f; OnStateEntered(State); }
            else stateTime += dt;
        }

        /// <summary>The capsize family comes straight from KayakMode; everything else from what the kayak is doing.</summary>
        PaddlerState ModeState()
        {
            switch (kayak.Mode)
            {
                case KayakMode.SpinOut: return PaddlerState.SpinOut;
                case KayakMode.Capsizing: return PaddlerState.Capsizing;
                case KayakMode.Capsized: return PaddlerState.Capsized;
                case KayakMode.Rolling: return PaddlerState.Rolling;
                case KayakMode.Recovering: if (Time.time - recoveredAt < recoveredDuration) return PaddlerState.Recovered; break;
            }
            return kayak.IsAirborne ? PaddlerState.Airborne
                : kayak.Activity == KayakActivity.Paddling ? (lastStrokeBack ? PaddlerState.BackPaddle : PaddlerState.Paddle)
                : stillTime > restAfter ? PaddlerState.Rest
                : Mathf.Abs(kayak.Steer) > steerThreshold ? PaddlerState.Steer
                : kayak.CurrentSpeed > rapidsCurrent ? PaddlerState.Rapids
                : PaddlerState.Drift;
        }

        /// <summary>One-off moves when a capsize-family state starts.</summary>
        void OnStateEntered(PaddlerState state)
        {
            switch (state)
            {
                case PaddlerState.SpinOut:
                    braceTimer = 0f; // brace right away (the impact already played one on the hit side)
                    break;
                case PaddlerState.Rolling:
                    // Hip snap + a big sweep of the paddle on the side it rolls up
                    rollSide = kayak.transform.InverseTransformDirection(Vector3.up).x > 0f ? 1 : -1;
                    paddle.Play(rollSide, false, 1.25f);
                    break;
            }
        }

        void UpdatePaddle(float dt)
        {
            switch (State)
            {
                case PaddlerState.Airborne: paddle.Target = paddle.lifted; break;
                case PaddlerState.Steer: paddle.Target = paddle.RudderPose(kayak.Steer); break;
                case PaddlerState.Rest:
                    stretchTimer -= dt;
                    if (stretchTime < 0f && stretchTimer <= 0f) { stretchTime = 0f; stretchTimer = Random.Range(stretchInterval.x, stretchInterval.y); }
                    if (stretchTime >= 0f)
                    {
                        stretchTime += dt;
                        // Stretch: the paddle goes up overhead and back down to the lap
                        var high = paddle.lifted; high.center += new Vector3(0f, 0.35f, -0.08f);
                        paddle.Target = stretchTime < stretchDuration * 0.6f ? high : paddle.rest;
                        if (stretchTime > stretchDuration) stretchTime = -1f;
                    }
                    else paddle.Target = paddle.rest;
                    break;
                case PaddlerState.Capsizing:
                    // A surprised reach: the paddle goes up and out
                    var reachUp = paddle.lifted; reachUp.center += new Vector3(0f, 0.3f, 0.12f);
                    paddle.Target = reachUp;
                    break;
                case PaddlerState.Capsized:
                    // Holding on and holding breath: tucked to the deck, paddle along the hull, ready to sweep
                    var tuck = paddle.rest; tuck.center += new Vector3(0f, -0.05f, 0.3f); tuck.yaw = 70f * rollSideGuess();
                    paddle.Target = tuck;
                    break;
                case PaddlerState.Recovered:
                    // Shake off the water, then a quick happy paddle-up
                    var cheer = paddle.lifted; cheer.center += new Vector3(0f, 0.35f, -0.05f);
                    paddle.Target = stateTime < recoveredDuration * 0.45f ? paddle.lifted : cheer;
                    break;
                default: paddle.Target = paddle.ready; break;
            }
            if (State == PaddlerState.SpinOut && !paddle.IsStroking)
            {
                braceTimer -= dt;
                if (braceTimer <= 0f) { braceTimer = braceInterval * 0.5f; idleSide = -idleSide; paddle.Play(idleSide, false, paddle.braceScale); }
            }

            if (paddle.IsStroking) return;
            if (State == PaddlerState.Drift)
            {
                idleTimer -= dt;
                if (idleTimer <= 0f)
                {
                    idleTimer = Random.Range(idleStrokeInterval.x, idleStrokeInterval.y);
                    idleSide = -idleSide;
                    paddle.Play(idleSide, false, idleStrokeScale);
                }
            }
            else if (State == PaddlerState.Rapids)
            {
                braceTimer -= dt;
                if (braceTimer <= 0f)
                {
                    braceTimer = braceInterval * Random.Range(0.8f, 1.25f);
                    idleSide = -idleSide;
                    paddle.Play(idleSide, false, paddle.braceScale);
                }
            }
        }

        void UpdateTorso(float dt)
        {
            float targetSide = Mathf.Clamp(kayak.YawRate * turnLean + kayak.Steer * steerLean, -maxLean, maxLean);
            float targetLean = 0f;
            if (paddle.IsStroking) targetLean += strokeLean * Mathf.Sin(Mathf.PI * Mathf.Clamp01(paddle.StrokePhase / paddle.exitAt));
            if (State == PaddlerState.Rapids) targetLean += alertLean;
            if (State == PaddlerState.Airborne) targetLean += kayak.Velocity.y < 0f ? -airLeanBack : airLeanBack * 0.3f;
            // Stage 8 tricks: tuck and lean into a flat spin; lean hard into a carve
            if (State == PaddlerState.Airborne && Mathf.Abs(kayak.YawRate) > 120f) { targetLean += spinTuck; targetSide += Mathf.Sign(kayak.YawRate) * spinLean; }
            if (kayak.Simulation is KayakPhysics skills && skills.Carving) targetSide += Mathf.Sign(kayak.Steer) * carveLean;
            if (State == PaddlerState.Rest) targetLean -= 3f;
            switch (State)
            {
                case PaddlerState.SpinOut:
                    targetSide += -Mathf.Sign(kayak.YawRate) * spinOutLean; // lean against the spin
                    targetLean += 5f;
                    break;
                case PaddlerState.Capsizing: targetLean -= capsizeLeanBack; break;
                case PaddlerState.Capsized: targetLean += capsizedTuck; break;
                case PaddlerState.Rolling:
                    float phase = Mathf.Clamp01(stateTime / 0.6f);
                    targetSide += rollSide * hipSnap * Mathf.Sin(phase * Mathf.PI); // the hip snap
                    targetLean += capsizedTuck * (1f - phase);
                    break;
                case PaddlerState.Recovered:
                    float fade = 1f - Mathf.Clamp01(stateTime / recoveredDuration);
                    targetSide += Mathf.Sin(stateTime * 22f) * 6f * fade; // shaking off the water
                    break;
            }
            if (secondary) secondary.extraOffset = State == PaddlerState.Capsized ? Vector3.up * (Mathf.Sin(Time.time * 2.6f) * hatBob) : Vector3.zero;

            landing = Mathf.Lerp(landing, 0f, 1f - Mathf.Exp(-landingRecovery * dt));
            flinch = Mathf.Lerp(flinch, 0f, 1f - Mathf.Exp(-4f * dt));
            targetLean += landing * landingDip;
            targetSide += flinchSide * flinch * bumpFlinch;

            float k = 1f - Mathf.Exp(-leanSmoothness * dt);
            lean = Mathf.Lerp(lean, Mathf.Clamp(targetLean, -maxLean, maxLean), k);
            sideLean = Mathf.Lerp(sideLean, Mathf.Clamp(targetSide, -maxLean, maxLean), k);
            rig.lean = lean;
            rig.sideLean = sideLean;
            rig.twist = paddle.Twist * torsoFollow;
        }

        void UpdateHands(float dt)
        {
            // Resting: sometimes trail the right hand in the water while the left keeps the paddle on the lap
            if (State == PaddlerState.Rest && rig.handTrail)
            {
                if (trailTime < 0f && stretchTime < 0f && Random.value < trailHandChance * dt * 0.3f) trailTime = Random.Range(trailDuration.x, trailDuration.y);
            }
            else trailTime = -1f;
            if (trailTime >= 0f) trailTime -= dt;
            trailWeight = Mathf.MoveTowards(trailWeight, trailTime > 0f ? 1f : 0f, dt / 0.8f);

            if (leftHandTarget && rig.leftGrip) leftHandTarget.SetPositionAndRotation(rig.leftGrip.position, rig.leftGrip.rotation);
            if (rightHandTarget && rig.rightGrip)
            {
                Vector3 grip = rig.rightGrip.position;
                Vector3 trail = rig.handTrail ? rig.handTrail.position + Vector3.up * Mathf.Sin(Time.time * 1.7f) * 0.02f : grip;
                rightHandTarget.SetPositionAndRotation(Vector3.Lerp(grip, trail, Smooth(trailWeight)), rig.rightGrip.rotation);
            }
        }

        void UpdateLook(float dt)
        {
            if (!rig.lookTarget || !rig.Head) return;
            Vector3 head = rig.Head.position;
            Vector3 forward = Flat(kayak.transform.forward).normalized;

            lookScan -= dt;
            if (lookScan <= 0f) { lookScan = 0.5f; CurrentLookTarget = BestTarget(head, forward); }

            Vector3 desired;
            if (State == PaddlerState.SpinOut)
                desired = head + Quaternion.AngleAxis(Mathf.Sign(kayak.YawRate) * spinOutLook, Vector3.up) * forward * 8f; // head whips around
            else if (State == PaddlerState.Capsizing || State == PaddlerState.Capsized || State == PaddlerState.Rolling)
                desired = head + kayak.transform.forward * 6f; // eyes along the hull, whichever way up
            else if (State == PaddlerState.Recovered && stateTime < recoveredDuration * 0.5f)
                desired = head + Quaternion.AngleAxis(Mathf.Sin(stateTime * 24f) * shakeAngle, Vector3.up) * forward * 8f; // shaking the head
            else if (CurrentLookTarget) desired = CurrentLookTarget.Point;
            else if (State == PaddlerState.Rapids || State == PaddlerState.Airborne)
                desired = head + (kayak.Speed > 0.5f ? Flat(kayak.Velocity).normalized : forward) * 12f - Vector3.up * 1.5f; // eyes on the line
            else
            {
                float amount = State == PaddlerState.Rest ? lookAround * 1.6f : lookAround;
                float glance = (Mathf.PerlinNoise(Time.time * 0.12f, 7.3f) - 0.5f) * 2f * amount;
                float tilt = (Mathf.PerlinNoise(Time.time * 0.09f, 1.9f) - 0.5f) * 10f + (State == PaddlerState.Rest ? 6f : 0f);
                desired = head + Quaternion.AngleAxis(glance, Vector3.up) * Quaternion.AngleAxis(-tilt, Vector3.Cross(Vector3.up, forward)) * forward * 10f;
            }
            if (!lookInitialized) { lookPoint = desired; lookInitialized = true; }
            lookPoint = Vector3.Lerp(lookPoint, desired, 1f - Mathf.Exp(-lookSmoothness * dt));
            rig.lookTarget.position = lookPoint;
            rig.lookWeight = lookWeight;
        }

        LookTarget BestTarget(Vector3 head, Vector3 forward)
        {
            LookTarget best = null;
            float bestScore = 0f;
            foreach (var t in LookTarget.All)
            {
                Vector3 to = t.Point - head;
                float d = to.magnitude, range = Mathf.Min(t.radius, lookRange);
                if (d > range || d < 1f) continue;
                if (t.transform.IsChildOf(kayak.transform)) continue; // not our own friend marker
                if (Vector3.Angle(forward, Flat(to)) > maxLookAngle) continue;
                float score = t.interest * (1f - d / range) * (t == CurrentLookTarget ? 1.3f : 1f); // a little stickiness
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        /// <summary>The side the kayak will roll up on: where world up points, seen from the kayak.</summary>
        float rollSideGuess() => kayak.transform.InverseTransformDirection(Vector3.up).x > 0f ? 1f : -1f;

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
        static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
