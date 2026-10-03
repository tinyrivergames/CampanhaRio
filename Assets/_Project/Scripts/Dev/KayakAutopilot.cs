using CampanhaRio.Kayak;
using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Dev
{
    public enum AutopilotState { Off, Cruise, ToRest, Resting, Leaving, ToEddy, InEddy }

    /// <summary>Bot personality. Default = the Ride-Along pilot on the player's kayak.</summary>
    public enum AutopilotProfile { Default, Careful, Normal, Risky }

    /// <summary>
    /// DEBUG + bots: a simple pilot that steers the kayak down the river. It is just another IKayakInputSource
    /// (the same KayakInputState the player produces), so rides test the real controls. F2 toggles it on the local kayak.
    ///   Cruise:  follows the fastest clear line a few meters ahead (the flow field decides where that is),
    ///            with a gentle weave, and taps a stroke now and then when slow.
    ///   Rest:    once per run, paddles into the rest spot (auto: the Backwater zone), sits still, then paddles out.
    /// </summary>
    public class KayakAutopilot : MonoBehaviour, IKayakInputSource
    {
        public KayakController kayak;
        [Tooltip("Start with the autopilot on (Ride-Along captures turn it on themselves).")]
        public bool enableOnStart;
        [Tooltip("Bot personality (Careful / Normal / Risky set by the bot spawner).")]
        public AutopilotProfile profile = AutopilotProfile.Default;
        [Tooltip("Keep this far from rocks when picking a line (m).")]
        public float obstacleClearance = 2.2f;
        [Tooltip("Keep this far from other kayaks when picking a line (m).")]
        public float kayakClearance = 3.5f;
        [Tooltip("Together: how much a line right behind a kayak 3-10 m ahead is worth (its draft), and how close behind it swings out to pass (m).")]
        public float draftSeek = 3f;
        public float passDistance = 3.5f;
        [Tooltip("Line score per unit of river feature under it: + seeks the ledge and wave train, - avoids them.")]
        public float featureBias = 0f;
        [Tooltip("Hold Paddle whenever the current is strong (the risky line).")]
        public bool paddleInRapids;
        [Tooltip("While paddling in the rapids: seconds to wait after each stroke (0 = every beat, the sprint).")]
        public float rapidsStrokeCooldown = 0f;
        [Tooltip("Stuck: less than this far down the river (m) in this long (s) while cruising = paddle out for the main current.")]
        public float stuckProgress = 8f;
        public float stuckTime = 5f;
        float progressTimer, progressFrom, unstickLeft;
        [Tooltip("Back-paddle above this speed (m/s, 0 = never): the careful line.")]
        public float brakeAbove = 0f;
        [Tooltip("Chance of timing the eskimo roll right after a capsize.")]
        [Range(0f, 1f)] public float rollSkill = 0.6f;
        [Tooltip("Stage 8 verbs: chance to time a stroke into the perfect window, to boof at a lip; boost once the meter reaches this (0 = never) in at least this current (m/s).")]
        [Range(0f, 1f)] public float rhythmSkill = 0.6f;
        [Range(0f, 1f)] public float boofSkill = 0.5f;
        public float boostAtMeter = 70f;
        public float boostMinCurrent = 5f;

        [Header("Line")]
        [Tooltip("How far ahead the line is chosen (m).")]
        public float lookAhead = 14f;
        [Tooltip("Lateral candidates scanned across the river.")]
        [Range(3, 15)] public int candidates = 9;
        [Tooltip("Stay this far from the banks (m).")]
        public float bankMargin = 3f;
        [Tooltip("Weave amplitude around the chosen line (m) and its wavelength along the river (m).")]
        public float weaveAmplitude = 1.5f;
        public float weaveWavelength = 70f;
        [Tooltip("Score penalty per meter of line change (keeps it from zig-zagging).")]
        public float changePenalty = 0.08f;
        [Tooltip("Layers treated as obstacles to steer around.")]
        public LayerMask obstacleLayers;

        [Header("Steering")]
        [Tooltip("Steer per meter of lateral error.")]
        public float lateralGain = 0.35f;
        [Tooltip("Steer damping per m/s of lateral speed.")]
        public float lateralDamping = 0.3f;
        [Tooltip("Steer per degree of heading error (still water).")]
        public float headingGain = 0.025f;

        [Header("Paddling")]
        [Tooltip("Tap a stroke when slower than this over ground (m/s).")]
        public float cruiseSpeed = 2.2f;
        [Tooltip("Seconds between strokes while cruising slow.")]
        public float strokeInterval = 0.9f;
        [Tooltip("Occasional stroke burst in normal water every this many seconds (to exercise the animation).")]
        public float burstInterval = 18f;

        [Header("Eddies")]
        [Tooltip("Now and then, catch the eddy behind a rock: pass it, turn into the pocket, park, peel out.")]
        public bool catchEddies = true;
        [Tooltip("Seconds between eddy attempts.")]
        public float eddyInterval = 40f;
        [Tooltip("Seconds parked in the eddy.")]
        public float eddyHold = 5f;

        [Header("Rest stop")]
        public bool restOnce = true;
        [Tooltip("Where to rest (auto: the Backwater CurrentZone).")]
        public Transform restSpot;
        [Tooltip("Start heading for the rest spot this many meters before it (along the river).")]
        public float approachDistance = 45f;
        public float restDuration = 9f;
        [Tooltip("Rest is reached within this distance (m).")]
        public float restRadius = 4f;

        public AutopilotState State { get; private set; } = AutopilotState.Off;
        public bool Active => State != AutopilotState.Off;
        public string ProfileName => profile.ToString();

        KayakInput input;
        RiverPath river;
        float targetLateral, stateTimer, strokeTimer, burstTimer;
        bool pendingTap, pendingRoll, pendingBoost, rested;
        float steer; bool paddleHeld, backPaddleHeld;
        float eddyTimer = 15f;
        FlowObstacle eddy;
        FlowObstacle[] obstacles;
        int eddySide = 1;

        /// <summary>One physics step of bot intent (the same struct the player's devices produce).</summary>
        public KayakInputState ReadInput()
        {
            var state = new KayakInputState { steer = steer, paddle = paddleHeld || pendingTap, backPaddle = backPaddleHeld, roll = pendingRoll, boost = pendingBoost };
            pendingTap = pendingRoll = pendingBoost = false;
            return state;
        }

        void Awake()
        {
            input = GetComponent<KayakInput>();
            if (!kayak) kayak = GetComponent<KayakController>();
            if (obstacleLayers.value == 0) obstacleLayers = LayerMask.GetMask("Obstacle");
        }

        void Start()
        {
            ApplyProfile();
            if (enableOnStart) SetActive(true);
        }

        void OnEnable() { if (kayak) kayak.Respawned += OnRespawned; }
        void OnDisable()
        {
            if (kayak) kayak.Respawned -= OnRespawned;
            if (kayak && ReferenceEquals(kayak.InputSource, this)) kayak.ResetInputSource();
        }

        void OnRespawned() { rested = false; if (Active) State = AutopilotState.Cruise; }

        public void SetActive(bool on)
        {
            State = on ? AutopilotState.Cruise : AutopilotState.Off;
            if (on) kayak.InputSource = this;
            else if (ReferenceEquals(kayak.InputSource, this)) kayak.ResetInputSource();
            steer = 0f; paddleHeld = false; pendingTap = false;
            if (on && kayak) targetLateral = kayak.RiverSample.lateralOffset;
        }

        void Update()
        {
            if (input && input.ToggleAutopilotPressed) SetActive(!Active);
            if (!Active || !kayak) return;
            if (!river) river = kayak.river;
            if (!river) return;
            if (!restSpot) restSpot = FindRestSpot();

            float dt = Time.deltaTime;
            stateTimer += dt;
            var here = kayak.RiverSample;
            paddleHeld = false;
            HandleCapsize();
            backPaddleHeld = false;

            switch (State)
            {
                case AutopilotState.Cruise:
                    Cruise(here, dt);
                    eddyTimer -= dt;
                    if (catchEddies && eddyTimer <= 0f && kayak.CurrentSpeed < 4f && (eddy = EddyAhead(here)) != null) { eddyTimer = eddyInterval; Enter(AutopilotState.ToEddy); }
                    if (restOnce && !rested && restSpot)
                    {
                        float restAlong = river.Sample(restSpot.position).distanceAlong;
                        if (here.distanceAlong > restAlong - approachDistance && here.distanceAlong < restAlong) Enter(AutopilotState.ToRest);
                    }
                    break;

                case AutopilotState.ToRest:
                    GoTo(restSpot.position, 0.6f);
                    if (Flat(restSpot.position - kayak.transform.position).magnitude < restRadius || stateTimer > 40f) Enter(AutopilotState.Resting);
                    break;

                case AutopilotState.Resting:
                    steer = 0f;
                    if (stateTimer > restDuration) { rested = true; Enter(AutopilotState.Leaving); }
                    break;

                case AutopilotState.Leaving:
                    var exit = river.GetPointAtDistance(river.Sample(restSpot.position).distanceAlong + 30f);
                    GoTo(exit.point, 1.2f);
                    if (kayak.CurrentSpeed > 0.8f || stateTimer > 30f) Enter(AutopilotState.Cruise);
                    break;

                case AutopilotState.ToEddy:
                    ApproachEddy(here);
                    if (eddy.Weight(kayak.transform.position) > 0.35f) Enter(AutopilotState.InEddy);
                    else if (stateTimer > 20f || here.distanceAlong > river.Sample(eddy.transform.position).distanceAlong + eddy.PocketLength + 4f) Enter(AutopilotState.Cruise);
                    break;

                case AutopilotState.InEddy:
                    if (stateTimer <= eddyHold) HoldEddy(); // parked: the eddy grip holds the kayak, gentle strokes keep it in the pocket
                    else
                    {
                        // Peel out: paddle across the eddy line toward the main current
                        var peel = river.GetPointAtDistance(here.distanceAlong + 10f);
                        GoTo(peel.point, 0.7f);
                        if (kayak.CurrentSpeed > 2f || stateTimer > eddyHold + 8f) Enter(AutopilotState.Cruise);
                    }
                    break;
            }
            // Never circle: cruising less than Stuck Progress m down the river in Stuck Time s (a recirculating eddy, a pile-up)
            // means paddling hard for the main current a little downstream
            if (State == AutopilotState.Cruise && !kayak.Held)
            {
                progressTimer += dt;
                if (progressTimer >= stuckTime)
                {
                    if (here.distanceAlong - progressFrom < stuckProgress) unstickLeft = 3f;
                    progressFrom = here.distanceAlong; progressTimer = 0f;
                }
            }
            else { progressTimer = 0f; progressFrom = here.distanceAlong; }
            if (unstickLeft > 0f)
            {
                unstickLeft -= dt;
                var out_ = river.GetPointAtDistance(here.distanceAlong + 12f);
                GoTo(out_.point + out_.right * here.thalweg, 0.35f);
                backPaddleHeld = false;
            }
            UseSkills();
            AvoidKayaks(); // in every state: never ram a friend
            if (kayak.IsAirborne) steer = 0f; // no accidental spins: bots only steer on the water
        }

        /// <summary>Bot personalities: only line choice, paddling and roll timing differ (the same controls as a player).</summary>
        void ApplyProfile()
        {
            if (profile != AutopilotProfile.Default) restOnce = false; // bots leave the rest spot to the player
            switch (profile)
            {
                case AutopilotProfile.Careful:
                    obstacleClearance = 3.2f; featureBias = -3f; brakeAbove = 5.2f; cruiseSpeed = 1.8f;
                    weaveAmplitude = 0.8f; lateralGain = 0.3f; rollSkill = 0.75f;
                    rhythmSkill = 0.3f; boofSkill = 0f; boostAtMeter = 0f; draftSeek = 1.5f;
                    break;
                case AutopilotProfile.Normal:
                    rollSkill = 0.55f; rhythmSkill = 0.6f; boofSkill = 0.5f; boostAtMeter = 85f;
                    paddleInRapids = true; rapidsStrokeCooldown = 1.5f; cruiseSpeed = 3f; catchEddies = false; // steady strokes through the rapids, no eddy stops (Risky sprints)
                    break;
                case AutopilotProfile.Risky:
                    obstacleClearance = 0.9f; featureBias = 4f; paddleInRapids = true; cruiseSpeed = 3.2f;
                    weaveAmplitude = 2.6f; lateralGain = 0.6f; lateralDamping = 0.15f; rollSkill = 0.4f;
                    rhythmSkill = 0.85f; boofSkill = 0.9f; boostAtMeter = 35f; boostMinCurrent = 7f; // boosts in the chutes, boofs the ledges
                    catchEddies = false; restOnce = false; draftSeek = 4f;
                    break;
            }
        }

        bool rollDecided, rollWillHit;
        float rollMissAt;

        /// <summary>Upside down: press Roll once, in the sweet spot if this capsize is one it gets right.</summary>
        void HandleCapsize()
        {
            if (!(kayak.Simulation is KayakPhysics physics) || kayak.Mode != KayakMode.Capsized) { rollDecided = false; return; }
            if (!rollDecided)
            {
                rollDecided = true;
                rollWillHit = Random.value < rollSkill;
                rollMissAt = Random.value < 0.5f ? Random.Range(0.02f, physics.rollSweetSpot.x - 0.08f) : Random.Range(physics.rollSweetSpot.y + 0.05f, 0.98f);
            }
            float ring = physics.RollRing;
            if (ring < 0f) return;
            float pressAt = rollWillHit ? (physics.rollSweetSpot.x + physics.rollSweetSpot.y) * 0.5f : rollMissAt;
            if (ring >= pressAt) { pendingRoll = true; rollMissAt = 2f; rollWillHit = false; } // one press per capsize
        }
        void Enter(AutopilotState s) { State = s; stateTimer = 0f; }

        /// <summary>Follow the fastest clear line ahead.</summary>
        void Cruise(RiverPath.RiverSample here, float dt)
        {
            var ahead = river.GetPointAtDistance(here.distanceAlong + lookAhead);
            float best = targetLateral, bestScore = float.MinValue;
            bool bestDrafts = false;
            float min = -ahead.leftWidth + bankMargin, max = ahead.rightWidth - bankMargin;
            for (int i = 0; i < candidates; i++)
            {
                float lateral = Mathf.Lerp(min, max, i / (candidates - 1f));
                Vector3 p = ahead.point + ahead.right * lateral;
                var s = river.Sample(p);
                float score = s.waterVelocity.magnitude - Mathf.Abs(lateral - targetLateral) * changePenalty;
                if (Physics.CheckSphere(p, obstacleClearance, obstacleLayers, QueryTriggerInteraction.Ignore)) score -= 10f;
                if (featureBias != 0f && river.FeatureAt(p, out float fw) is RiverFeature rf && rf.type != RiverFeatureType.Riffle) score += featureBias * fw;
                float crowding = KayakCrowding(lateral, here.distanceAlong);
                score -= crowding;
                if (score > bestScore) { bestScore = score; best = lateral; bestDrafts = crowding < 0f; }
            }
            weaveScale = Mathf.MoveTowards(weaveScale, bestDrafts ? 0f : 1f, dt); // tucked into a draft: hold the line
            float weave = Mathf.Sin(here.distanceAlong / Mathf.Max(weaveWavelength, 1f) * Mathf.PI * 2f) * weaveAmplitude * weaveScale;
            targetLateral = Mathf.Lerp(targetLateral, Mathf.Clamp(best + weave, min, max), 1f - Mathf.Exp(-1.5f * dt));

            float lateralSpeed = Vector3.Dot(kayak.Velocity, here.right);
            float lateralSteer = Mathf.Clamp((targetLateral - here.lateralOffset) * lateralGain - lateralSpeed * lateralDamping, -1f, 1f);
            // In slow water "right" turns the kayak: aim at the line instead
            float flow = Mathf.Clamp01(kayak.CurrentSpeed / 1.5f);
            Vector3 aim = ahead.point + ahead.right * targetLateral;
            steer = Mathf.Lerp(HeadingSteer(aim), lateralSteer, flow);

            strokeTimer -= dt;
            burstTimer += dt;
            bool slow = kayak.Speed < cruiseSpeed;
            bool burst = burstTimer > burstInterval && burstTimer < burstInterval + 3f;
            if (burstTimer > burstInterval + 3f) burstTimer = 0f;
            if ((slow || burst) && strokeTimer <= 0f && !kayak.IsAirborne) RhythmTap(strokeInterval);
            if (paddleInRapids && kayak.CurrentSpeed > 3f && !kayak.IsAirborne) RhythmTap(rapidsStrokeCooldown); // keeps paddling in the rapids, on the rhythm
            if (brakeAbove > 0f && kayak.Speed > brakeAbove) backPaddleHeld = true;     // Careful: checks the speed
        }



        bool rhythmWait, boofDecided;

        /// <summary>A stroke press; with Rhythm Skill it waits for the timing window (a perfect stroke), otherwise it goes now.</summary>
        void RhythmTap(float cooldown)
        {
            if (strokeTimer > 0f) return;
            if (!rhythmWait) rhythmWait = Random.value < rhythmSkill;
            if (rhythmWait && kayak.State.strokePhase < 0.9f) return; // wait for the window
            pendingTap = true;
            rhythmWait = false;
            strokeTimer = cooldown;
        }

        /// <summary>The Stage 8 verbs a bot uses: boofs at lips and boosts in fast water (same inputs as a player).</summary>
        void UseSkills()
        {
            if (!(kayak.Simulation is KayakPhysics physics)) return;
            if (physics.BoofWindow && !boofDecided)
            {
                boofDecided = true;
                if (Random.value < boofSkill) pendingTap = true;
            }
            if (!physics.BoofWindow) boofDecided = false;
            if (boostAtMeter > 0f && kayak.Meter >= Mathf.Max(boostAtMeter, physics.boostCost) && kayak.CurrentSpeed >= boostMinCurrent && !physics.Boosting)
                pendingBoost = true;
        }
        /// <summary>A friend right ahead on our path (moving or parked): ease off and slip past on the roomier side (no ramming).</summary>
        void AvoidKayaks()
        {
            Vector3 v = Flat(kayak.Velocity);
            if (v.sqrMagnitude < 0.25f) return;
            Vector3 dir = v.normalized, side = new Vector3(dir.z, 0f, -dir.x);
            foreach (var other in KayakRegistry.All)
            {
                if (!other || other == kayak) continue;
                Vector3 to = Flat(other.transform.position - kayak.transform.position);
                float ahead = Vector3.Dot(to, dir), across = Vector3.Dot(to, side);
                if (ahead < 0f || ahead > 4.5f || Mathf.Abs(across) > 2f) continue; // further back is the draft: welcome
                float closing = Vector3.Dot(v - Flat(other.Velocity), dir);
                if (closing > 2f && ahead < 3.5f) { backPaddleHeld = true; paddleHeld = false; pendingTap = false; }
                steer = Mathf.Clamp(steer + (across > 0f ? -0.6f : 0.6f), -1f, 1f);
            }
        }
        /// <summary>
        /// Friends take up room too: a line through a kayak just ahead (the next few meters) scores lower. Further ahead
        /// (Draft Seek > 0), the line right behind it scores higher: the bot tucks into the draft, closes up in the pull, and
        /// swings out to pass once it is on the stern (the leapfrog).
        /// </summary>
        float weaveScale = 1f;

        float KayakCrowding(float lateral, float along)
        {
            float penalty = 0f;
            foreach (var other in KayakRegistry.All)
            {
                if (!other || other == kayak) continue;
                var s = other.RiverSample;
                float ahead = s.distanceAlong - along;
                if (ahead < -2f || ahead > 15f) continue;
                float d = Mathf.Abs(s.lateralOffset - lateral);
                if (ahead < passDistance) { if (d < kayakClearance) penalty += 4f * (1f - d / kayakClearance); }
                else if (draftSeek > 0f && ahead < 15f && d < 1.5f && other.Speed > 2.5f) penalty -= draftSeek * (1f - d / 1.5f);
            }
            return penalty;
        }

        /// <summary>Another kayak is in (or heading into) this eddy pocket.</summary>
        bool Occupied(FlowObstacle o)
        {
            foreach (var other in KayakRegistry.All)
                if (other && other != kayak && Flat(other.transform.position - o.transform.position).magnitude < o.PocketLength + o.Radius) return true;
            return false;
        }

        FlowObstacle EddyAhead(RiverPath.RiverSample here)
        {
            FlowObstacle best = null;
            float bestAhead = float.MaxValue;
            obstacles ??= FindObjectsByType<FlowObstacle>(); // the rocks don't change during a ride
            foreach (var o in obstacles)
            {
                var s = river.Sample(o.transform.position);
                float ahead = s.distanceAlong - here.distanceAlong;
                if (ahead < 12f || ahead > 35f || s.edgeDistance > -1f) continue;
                if (Occupied(o)) continue; // someone is already parked there
                if (ahead < bestAhead) { bestAhead = ahead; best = o; eddySide = s.lateralOffset > 0f ? -1 : 1; }
            }
            return best;
        }

        /// <summary>
        /// Catch an eddy like a kayaker: drop past the rock on its open side, then ferry across the eddy line at an angle
        /// into the slack water below it (full sideways steer + a few strokes to punch through the shear).
        /// </summary>
        void ApproachEddy(RiverPath.RiverSample here)
        {
            var rock = river.Sample(eddy.transform.position);
            float radius = eddy.Radius, pocket = eddy.PocketLength;
            float behind = here.distanceAlong - rock.distanceAlong;
            float lateralSpeed = Vector3.Dot(kayak.Velocity, here.right);
            if (behind < -radius)
            {
                // Beside the rock, out in the current
                float target = rock.lateralOffset + eddySide * (radius + 1.6f);
                steer = Mathf.Clamp((target - here.lateralOffset) * lateralGain - lateralSpeed * lateralDamping, -1f, 1f);
            }
            else
            {
                // Just below it: angle into the pocket and ferry across the line, back-paddling to shed downstream speed
                steer = Mathf.Clamp((rock.lateralOffset - here.lateralOffset) * 1.2f, -1f, 1f);
                backPaddleHeld = kayak.Speed > 1.2f;
            }
        }

        /// <summary>Hold in the eddy: point upstream at the top of the pocket, a gentle stroke when drifting down.</summary>
        void HoldEddy()
        {
            var rock = river.Sample(eddy.transform.position);
            var here = kayak.RiverSample;
            float along = Vector3.Dot(Flat(kayak.transform.position - eddy.transform.position), rock.direction);
            // Stay on the pocket's line; back-paddle when sliding toward its tail
            steer = Mathf.Clamp((rock.lateralOffset - here.lateralOffset) * 0.6f, -0.6f, 0.6f);
            backPaddleHeld = along > eddy.PocketLength * 0.45f && kayak.Speed > 0.3f;
        }

        /// <summary>Head for a point: turn toward it and paddle while it's ahead.</summary>
        void GoTo(Vector3 target, float strokeEvery)
        {
            steer = HeadingSteer(target);
            float error = Mathf.Abs(Vector3.SignedAngle(Flat(kayak.transform.forward), Flat(target - kayak.transform.position), Vector3.up));
            strokeTimer -= Time.deltaTime;
            if (error < 50f && strokeTimer <= 0f) { pendingTap = true; strokeTimer = strokeEvery; }
        }

        float HeadingSteer(Vector3 target)
        {
            float error = Vector3.SignedAngle(Flat(kayak.transform.forward), Flat(target - kayak.transform.position), Vector3.up);
            return Mathf.Clamp(error * headingGain, -1f, 1f);
        }

        static Transform FindRestSpot()
        {
            foreach (var zone in FindObjectsByType<CurrentZone>())
                if (zone.type == CurrentZoneType.Backwater) return zone.transform;
            return null;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    }
}
