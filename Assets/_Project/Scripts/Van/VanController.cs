using UnityEngine;

namespace CampanhaRio.Van
{
    /// <summary>What drives the van this frame: a player, the auto-driver, or nothing.</summary>
    public struct VanControls
    {
        /// <summary>-1 (brake / reverse) .. 1 (throttle).</summary>
        public float throttle;
        /// <summary>-1 (left) .. 1 (right).</summary>
        public float steer;
        public bool handbrake;
    }

    /// <summary>
    /// The van's physics (Stage 11): an arcade raycast vehicle, no WheelColliders. Four suspension rays with a spring and a
    /// damper push the body up; each grounded wheel grips sideways (less with the handbrake on the rear) and the rear drives.
    /// The top speed is soft (the push fades toward it), braking and reversing are gentle, and the steering eases with speed.
    /// A low centre of mass and an auto-right torque keep it on its wheels; if it is upside down or stuck off the road for
    /// 3 s it is put back on the nearest road point (Reset fires first, for the fade of the people inside).
    /// Only the machine that simulates the van runs this (the driver's, or the host's); other copies are kinematic.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VanController : MonoBehaviour
    {
        [Header("Body")]
        public float mass = 1700f;
        public Vector3 centerOfMass = new Vector3(0f, 0.05f, 0.1f);

        [Header("Suspension (per wheel)")]
        [Tooltip("Wheel hardpoints in the van's space (front left, front right, rear left, rear right).")]
        public Vector3[] wheels = { new Vector3(-0.82f, 0.55f, 1.42f), new Vector3(0.82f, 0.55f, 1.42f), new Vector3(-0.82f, 0.55f, -1.42f), new Vector3(0.82f, 0.55f, -1.42f) };
        public float wheelRadius = 0.36f;
        [Tooltip("Suspension travel below the hardpoint (m), spring (N/m) and damper (N·s/m).")]
        public float restLength = 0.38f;
        public float spring = 42000f, damper = 3800f;
        [Tooltip("Anti-roll bars: force per unit of compression difference across an axle (N). Higher = flatter in curves.")]
        public float antiRoll = 30000f;
        [Tooltip("Sideways grip acts this high above the contact (m): lower = less body roll.")]
        public float gripHeight = 0.12f;

        [Header("Driving")]
        [Tooltip("Push at full throttle (N), soft top speed forward and in reverse (m/s).")]
        public float engineForce = 7600f;
        public float topSpeed = 17f, reverseSpeed = 5f;
        public float brakeForce = 9500f;
        [Tooltip("Rolling and air drag (N per m/s).")]
        public float drag = 120f;
        [Tooltip("Steering angle at walking pace and at the top speed (degrees), and how fast the wheels turn (deg/s).")]
        public float steerLow = 32f, steerHigh = 14f, steerRate = 110f;
        [Tooltip("Sideways grip (an acceleration cap, m/s²), and the rear's share of it with the handbrake on.")]
        public float grip = 11f;
        [Range(0f, 1f)] public float handbrakeGrip = 0.35f;

        [Header("Staying upright")]
        public float uprightTorque = 9000f;
        [Tooltip("Upside down, or stuck off the road this long (s): put back on the road.")]
        public float resetAfter = 3f;

        public LayerMask groundMask;

        public Rigidbody Body { get; private set; }
        /// <summary>The controls this physics step (set by the van every frame).</summary>
        public VanControls Controls { get; set; }
        /// <summary>Forward speed (m/s, negative backwards).</summary>
        public float ForwardSpeed { get; private set; }
        public float Speed => Body ? Body.linearVelocity.magnitude : 0f;
        public float SteerAngle { get; private set; }
        public int GroundedWheels { get; private set; }
        public float[] Compression { get; } = new float[4];
        public bool[] WheelGrounded { get; } = new bool[4];
        /// <summary>The road under each wheel this step is gravel (for the dust).</summary>
        public int Resets { get; private set; }
        public event System.Action BeforeReset, AfterReset;
        /// <summary>The physics runs here (the others follow the network).</summary>
        public bool Simulated { get; private set; } = true;

        float troubleFor, stuckOnRoad;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.mass = mass;
            Body.centerOfMass = centerOfMass;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping = 0.02f;
            Body.angularDamping = 0.6f;
            Body.maxDepenetrationVelocity = 2f; // something overlapping it nudges it out, never launches it
            if (groundMask.value == 0) groundMask = LayerMask.GetMask("Environment");
        }

