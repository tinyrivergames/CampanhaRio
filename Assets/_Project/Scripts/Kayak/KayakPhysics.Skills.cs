using System.Collections.Generic;
using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Kayak
{
    /// <summary>Air tricks: Grounded = flat spins only (default); Exaggerated adds barrel rolls with the Roll button.</summary>
    public enum TrickStyle { Grounded, Exaggerated }

    /// <summary>
    /// The skill verbs (Stage 8: speed is earned). Reading the water and paddling well fill the boost meter and make the kayak
    /// faster; Boost spends it. Everything here is input → forces, so it runs in the simulation like the rest (deterministic,
    /// owner-simulated later). Each verb raises KayakController.OnSkill for the score, the HUD and the FX.
    ///   Fast line:  on the thalweg in fast water: meter + a slight speed edge.
    ///   Perfect stroke: pressing Paddle in the timing window of the stroke cycle = a stronger stroke; mashing = weaker ones.
    ///   Eddy slingshot: carve around inside an eddy to charge, exit across the eddy line for a kick (perfect angle = bonus).
    ///   Carve: Back-paddle + steer on fast water carves hard (meter); releasing gives a small kick.
    ///   Boof: Paddle right at a lip: more air, a flatter landing. In the air steer = flat spin; clean landings pay.
    ///   Near miss: passing very close to a rock or log at speed without touching.
    /// </summary>
    public partial class KayakPhysics
    {
        [Header("Boost (Stage 8)")]
        [Tooltip("Meter spent per boost (of 100), how long it pushes (s), and how hard (m/s², up to the track's top speed).")]
        public float boostCost = 35f;
        public float boostDuration = 1.5f;
        public float boostAcceleration = 7f;

        [Header("Fast line")]
        [Tooltip("On the fast line: within this distance of the thalweg (m), in at least this current (m/s).")]
        public float fastLineWidth = 1.3f;
        public float fastLineMinCurrent = 3.5f;
        [Tooltip("Meter per second on the fast line, and the small speed edge it gives (m/s²).")]
        public float fastLineMeterRate = 1.5f;
        public float fastLineEdge = 0.35f;

        [Header("Perfect stroke")]
        [Tooltip("A press this far into the stroke cycle (share of the stroke interval since the last stroke) is a perfect stroke.")]
        public Vector2 perfectWindow = new Vector2(0.85f, 1.3f);
        [Tooltip("Stroke power for a perfect stroke, and for an early (mashed) press.")]
        public float perfectPower = 1.35f;
        public float mashPower = 0.75f;
        public float perfectStrokeMeter = 6f;

        [Header("Eddy slingshot")]
        [Tooltip("Seconds of carving inside an eddy to reach a full charge; turning slower than this (deg/s) doesn't charge.")]
        public float slingshotChargeTime = 2f;
        public float slingshotMinYaw = 25f;
        [Tooltip("Kick at full charge (m/s), how long after leaving the eddy it can be cashed in (s), and the meter at full charge.")]
        public float slingshotKick = 4.5f;
        public float slingshotExitWindow = 1.5f;
        public float slingshotMeter = 20f;
        [Tooltip("Exiting at this angle to the current (degrees) is a perfect exit, worth this much more.")]
        public Vector2 slingshotPerfectAngle = new Vector2(20f, 55f);
        public float slingshotPerfectBonus = 0.4f;

        [Header("Carve")]
        [Tooltip("Carving needs this much current (m/s), Back-paddle held this long (s) and at least this much steer.")]
        public float carveMinCurrent = 3f;
        public float carveHoldTime = 0.2f;
        [Range(0f, 1f)] public float carveMinSteer = 0.5f;
        [Tooltip("Extra turn rate while carving (deg/s), meter per second, and the kick on release (m/s per second carved, capped).")]
        public float carveYaw = 55f;
        public float carveMeterRate = 8f;
        public float carveKickPerSecond = 0.8f;
        public float carveMaxKick = 1.6f;

        [Header("Air")]
        public TrickStyle trickStyle = TrickStyle.Grounded;
        [Tooltip("Boof: the surface this far ahead (m) drops by at least this much (m) at a feature = a lip. Paddle there = boof.")]
        public float boofLookAhead = 1.8f;
        public float boofMinDrop = 0.25f;
        [Tooltip("The boof stroke's lift and forward push (m/s), and its meter.")]
        public float boofLift = 1.8f;
        public float boofForward = 1f;
        public float boofMeter = 8f;
        [Tooltip("Flat spin in the air: turn rate at full steer (deg/s) and how quickly it responds (1/s).")]
        public float airSpinRate = 420f;
        public float airSpinResponse = 8f;
        [Tooltip("With no steer in the air, turn toward the nearest travel-aligned heading at this rate (deg/s per degree of error).")]
        public float airAlignGain = 3f;
        [Tooltip("A spin counts when it ends within this many degrees of a half turn; meter per half turn.")]
        public float spinTolerance = 50f;
        public float spinMeterPerHalf = 8f;
        [Tooltip("Clean landing: rolled less than this and within this angle of the flow (degrees); its meter.")]
        public float cleanLandingRoll = 15f;
        public float cleanLandingAngle = 25f;
        public float cleanLandingMeter = 12f;
        [Tooltip("Sloppy landing (rolled or angled more than this, degrees): speed lost.")]
        public float sloppyLandingRoll = 22f;
        public float sloppyLandingAngle = 35f;
        [Range(0f, 0.5f)] public float sloppySpeedLoss = 0.12f;
        [Tooltip("Exaggerated style: a barrel roll takes this long (s).")]
        public float barrelRollDuration = 0.7f;
        public float barrelRollMeter = 15f;
        [Tooltip("Landings within this long (s) of a paid one are bounces of the same jump: no second clean-landing award.")]
        public float landingCooldown = 1.5f;
        float lastLandingAward = -10f;

        [Header("Near miss")]
        [Tooltip("Passing within this distance (m) of a rock or log above this speed (m/s) without touching it.")]
        public float nearMissDistance = 0.7f;
        public float nearMissMinSpeed = 5f;
        public float nearMissMeter = 5f;

        [Header("Drafting (together)")]
        [Tooltip("The draft zone behind any kayak moving faster than this (m/s): this long plus Draft Length Per Speed (m), this wide at the tail (m either side).")]
        public float draftMinSpeed = 2.5f;
        public float draftLength = 6f;
        [Tooltip("The draft reaches further behind a faster kayak: extra length per m/s of its speed (s).")]
        public float draftLengthPerSpeed = 0.7f;
        public float draftWidth = 1.8f;
        [Tooltip("At full draft: share of the water drag taken away, the pull toward the leader (m/s², up to the track's top speed), and the meter per second.")]
        [Range(0f, 1f)] public float draftDragCut = 0.6f;
        public float draftPull = 2.5f;
        public float draftMeterRate = 5f;

        /// <summary>0..1: how deep in another kayak's draft (the wake shimmer brightens, the meter fills).</summary>
        public float DraftShare { get; private set; }
        /// <summary>The kayak whose draft this one is in (null if none).</summary>
        public KayakController DraftLeader { get; private set; }
        float draftAccum;

        /// <summary>
        /// The draft: the calm, pulled water in the wake behind another kayak. Deepest right behind its stern, fading with
        /// distance and to the sides. Reads the others' positions, so it is the same for every client that sees them.
        /// </summary>
        /// <summary>How far behind a kayak moving at this speed its draft reaches (m).</summary>
        public float DraftLength(float speed) => draftLength + speed * draftLengthPerSpeed;

        float MeasureDraft(Vector3 position)
        {
            float best = 0f;
            KayakController leader = null;
            foreach (var k in KayakRegistry.All)
            {
                if (!k || k == kayak || !k.isActiveAndEnabled || k.IsAirborne) continue;
                Vector3 v = Flat(k.Velocity);
                float speed = v.magnitude;
                if (speed < draftMinSpeed) continue;
                Vector3 dir = v / speed;
                Vector3 d = Flat(position - k.transform.position);
                float behind = -Vector3.Dot(d, dir);
                float length = DraftLength(speed);
                if (behind < 1f || behind > length) continue;
                float lateral = Mathf.Abs(Vector3.Dot(d, new Vector3(dir.z, 0f, -dir.x)));
                float half = draftWidth * (0.6f + 0.4f * behind / length); // the wake widens a little
                if (lateral > half) continue;
                float share = Mathf.Sqrt(1f - behind / length) * (1f - Sq(lateral / half)) * Mathf.Clamp01((speed - draftMinSpeed) / 1.5f);
                if (share > best) { best = share; leader = k; }
            }
            DraftLeader = leader;
            return best;
        }

        void UpdateDraft(float dt, Vector3 forward, bool airborne)
        {
            if (airborne || DraftShare <= 0.05f) return; // short drafts add up: the score counts total seconds
            Vector3 toLeader = DraftLeader ? Flat(DraftLeader.transform.position - rb.position).normalized : forward;
            if (kayak.Speed < TopSpeed) rb.AddForce(Vector3.Lerp(forward, toLeader, 0.5f).normalized * (draftPull * DraftShare), ForceMode.Acceleration);
            kayak.AddMeter(draftMeterRate * DraftShare * dt);
            draftAccum += DraftShare * dt; // the score counts seconds in the draft
            if (draftAccum >= 1f) { kayak.RaiseSkill(SkillType.Draft, 1f, 0f); draftAccum -= 1f; }
        }

        /// <summary>Boosting right now.</summary>
        public bool Boosting => boostLeft > 0f;
        /// <summary>0..1 slingshot charge (the ring on the water shows it).</summary>
        public float SlingshotCharge { get; private set; }
        public bool Carving { get; private set; }
        public bool OnFastLine { get; private set; }
        /// <summary>A lip is right ahead: Paddle now for a boof (the bots read this too).</summary>
        public bool BoofWindow { get; private set; }
        public bool BarrelRolling => barrelRollLeft > 0f;
        /// <summary>Degrees spun in the air so far on this jump.</summary>
        public float AirSpin { get; private set; }

        float boostLeft, slingshotArmedLeft, carveHold, carveTime, fastLineAccum, barrelRollLeft, barrelRollSide = 1f;
        float lastPressTime = -10f, lastStrokeTimeForRhythm = -10f, lastBoofTime = -10f;
        bool lastPaddleSkill, lastBoostInput, lastRollSkill, boofed, wasInEddy;
        readonly Collider[] nearBuffer = new Collider[8];
        readonly Dictionary<Collider, bool> nearby = new Dictionary<Collider, bool>();
        readonly List<Collider> nearbyGone = new List<Collider>();

        void ResetSkills()
        {
            boostLeft = slingshotArmedLeft = carveHold = carveTime = fastLineAccum = barrelRollLeft = 0f;
            SlingshotCharge = 0f; Carving = OnFastLine = BoofWindow = false; AirSpin = 0f; boofed = false;
            nearby.Clear();
            DraftShare = 0f; DraftLeader = null; draftAccum = 0f;
            if (kayak) kayak.BoostVisual = 0f;
        }

        /// <summary>One step of the verbs. Runs after the modes, before steering and strokes.</summary>
        void UpdateSkills(float dt, in KayakInputState input, RiverPath.RiverSample sample, float waterSpeed, Vector3 flowDir,
                          Vector3 forward, float yaw, bool airborne, bool upright)
        {
            bool pressed = input.paddle && !lastPaddleSkill;
            lastPaddleSkill = input.paddle;
            if (pressed) lastPressTime = kayak.SimTime;

            // ---- boost
            bool boostPressed = input.boost && !lastBoostInput;
            lastBoostInput = input.boost;
            if (boostPressed && upright && boostLeft <= 0f && kayak.Meter >= boostCost && KayakFeel.Current.manualBoost)
            {
                boostLeft = boostDuration;
                kayak.RaiseSkill(SkillType.Boost, boostCost, -boostCost);
            }
            if (boostLeft > 0f)
            {
                boostLeft -= dt;
                if (!airborne && kayak.Speed < TopSpeed) rb.AddForce(forward * boostAcceleration, ForceMode.Acceleration);
            }
            kayak.BoostVisual = Mathf.MoveTowards(kayak.BoostVisual, boostLeft > 0f ? 1f : 0f, dt / 0.25f);

            // ---- the natural surge (no boost button): earned speed given right away, a little each step
            var feel = KayakFeel.Current;
            bool surging = false;
            if (kayak.PendingSurge > 0f && upright && !airborne)
            {
                float dv = Mathf.Min(kayak.PendingSurge, feel.surgeRate * dt);
                kayak.PendingSurge -= dv;
                if (kayak.Speed < TopSpeed) rb.AddForce(forward * dv, ForceMode.VelocityChange);
                surging = dv > feel.surgeRate * dt * 0.5f;
            }
            kayak.SurgeVisual = Mathf.MoveTowards(kayak.SurgeVisual, surging ? 1f : 0f, dt / (surging ? 0.15f : 0.6f));

            if (!upright) { Carving = false; SlingshotCharge = 0f; return; }

            // ---- fast line: meter trickles in (paid in whole points so the score reads cleanly)
            OnFastLine = !airborne && waterSpeed >= fastLineMinCurrent && Mathf.Abs(sample.lateralOffset - sample.thalweg) < fastLineWidth;
            if (OnFastLine)
            {
                rb.AddForce(forward * fastLineEdge, ForceMode.Acceleration);
                fastLineAccum += fastLineMeterRate * dt;
                if (fastLineAccum >= 1f) { kayak.RaiseSkill(SkillType.FastLine, 1f, 1f); fastLineAccum -= 1f; }
            }

            // ---- drafting: in another kayak's wake
            UpdateDraft(dt, forward, airborne);

            // ---- eddy slingshot
            bool inEddy = lastFeature is FlowObstacle && lastFeatureWeight > 0.35f;
            if (inEddy && Mathf.Abs(input.steer) > 0.4f && Mathf.Abs(yaw) > slingshotMinYaw)
                SlingshotCharge = Mathf.Min(1f, SlingshotCharge + dt / Mathf.Max(slingshotChargeTime, 0.1f));
            if (wasInEddy && !inEddy && SlingshotCharge > 0.15f) slingshotArmedLeft = slingshotExitWindow;
            wasInEddy = inEddy;
            if (!inEddy)
            {
                if (slingshotArmedLeft > 0f)
                {
                    slingshotArmedLeft -= dt;
                    if (waterSpeed > 2.5f)
                    {
                        float angle = Vector3.Angle(flowDir, forward);
                        bool perfect = angle >= slingshotPerfectAngle.x && angle <= slingshotPerfectAngle.y;
                        float power = SlingshotCharge * (perfect ? 1f + slingshotPerfectBonus : 1f);
                        rb.AddForce(forward * (slingshotKick * power), ForceMode.VelocityChange);
                        kayak.RaiseSkill(SkillType.Slingshot, power, slingshotMeter * power);
                        SlingshotCharge = 0f; slingshotArmedLeft = 0f;
                    }
                }
                else SlingshotCharge = Mathf.MoveTowards(SlingshotCharge, 0f, dt);
            }

            // ---- carve: Back-paddle + steer on fast water
            carveHold = input.backPaddle ? carveHold + dt : 0f;
            bool carving = !airborne && waterSpeed >= carveMinCurrent && carveHold >= carveHoldTime && Mathf.Abs(input.steer) >= carveMinSteer;
            if (carving)
            {
                carveTime += dt;
                kayak.AddMeter(carveMeterRate * dt);
            }
            else if (Carving && carveTime > 0.3f)
            {
                float kick = Mathf.Min(carveTime * carveKickPerSecond, carveMaxKick);
                rb.AddForce(forward * kick, ForceMode.VelocityChange);
                kayak.RaiseSkill(SkillType.Carve, carveTime, 0f); // the meter already flowed while carving
                carveTime = 0f;
            }
            if (!carving && !Carving) carveTime = 0f;
            Carving = carving;

            // ---- boof: Paddle right at a lip
            BoofWindow = false;
            if (!airborne && lastFeature is RiverFeature f && f.type != RiverFeatureType.Riffle && kayak.Speed > 2f)
            {
                Vector3 v = Flat(rb.linearVelocity).normalized;
                Vector3 p = rb.position;
                float here = kayak.river.GetWaterHeight(p), ahead = kayak.river.GetWaterHeight(p + v * boofLookAhead);
                BoofWindow = here - ahead >= boofMinDrop;
            }
            if (BoofWindow && pressed && kayak.SimTime - lastBoofTime > 1f)
            {
                lastBoofTime = kayak.SimTime;
                boofed = true;
                rb.AddForce(Vector3.up * boofLift + forward * boofForward, ForceMode.VelocityChange);
                kayak.RaiseSkill(SkillType.Boof, 1f, boofMeter);
            }

            // ---- air: flat spins (steer), barrel rolls (Exaggerated, Roll)
            bool rollPressed = input.roll && !lastRollSkill;
            lastRollSkill = input.roll;
            if (airborne)
            {
                AirSpin += yaw * dt;
                if (trickStyle == TrickStyle.Exaggerated && rollPressed && barrelRollLeft <= 0f)
                {
                    barrelRollLeft = barrelRollDuration;
                    barrelRollSide = input.steer < 0f ? -1f : 1f;
                }
            }
            if (barrelRollLeft > 0f)
            {
                barrelRollLeft -= dt;
                // Spin the hull around its length at one full turn per Barrel Roll Duration (the righting is off meanwhile)
                Vector3 localOmega = Quaternion.Inverse(rb.rotation) * rb.angularVelocity;
                float target = barrelRollSide * 360f / Mathf.Max(barrelRollDuration, 0.1f) * Mathf.Deg2Rad;
                rb.AddTorque(rb.rotation * Vector3.forward * (inertia.z * (target - localOmega.z) * 12f));
                if (barrelRollLeft <= 0f && airborne) kayak.RaiseSkill(SkillType.BarrelRoll, 360f, barrelRollMeter);
            }

            // ---- near misses
            if (kayak.Speed >= nearMissMinSpeed && !airborne) NearMisses();
            else nearby.Clear();
        }

        /// <summary>Degrees from the heading to the travel direction or its reverse, whichever is closer (+ = turn right).</summary>
        static float AirAlignError(Vector3 forward, Vector3 velocity)
        {
            Vector3 v = Flat(velocity);
            if (v.sqrMagnitude < 1f) return 0f;
            float error = Vector3.SignedAngle(forward, v, Vector3.up);
            if (error > 90f) error -= 180f; else if (error < -90f) error += 180f;
            return error;
        }

        /// <summary>Back-paddle here means carving, not a back stroke (fast water, steering hard).</summary>
        bool CarveContext(float waterSpeed, float steer) => Carving || (waterSpeed >= carveMinCurrent && Mathf.Abs(steer) >= carveMinSteer);

        /// <summary>A rock or log within Near Miss Distance that we left without touching.</summary>
        void NearMisses()
        {
            Vector3 center = rb.position + rb.rotation * hull.center;
            Vector3 axis = rb.rotation * Vector3.forward;
            float half = Mathf.Max(0f, hull.height * 0.5f - hull.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center - axis * half, center + axis * half, hull.radius + nearMissDistance,
                nearBuffer, capsizeLayers, QueryTriggerInteraction.Ignore);
            nearbyGone.Clear();
            foreach (var c in nearby.Keys) nearbyGone.Add(c);
            for (int i = 0; i < count; i++)
            {
                var c = nearBuffer[i];
                nearbyGone.Remove(c);
                if (!nearby.ContainsKey(c)) nearby[c] = false;
            }
            foreach (var c in nearbyGone)
            {
                bool touched = nearby[c];
                nearby.Remove(c);
                if (!touched) kayak.RaiseSkill(SkillType.NearMiss, 1f, nearMissMeter);
            }
        }

        /// <summary>Contact with a rock or log: no near miss for it.</summary>
        void MarkTouched(Collider c) { if (nearby.ContainsKey(c)) nearby[c] = true; }

        /// <summary>Stroke power from the rhythm: a press in the window = perfect, an early press = weak, holding = normal.</summary>
        float StrokePower(bool freshPress)
        {
            float power = 1f;
            if (freshPress)
            {
                float phase = (lastPressTime - lastStrokeTimeForRhythm) / Mathf.Max(strokeInterval, 0.01f);
                if (phase >= perfectWindow.x && phase <= perfectWindow.y)
                {
                    power = perfectPower;
                    kayak.RaiseSkill(SkillType.PerfectStroke, 1f, perfectStrokeMeter);
                }
                else if (phase < perfectWindow.x) power = mashPower;
            }
            lastStrokeTimeForRhythm = kayak.SimTime;
            return power;
        }

        /// <summary>Touchdown: counts the spin, then clean / sloppy (a very bad one is still CheckLanding's spin-out or capsize).</summary>
        void EvaluateLanding(Quaternion rotation, Vector3 velocity, Vector3 forward, Vector3 flowDir, float airTime)
        {
            float spin = Mathf.Abs(AirSpin);
            AirSpin = 0f;
            bool wasBoof = boofed;
            boofed = false;
            if (airTime < 0.25f || !NormalMode) return;
            bool bounce = kayak.SimTime - lastLandingAward < landingCooldown; // the skips after a landing are the same jump
            if (!bounce) lastLandingAward = kayak.SimTime;
            int halves = Mathf.RoundToInt(spin / 180f);
            if (halves >= 1 && Mathf.Abs(spin - halves * 180f) <= spinTolerance)
                kayak.RaiseSkill(SkillType.Spin, halves * 180f, spinMeterPerHalf * halves);

            float roll = Mathf.Abs(RollAngle(rotation));
            float angle = Vector3.Angle(flowDir, forward);
            angle = Mathf.Min(angle, 180f - angle); // landing backwards after a 180 is fine
            if (roll <= cleanLandingRoll && angle <= cleanLandingAngle && !bounce)
                kayak.RaiseSkill(SkillType.CleanLanding, wasBoof ? 2f : 1f, cleanLandingMeter * (wasBoof ? 1.5f : 1f));
            else if (roll > sloppyLandingRoll || angle > sloppyLandingAngle)
            {
                LoseSpeed(sloppySpeedLoss);
                kayak.RaiseSkill(SkillType.SloppyLanding, angle, 0f);
            }
        }
    }
}
