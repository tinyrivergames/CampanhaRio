using System;
using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Kayak
{
    /// <summary>
    /// The physical kayak (Stage 7): a real Rigidbody moved only by forces.
    ///   Buoyancy:  8 hull points (bow, stern, 3 per side), each a spring + damper on its depth under RiverPath's surface.
    ///              With every point out of the water, extra lift makes the effective gravity the tuned 5.5 m/s² (floaty jumps).
    ///   Water:     drag per point, relative to the water at that point, split in the hull frame: low along the hull,
    ///              high across it (the keel), damped vertically. The current carries the kayak; bow/stern shear and
    ///              eddy grip come out of the per-point sampling.
    ///   Steering:  the Stage 2–6 model driven by torque. In the current, input picks an angle relative to the flow
    ///              and a yaw controller turns toward it (release = back downstream); in still water it turns freely.
    ///              The paddler ferries across the flow in proportion to the hull's actual angle (turn first, then move).
    ///   Strokes:   impulses at the stroke side (forward thrust + the small natural yaw).
    ///   Upright:   an invisible self-righting torque with a maximum, so a big enough hit can still roll the kayak over.
    /// Impacts, spin-outs and capsizes are handled here too (Part D).
    /// </summary>
    [Serializable]
    public partial class KayakPhysics : KayakSimulation
    {
        [Header("Body")]
        [Tooltip("Kayak + paddler (kg).")]
        public float mass = 110f;
        [Tooltip("Center of mass in kayak space: low, so the kayak is stable (m).")]
        public Vector3 centerOfMass = new Vector3(0f, -0.2f, 0f);
        [Tooltip("The hull collider while this model runs: radius, tip-to-tip length and center height (m). Its bottom sits ~0.2 m under the waterline.")]
        public float hullRadius = 0.32f;
        public float hullLength = 3.3f;
        public float hullCenterHeight = 0.1f;
        [Tooltip("Inertia (kg·m²) around pitch (x), yaw (y) and roll (z). Set explicitly so collider changes don't change the feel.")]
        public Vector3 inertia = new Vector3(95f, 100f, 14f);

        [Header("Buoyancy (8 points)")]
        [Tooltip("Bow and stern points: distance from the center (m).")]
        public float hullHalfLength = 1.55f;
        [Tooltip("Side points: distance from the center line (m), and the along-hull position of the front/back pairs (m).")]
        public float hullHalfWidth = 0.34f;
        public float sidePointsAlong = 0.85f;
        [Tooltip("Heave frequency (Hz): the spring stiffness comes from it. Higher = follows the surface more tightly.")]
        public float buoyancyFrequency = 2.6f;
        [Tooltip("Heave damping ratio (1 = settles without overshoot).")]
        [Range(0.1f, 2f)] public float buoyancyDamping = 0.8f;
        [Tooltip("Deepest submersion a point pushes back for (m): limits the force when slammed under.")]
        public float maxSubmersion = 0.45f;
        [Tooltip("Effective gravity while fully airborne (m/s²). Lower = floatier jumps.")]
        public float airborneGravity = 5.5f;
        [Tooltip("Seconds with every point dry before it counts as a jump (ignores tiny hops).")]
        public float airborneDelay = 0.06f;
        [Tooltip("Share of dry points at which the floaty lift is complete (it fades in linearly before). 1 = only when fully airborne.")]
        [Range(0.1f, 1f)] public float fullLiftDryShare = 0.5f;
        [Tooltip("Still airborne with this many points touching (a hull skimming the water with its nose or tail).")]
        [Range(0, 3)] public int airborneMaxWet = 2;
        [Tooltip("Share of the heave damping while the hull rises out of the water (water pushes, it hardly holds on).")]
        [Range(0f, 1f)] public float riseDamping = 0.1f;
        [Tooltip("Gentle up/down float on the water (m) and its frequency (Hz).")]
        public float bobAmplitude = 0.02f;
        public float bobFrequency = 0.6f;

        [Header("Water drag (relative to the local current)")]
        [Tooltip("Along the hull (1/s), when almost matched with the water: a long, soft glide.")]
        public float lowSpeedDrag = 0.42f;
        [Tooltip("Along the hull (1/s), at and above Drag Reference Speed relative to the water.")]
        public float highSpeedDrag = 0.75f;
        public float dragReferenceSpeed = 4f;
        [Tooltip("Across the hull (1/s): the keel. High = the kayak tracks its heading instead of sliding sideways.")]
        public float keelDrag = 2.2f;
        [Tooltip("Inside an eddy pocket the slack water grabs the hull: drag is multiplied by up to this (so a kayak can park).")]
        public float eddyGrip = 3.5f;
        [Tooltip("Extra explicit shear yaw (deg/s² per m/s of bow-stern current difference) on top of the emergent one. 0 = emergent only.")]
        public float extraShearTorque = 100f;
        [Tooltip("Angular damping (1/s) on roll, pitch and yaw.")]
        public float rollDamping = 3f;
        public float pitchDamping = 2f;
        public float yawDamping = 0.2f;
        [Tooltip("Share of horizontal speed lost on a hard landing (scaled by impact).")]
        [Range(0f, 1f)] public float landingSpeedLoss = 0.08f;

        [Header("Steering")]
        [Tooltip("In the current, full input angles the kayak this many degrees away from downstream.")]
        public float maxSteerAngle = 50f;
        [Tooltip("Maximum turn rate in the current (deg/s). Stage 8: high, so the kayak snaps to its angle.")]
        public float maxTurnSpeed = 170f;
        [Tooltip("Full-input turn rate in still water (deg/s): free rotation in pools and eddies.")]
        public float stillWaterTurnSpeed = 85f;
        [Tooltip("How strongly the kayak turns toward its target angle in the current (deg/s per degree of error).")]
        public float alignToCurrent = 6.5f;
        [Tooltip("How quickly the turn rate reaches its target (1/s). Lower = lazier, heavier turns.")]
        public float rotationSmoothness = 18f;
        [Tooltip("How fast the steer value follows the input (units/second). The keyboard ramp lives in KayakInput; this only smooths.")]
        public float inputResponse = 30f;
        [Tooltip("Current speed (m/s) at which steering is fully relative to the current. Below it, steering blends into free turning.")]
        public float flowReferenceSpeed = 1.5f;
        [Tooltip("Speed across the flow (m/s) when held at the full steer angle: the ferry.")]
        public float ferrySpeed = 3.2f;
        [Tooltip("How quickly the ferry speed is reached (1/s).")]
        public float ferryResponse = 3.5f;
        [Tooltip("Hull angle to the flow (degrees) at which the full ferry speed is reached.")]
        public float fullFerryAngle = 30f;
        [Tooltip("Arcade grip: cross-flow speed (m/s) in the steer direction right away, before the hull has turned. 0 = pure ferry.")]
        public float lateralAssist = 1.2f;
        [Tooltip("Forward glide while steering (m/s relative to the water). Only ever speeds up, never brakes.")]
        public float strokeGlide = 0.8f;
        public float strokeGlideResponse = 1.2f;
        [Tooltip("Share of steering that still works in the air.")]
        [Range(0f, 1f)] public float airControl = 0.3f;

        [Header("Paddle strokes")]
        [Tooltip("Forward speed added by one stroke (m/s relative to the water).")]
        public float strokeImpulse = 2.4f;
        [Tooltip("Seconds between strokes while Paddle is held.")]
        public float strokeInterval = 0.6f;
        [Tooltip("Yaw kick of each stroke toward the opposite side (deg/s): the impulse lands off-center by just this much.")]
        public float strokeYawKick = 10f;
        [Tooltip("Relative forward speed at which strokes stop adding speed (m/s).")]
        public float maxPaddleSpeed = 4.5f;
        [Tooltip("Strokes stop adding speed as the speed over ground approaches this (m/s).")]
        public float maxAssistedSpeed = 7.5f;
        [Tooltip("Speed removed by one back stroke (m/s relative to the water), and the fastest back-paddling (m/s).")]
        public float backStrokeImpulse = 1f;
        public float maxBackSpeed = 1.2f;
        [Tooltip("Hard cap on the speed over ground (m/s).")]
        public float maxSpeed = 12f;

        [Header("Self-righting assist")]
        [Tooltip("Stiffness (1/s²) and damping (1/s) of the invisible torque that keeps the kayak upright.")]
        public float rightingStrength = 40f;
        public float rightingDamping = 9f;
        [Tooltip("Most torque the assist can give (N·m). Big impacts beat it: that is what allows a capsize.")]
        public float maxRightingTorque = 900f;

        [Header("Impacts")]
        [Tooltip("Layers whose hits count as impacts (rocks, cliffs, logs; other kayaks always count).")]
        public LayerMask impactLayers;
        [Tooltip("Minimum seconds between two impact events (contacts flicker while scraping along a rock).")]
        public float bumpCooldown = 0.5f;
        [Tooltip("Hits whose normal points this much up are the riverbed, not an impact.")]
        [Range(0f, 1f)] public float bedNormalY = 0.65f;

        public override float StrokeInterval => strokeInterval;

        float AssistedSpeed => maxAssistedSpeed;
        float PaddleSpeed => maxPaddleSpeed;
        internal float TopSpeed => maxSpeed;
        /// <summary>Hull points in the water in the last step (0 = airborne). For debugging and the benchmark trace.</summary>
        public int WetPoints { get; private set; }
        public override float CapsizeProgress => rb ? Mathf.Abs(RollAngle(rb.rotation)) / 180f : 0f;

        const int PointCount = 8;
        readonly Vector3[] points = new Vector3[PointCount];
        readonly float[] lastSurface = new float[PointCount];
        readonly int[] heightHints = new int[PointCount];
        int riverHint = -1, bowHint = -1, sternHint = -1;
        bool hasSurface;
        float dryTime;
        float strokeCooldown;
        bool pendingStroke, pendingBackStroke, lastPaddle, lastBackPaddle;
        float lastImpactTime = -10f, lastBedContact = -10f, lastObstacleContact = -10f;
        float savedRadius, savedHeight; Vector3 savedCenter;
        Vector3 stepVelocity;
        float stepYaw;

        public override void Enter(KayakController owner, Rigidbody body, CapsuleCollider hullCollider)
        {
            base.Enter(owner, body, hullCollider);
            if (impactLayers.value == 0) impactLayers = LayerMask.GetMask("Obstacle", "Environment");
            if (capsizeLayers.value == 0) capsizeLayers = LayerMask.GetMask("Obstacle");
            rb.constraints = RigidbodyConstraints.None;
            rb.useGravity = true;
            rb.mass = mass;
            rb.centerOfMass = centerOfMass;
            rb.inertiaTensor = inertia;
            rb.inertiaTensorRotation = Quaternion.identity;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.maxAngularVelocity = 20f; // a capsize rolls fast
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // A slimmer hull collider than the legacy one (whose 0.5 m radius reaches half a meter under the waterline and
            // would sit on the bed in the shallows). The legacy shape comes back on Exit.
            savedRadius = hull.radius; savedHeight = hull.height; savedCenter = hull.center;
            hull.radius = hullRadius;
            hull.height = hullLength;
            hull.center = new Vector3(0f, hullCenterHeight, 0f);
            BuildPoints();
            ResetState();
        }

        public override void Exit()
        {
            hull.radius = savedRadius; hull.height = savedHeight; hull.center = savedCenter;
        }

        void BuildPoints()
        {
            points[0] = new Vector3(0f, 0f, hullHalfLength);
            points[1] = new Vector3(0f, 0f, -hullHalfLength);
            float[] along = { sidePointsAlong, 0f, -sidePointsAlong };
            for (int i = 0; i < 3; i++)
            {
                points[2 + i * 2] = new Vector3(-hullHalfWidth, 0f, along[i]);
                points[3 + i * 2] = new Vector3(hullHalfWidth, 0f, along[i]);
            }
        }

        public override void ResetRiverHints() { riverHint = bowHint = sternHint = -1; for (int i = 0; i < PointCount; i++) heightHints[i] = -1; }

        public override void ResetState()
        {
            riverHint = bowHint = sternHint = -1;
            for (int i = 0; i < PointCount; i++) heightHints[i] = -1;
            hasSurface = false;
            dryTime = 0f;
            strokeCooldown = 0f;
            pendingStroke = pendingBackStroke = false;
            ResetModes();
            ResetSkills();
        }

        float Spring => mass / PointCount * Sq(2f * Mathf.PI * buoyancyFrequency);
        float Damper => 2f * buoyancyDamping * Mathf.Sqrt(Spring * mass / PointCount);
        /// <summary>How deep a point sits at rest: the root floats at the surface (the same waterline as the legacy model).</summary>
        float Draft => mass * Physics.gravity.magnitude / (PointCount * Spring);

        public override void Step(float dt, in KayakInputState input, Vector3 nudge)
        {
            var river = kayak.river;
            float simTime = kayak.SimTime;
            kayak.Steer = Mathf.MoveTowards(kayak.Steer, Mathf.Clamp(input.steer, -1f, 1f), inputResponse * dt);
            float steer = kayak.Steer;

            Vector3 position = rb.position;
            Quaternion rotation = rb.rotation;
            Vector3 com = rb.worldCenterOfMass;
            Vector3 up = rotation * Vector3.up, forward3 = rotation * Vector3.forward, right3 = rotation * Vector3.right;
            Vector3 forward = Flat(forward3).sqrMagnitude > 1e-4f ? Flat(forward3).normalized : Flat(-up).normalized;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            // The water here: centre, bow and stern samples (the side points interpolate along the hull)
            var sample = river.Sample(position, ref riverHint);
            kayak.RiverSample = sample;
            Vector3 boil = river.GetBoil(position, sample.waterVelocity.magnitude, simTime);
            float influence = kayak.currentInfluence;
            Vector3 water = Current(sample.waterVelocity, boil, river) * influence;
            Vector3 bowWater = Current(river.Sample(position + forward * 1.4f, ref bowHint).waterVelocity, boil, river) * influence;
            Vector3 sternWater = Current(river.Sample(position - forward * 1.4f, ref sternHint).waterVelocity, boil, river) * influence;
            kayak.CurrentVector = water;
            kayak.Shear = Vector3.Dot(bowWater - sternWater, right);
            var feature = river.FeatureAt(position, out float featureWeight);
            lastFeature = feature; lastFeatureWeight = featureWeight;
            kayak.FeatureName = feature != null ? feature.DisplayName : "-";
            float grip = feature is FlowObstacle ? Mathf.Lerp(1f, eddyGrip, featureWeight) : 1f;

            float waterSpeed = water.magnitude;
            float flowWeight = Mathf.Clamp01(waterSpeed / Mathf.Max(flowReferenceSpeed, 0.01f));
            Vector3 flowDir = waterSpeed > 0.01f ? water / waterSpeed : sample.direction;
            Vector3 flowRight = new Vector3(flowDir.z, 0f, -flowDir.x);

            Vector3 velocity = rb.linearVelocity;
            Vector3 omega = rb.angularVelocity;
            Vector3 relative = Flat(velocity) - water;
            float relForward = Vector3.Dot(relative, forward);
            float dragBlend = Mathf.Clamp01(relative.magnitude / Mathf.Max(dragReferenceSpeed, 0.01f));
            DraftShare = MeasureDraft(position); // another kayak's wake: calmer water, less drag
            float longDrag = Mathf.Lerp(lowSpeedDrag, highSpeedDrag, dragBlend) * grip * (1f - draftDragCut * DraftShare);
            float latDrag = keelDrag * grip;

            // ---- buoyancy + water drag, per point
            float spring = Spring, damper = Damper, draft = Draft, share = mass / PointCount;
            float bob = Mathf.Sin(simTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            Vector3 omegaNoYaw = omega - up * Vector3.Dot(omega, up); // the keel doesn't brake turning: steering owns the yaw
            int wet = 0;
            for (int i = 0; i < PointCount; i++)
            {
                Vector3 p = position + rotation * points[i];
                float surface = river.GetWaterHeight(p, ref heightHints[i]) + bob;
                float surfaceVelocity = hasSurface ? (surface - lastSurface[i]) / dt : 0f;
                lastSurface[i] = surface;
                float depth = surface - p.y + draft;
                if (depth <= 0f) continue;
                wet++;

                Vector3 pointVelocity = velocity + Vector3.Cross(omegaNoYaw, p - com);
                float along = points[i].z / 1.4f;
                Vector3 pointWater = along >= 0f ? Vector3.LerpUnclamped(water, bowWater, Mathf.Min(along, 1.2f))
                                                 : Vector3.LerpUnclamped(water, sternWater, Mathf.Min(-along, 1.2f));
                // Spring + damper on the depth (water pushes, it never pulls)
                // Water resists the hull pushing in (full damping) far more than it holds it back rising out (light damping),
                // so a kayak keeps its upward momentum over a crest and flies
                float relativeRise = pointVelocity.y - surfaceVelocity;
                float lift = spring * Mathf.Min(depth, maxSubmersion) - damper * relativeRise * (relativeRise > 0f ? riseDamping : 1f);
                Vector3 force = Vector3.up * Mathf.Max(0f, lift);
                // Drag in the hull frame, fading in as the point gets wet
                float wetShare = Mathf.Clamp01(depth / draft);
                Vector3 rel = pointVelocity - pointWater; rel.y = 0f;
                force -= share * wetShare * (longDrag * Vector3.Dot(rel, forward3) * Flat(forward3) + latDrag * Vector3.Dot(rel, right3) * Flat(right3));
                rb.AddForceAtPosition(force, p);
            }
            hasSurface = true;

            // ---- airborne: extra lift so the effective gravity is the tuned floaty one. It fades in with the share of dry
            // points, so a kayak leaving a lip or a crest nose-first already floats instead of diving.
            // Sitting on the riverbed in the shallows lifts the points out too: that is not a jump
            bool grounded = kayak.SimTime - lastBedContact < 0.1f;
            float dryShare = grounded ? 0f : 1f - wet / (float)PointCount;
            if (dryShare > 0f) rb.AddForce(Vector3.up * ((Physics.gravity.magnitude - airborneGravity) * Mathf.Clamp01(dryShare / Mathf.Max(fullLiftDryShare, 0.01f))), ForceMode.Acceleration);
            if (wet <= airborneMaxWet && !grounded)
            {
                dryTime += dt;
                if (dryTime >= airborneDelay && !kayak.IsAirborne) { kayak.IsAirborne = true; SyncJumps.TakeOff(kayak, lastFeature); kayak.RaiseTakeOff(); }
                if (kayak.IsAirborne) kayak.AirTime = dryTime;
            }
            else
            {
                if (kayak.IsAirborne)
                {
                    float impact = Mathf.Max(0f, -velocity.y);
                    kayak.IsAirborne = false;
                    Vector3 h = Flat(velocity) * (landingSpeedLoss * Mathf.Clamp01(impact / 3f));
                    rb.AddForce(-h, ForceMode.VelocityChange);
                    kayak.RaiseLand(impact, kayak.AirTime);
                    SyncJumps.Land(kayak, kayak.AirTime);
                    EvaluateLanding(rotation, velocity, forward, flowDir, kayak.AirTime);
                    CheckLanding(rotation, velocity, forward);
                }
                dryTime = 0f;
            }
            bool airborne = wet <= airborneMaxWet && !grounded;
            WetPoints = wet;

            // ---- modes (spin-out, capsize, roll-up): how much control the paddler has right now
            float roll = RollAngle(rotation);
            UpdateModes(dt, input, roll, waterSpeed, flowDir, right, airborne);
            float control = ControlShare();
            bool upright = control > 0f;
            steer *= control;

            // ---- steering: yaw toward the target (the Stage 2–6 rule, as a torque)
            float yaw = Vector3.Dot(omega, up) * Mathf.Rad2Deg;
            UpdateSkills(dt, input, sample, waterSpeed, flowDir, forward, yaw, airborne, upright);
            if (upright)
            {
                float freeYaw = steer * stillWaterTurnSpeed;
                Vector3 targetHeading = Quaternion.AngleAxis(steer * maxSteerAngle, Vector3.up) * flowDir;
                float headingError = Vector3.SignedAngle(forward, targetHeading, Vector3.up);
                float flowYaw = Mathf.Clamp(headingError * alignToCurrent, -maxTurnSpeed, maxTurnSpeed);
                float targetYaw = Mathf.Lerp(freeYaw, flowYaw, flowWeight * control);
                float yawRate = rotationSmoothness * control;
                if (Carving) targetYaw += Mathf.Sign(steer) * carveYaw;     // a hard carve turns tighter than the steering angle
                if (airborne)
                {
                    // In the air steer = flat spin; let go and the hull swings to the nearest travel-aligned heading (forward
                    // or backward), so a released spin lands clean instead of sideways (an arcade assist)
                    targetYaw = Mathf.Abs(steer) > 0.1f ? steer * airSpinRate : AirAlignError(forward, velocity) * airAlignGain;
                    yawRate = airSpinResponse;
                }
                float yawAccel = (targetYaw - yaw) * (1f - Mathf.Exp(-yawRate * dt)) / dt; // exact discrete version of the legacy lerp
                if (!airborne) yawAccel += kayak.Shear * extraShearTorque;
                rb.AddTorque(up * (inertia.y * yawAccel * Mathf.Deg2Rad));
            }

            if (!airborne && upright)
            {
                // Ferry: the paddler works across the flow in proportion to the hull's real angle (turn first, then move).
                // The hull drag at the target speed is what the paddler works against (fed forward), so a held ferry moves
                // straight across the flow; the keel still resists everything else (release, turns, cross currents).
                float angle = Vector3.SignedAngle(flowDir, forward, Vector3.up);
                float angleShare = Mathf.Clamp(Mathf.Sin(angle * Mathf.Deg2Rad) / Mathf.Sin(fullFerryAngle * Mathf.Deg2Rad), -1f, 1f);
                // Plus a direct grip assist in the steer direction that fades as the angle takes over (instant response)
                float assist = lateralAssist * steer * (1f - Mathf.Abs(angleShare));
                float crossTarget = Mathf.Clamp(ferrySpeed * angleShare * Mathf.Abs(steer) + assist, -ferrySpeed, ferrySpeed) * flowWeight;
                float cross = Vector3.Dot(relative, flowRight);
                float ferryWeight = flowWeight * Mathf.Clamp01(Mathf.Abs(steer) * 4f); // only while steering in a current
                Vector3 ferry = flowRight * (ferryResponse * ferryWeight * (crossTarget - cross))
                              + right * (latDrag * Vector3.Dot(flowRight * crossTarget, right))
                              + forward * (longDrag * Vector3.Dot(flowRight * crossTarget, forward));
                // Steering glide: a little forward speed relative to the water (never brakes a stroke)
                float glideTarget = Mathf.Abs(steer) * strokeGlide;
                if (Mathf.Abs(steer) > 0.01f && relForward < glideTarget)
                    ferry += forward * (strokeGlideResponse * (glideTarget - relForward) + longDrag * relForward);
                rb.AddForce(ferry, ForceMode.Acceleration);
            }

            // ---- self-righting: roll toward the mode's target (upright normally; over and back up during a capsize);
            // pitch only in the air, the buoyancy levels it on the water
            Vector3 localOmega = Quaternion.Inverse(rotation) * omega;
            Vector3 error = Quaternion.Inverse(rotation) * AxisAngle(up, Vector3.up);
            float pitchTorque = airborne && upright ? inertia.x * (rightingStrength * 0.5f * error.x - rightingDamping * localOmega.x) : 0f;
            RollGains(out float rollTarget, out float kp, out float kd, out float maxTorque);
            if (BarrelRolling) kp = kd = 0f; // the trick spins the hull itself
            float rollTorque = inertia.z * (kp * Mathf.DeltaAngle(roll, rollTarget) * Mathf.Deg2Rad - kd * localOmega.z);
            Vector3 righting = Vector3.ClampMagnitude(new Vector3(pitchTorque, 0f, rollTorque), maxTorque);
            // Plain angular damping: strong on roll and pitch, light on yaw
            righting -= new Vector3(inertia.x * pitchDamping * localOmega.x, inertia.y * yawDamping * localOmega.y, inertia.z * rollDamping * localOmega.z);
            rb.AddTorque(rotation * righting);

            // ---- strokes, nudges, never stuck, speed cap
            if (upright) HandleStrokes(dt, input.paddle, input.backPaddle && !CarveContext(waterSpeed, steer), relForward, forward, right, com);
            if (nudge.sqrMagnitude > 0f) rb.AddForce(nudge, ForceMode.VelocityChange);
            NeverStuck(dt, velocity, flowDir, waterSpeed);
            Vector3 flat = Flat(velocity);
            if (flat.magnitude > TopSpeed) rb.AddForce(flat.normalized * (TopSpeed - flat.magnitude), ForceMode.VelocityChange);

            kayak.Velocity = velocity;
            stepVelocity = velocity;
            stepYaw = omega.y;
            kayak.YawRate = yaw;
            kayak.CurrentSpeed = waterSpeed;
            kayak.LateralSpeed = Vector3.Dot(velocity, sample.right);
        }

        static Vector3 Current(Vector3 flow, Vector3 boil, RiverPath river) => Vector3.ClampMagnitude(flow + boil, river.maxCurrentFinal);

        /// <summary>Rotation that takes <paramref name="from"/> to <paramref name="to"/> as axis * angle (radians), valid up to 180°.</summary>
        static Vector3 AxisAngle(Vector3 from, Vector3 to)
        {
            Vector3 axis = Vector3.Cross(from, to);
            float sin = axis.magnitude, cos = Vector3.Dot(from, to);
            if (sin < 1e-5f) return cos > 0f ? Vector3.zero : Vector3.forward * Mathf.PI; // upside down: pick an axis
            return axis / sin * Mathf.Atan2(sin, cos);
        }

        /// <summary>One stroke per press; holding repeats at Stroke Interval. The impulse lands beside the hull on the stroke side.</summary>
        void HandleStrokes(float dt, bool paddle, bool backPaddle, float relForward, Vector3 forward, Vector3 right, Vector3 com)
        {
            strokeCooldown -= dt;
            pendingStroke |= paddle && !lastPaddle;
            lastPaddle = paddle;
            pendingBackStroke |= backPaddle && !lastBackPaddle;
            lastBackPaddle = backPaddle;
            if (strokeCooldown > 0f || kayak.IsAirborne) return;

            bool forwardStroke = pendingStroke || paddle;
            bool backStroke = !forwardStroke && (pendingBackStroke || backPaddle);
            if (!forwardStroke && !backStroke) return;
            bool freshPress = pendingStroke; // a press, not just holding: the rhythm counts
            pendingStroke = pendingBackStroke = false;

            int side = -kayak.LastStrokeSide; // alternate left / right
            strokeCooldown = strokeInterval;

            float dv;
            if (forwardStroke)
            {
                float groundRoom = Mathf.Clamp01((AssistedSpeed - kayak.Speed) / 2f);
                dv = strokeImpulse * StrokePower(freshPress) * Mathf.Clamp01(1f - relForward / Mathf.Max(PaddleSpeed, 0.01f)) * groundRoom;
            }
            else dv = -Mathf.Max(0f, Mathf.Min(backStrokeImpulse, relForward + maxBackSpeed));

            // A stroke on the left pushes the bow right, and the other way round; back strokes mirror it.
            // The lever arm is chosen so the kick is Stroke Yaw Kick, whatever the impulse size.
            float yawKick = strokeYawKick * Mathf.Deg2Rad * -side * (backStroke ? -1f : 1f);
            if (Mathf.Abs(dv) > 0.05f)
            {
                float lever = Mathf.Clamp(inertia.y * yawKick / (mass * dv), -0.9f, 0.9f); // yaw = lever × impulse
                rb.AddForceAtPosition(forward * (mass * dv), com - right * lever, ForceMode.Impulse);
            }
            else rb.AddTorque(Vector3.up * (inertia.y * yawKick), ForceMode.Impulse);
            kayak.RaiseStroke(side, backStroke);
        }

        // ------------------------------------------------------------------ collisions

        public override void OnCollision(Collision collision, bool enter)
        {
            if (collision.contactCount == 0) return;
            var other = collision.collider;
            bool otherKayak = other.attachedRigidbody && other.attachedRigidbody.GetComponent<KayakController>();
            var contact = collision.GetContact(0);
            Vector3 normal = contact.normal;
            bool rock = (capsizeLayers.value & (1 << other.gameObject.layer)) != 0;
            if (rock) MarkTouched(other);
            if (!otherKayak && !rock && normal.y > bedNormalY) { lastBedContact = kayak.SimTime; return; } // on the riverbed, not an impact
            bool counts = otherKayak || (impactLayers.value & (1 << other.gameObject.layer)) != 0;
            if (counts) lastObstacleContact = kayak.SimTime; // for "never stuck"
            if (!enter || !counts) return;
            // Relative speed into the obstacle across the horizontal part of the normal (rounded rocks give tilted normals),
            // from the velocities before this step's contact solve (the reported relative velocity can already be damped)
            Vector3 across = Flat(normal).sqrMagnitude > 0.0025f ? Flat(normal).normalized : normal;
            var otherBody = otherKayak ? other.attachedRigidbody.GetComponent<KayakController>() : null;
            Vector3 closing = stepVelocity - (otherBody ? otherBody.Velocity : Vector3.zero);
            float speed = Mathf.Max(Mathf.Abs(Vector3.Dot(collision.relativeVelocity, across)), -Vector3.Dot(closing, across));
            float sliding = Flat(closing - across * Vector3.Dot(closing, across)).magnitude;
            // Head-on share of the approach: 1 = straight into it, 0 = sliding along it (a glancing scrape)
            float headOn = speed / Mathf.Max(Mathf.Sqrt(speed * speed + sliding * sliding), 0.01f);
            bool gentle = other.GetComponentInParent<GentleObstacle>(); // the deer: only ever a gentle bump
            HandleImpact(speed, headOn, contact.point, normal, otherKayak, rock && !gentle, gentle);
        }

        static float Sq(float x) => x * x;
    }
}