        /// <summary>Simulate here (the driver's or the host's machine), or follow someone else's snapshots.</summary>
        public void SetSimulated(bool on)
        {
            Simulated = on;
            Body.isKinematic = !on;
            if (!on) Controls = default;
        }

        void FixedUpdate()
        {
            if (!Simulated) return;
            float dt = Time.fixedDeltaTime;
            var c = Controls;
            // A safety net: nothing should ever throw it faster than a car (a bad overlap once launched it at 480 m/s)
            if (Body.linearVelocity.sqrMagnitude > 30f * 30f) Body.linearVelocity = Body.linearVelocity.normalized * 30f;
            if (Body.angularVelocity.sqrMagnitude > 4f * 4f) Body.angularVelocity = Body.angularVelocity.normalized * 4f;
            Vector3 v = Body.linearVelocity;
            ForwardSpeed = Vector3.Dot(v, transform.forward);
            float speed01 = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / topSpeed);
            float maxSteer = Mathf.Lerp(steerLow, steerHigh, speed01 * speed01);
            SteerAngle = Mathf.MoveTowards(SteerAngle, c.steer * maxSteer, steerRate * dt);

            GroundedWheels = 0;
            for (int i = 0; i < 4; i++)
            {
                Vector3 hard = transform.TransformPoint(wheels[i]);
                float length = restLength + wheelRadius;
                if (!Physics.Raycast(hard, -transform.up, out var hit, length, groundMask, QueryTriggerInteraction.Ignore))
                {
                    WheelGrounded[i] = false;
                    Compression[i] = 0f;
                    continue;
                }
                WheelGrounded[i] = true;
                GroundedWheels++;
                float comp = 1f - (hit.distance - wheelRadius) / restLength;
                comp = Mathf.Clamp01(comp);
                Vector3 pointVel = Body.GetPointVelocity(hard);
                float compVel = (comp - Compression[i]) / dt;
                Compression[i] = comp;
                float suspension = Mathf.Max(0f, spring * comp * restLength + damper * compVel * restLength);
                Body.AddForceAtPosition(transform.up * suspension, hard);

                // The wheel's frame on the ground
                bool front = i < 2;
                Quaternion steerRot = front ? Quaternion.AngleAxis(SteerAngle, transform.up) : Quaternion.identity;
                Vector3 wheelFwd = Vector3.ProjectOnPlane(steerRot * transform.forward, hit.normal).normalized;
                Vector3 wheelSide = Vector3.Cross(hit.normal, wheelFwd).normalized;
                Vector3 contact = hit.point + hit.normal * 0.05f;

                // Sideways grip: cancel the sliding (capped), less on the rear with the handbrake
                float side = Vector3.Dot(pointVel, wheelSide);
                float g = grip * (c.handbrake && !front ? handbrakeGrip : 1f);
                float lateral = Mathf.Clamp(-side / dt, -g, g) * (Body.mass * 0.25f);
                Body.AddForceAtPosition(wheelSide * lateral, contact + hit.normal * gripHeight);

                // Drive (the rear) and brakes (all four)
                float along = Vector3.Dot(pointVel, wheelFwd);
                if (!front)
                {
                    float push = 0f;
                    if (c.throttle > 0.01f && along > -0.5f) push = c.throttle * engineForce * Mathf.Clamp01(1f - along / topSpeed) * 0.5f;
                    else if (c.throttle < -0.01f && along < 0.5f) push = c.throttle * engineForce * 0.6f * Mathf.Clamp01(1f + along / reverseSpeed) * 0.5f;
                    Body.AddForceAtPosition(wheelFwd * push, contact);
                }
                bool braking = (c.throttle < -0.01f && along > 0.5f) || (c.throttle > 0.01f && along < -0.5f) || (c.handbrake && !front);
                if (braking)
                {
                    float b = Mathf.Clamp(-along / dt * Body.mass * 0.25f, -brakeForce * 0.25f, brakeForce * 0.25f);
                    Body.AddForceAtPosition(wheelFwd * b, contact);
                }
                // Rolling resistance (and coming to rest when nothing presses)
                Body.AddForceAtPosition(-wheelFwd * along * drag * 0.25f, contact);
                if (Mathf.Abs(c.throttle) < 0.01f && Mathf.Abs(along) < 0.6f) Body.AddForceAtPosition(-wheelFwd * along / dt * Body.mass * 0.25f * 0.3f, contact);
            }

            // Anti-roll bars: the more compressed side of an axle is pushed up, the other down
            for (int axle = 0; axle < 2; axle++)
            {
                int l = axle * 2, r = l + 1;
                if (!WheelGrounded[l] && !WheelGrounded[r]) continue;
                float diff = (Compression[l] - Compression[r]) * antiRoll;
                if (WheelGrounded[l]) Body.AddForceAtPosition(transform.up * diff, transform.TransformPoint(wheels[l]));
                if (WheelGrounded[r]) Body.AddForceAtPosition(-transform.up * diff, transform.TransformPoint(wheels[r]));
            }

            // Upright: a spring toward the world's up (always a little, strongly when tipped)
            Vector3 axis = Vector3.Cross(transform.up, Vector3.up);
            float tip = Vector3.Angle(transform.up, Vector3.up) / 180f;
            Body.AddTorque(axis.normalized * (uprightTorque * (GroundedWheels < 4 ? 1f : 0.6f) * tip * Mathf.Clamp01(axis.magnitude * 4f + 0.2f)), ForceMode.Force);
            Body.AddTorque(-Body.angularVelocity * Body.mass * 0.6f, ForceMode.Force); // a heavy, calm body

            Trouble(dt);
        }

        /// <summary>Upside down, or stuck off the road, for resetAfter seconds: back on the road.</summary>
        void Trouble(float dt)
        {
            bool upsideDown = transform.up.y < 0.25f;
            bool stuck = false;
            var road = VanRoad.Instance;
            // Stuck: pressing on and not moving (off the road at once, on it after a while: a bank, a rock)
            if (road && Speed < 0.6f && Mathf.Abs(Controls.throttle) > 0.2f && !Controls.handbrake && Controls.throttle * ForwardSpeed >= -0.05f) // (not while braking)
            {
                road.Nearest(transform.position, transform.forward, out float offset);
                stuck = offset > road.halfWidth + 4f || stuckOnRoad > 2f;
                stuckOnRoad += dt;
            }
            else stuckOnRoad = 0f;
            troubleFor = upsideDown || stuck ? troubleFor + dt : 0f;
            if (troubleFor > resetAfter) ResetOntoRoad();
        }

        /// <summary>Back onto the nearest road point, facing along the road, at rest.</summary>
        public void ResetOntoRoad()
        {
            troubleFor = 0f;
            var road = VanRoad.Instance;
            if (!road) return;
            BeforeReset?.Invoke();
            Debug.Log($"[Campanha] Van reset onto the road from {transform.position} (up {transform.up.y:0.00}, speed {Speed:0.0})");
            float d = road.Nearest(transform.position, transform.forward, out _);
            Vector3 p = road.PointAt(d) + Vector3.up * 0.9f;
            Teleport(new Pose(p, Quaternion.LookRotation(road.DirectionAt(d), Vector3.up)));
            Resets++;
            AfterReset?.Invoke();
        }

        public void Teleport(Pose pose)
        {
            Body.position = pose.position; Body.rotation = pose.rotation;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (!Body.isKinematic) { Body.linearVelocity = Vector3.zero; Body.angularVelocity = Vector3.zero; }
            SteerAngle = 0f;
            for (int i = 0; i < 4; i++) Compression[i] = 0f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            foreach (var w in wheels) { Vector3 p = transform.TransformPoint(w); Gizmos.DrawLine(p, p - transform.up * (restLength + wheelRadius)); }
        }
    }
}
