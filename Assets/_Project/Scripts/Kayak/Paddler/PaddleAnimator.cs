using System;
using UnityEngine;

namespace CampanhaRio.Kayak.Paddler
{
    /// <summary>A paddle pose in kayak (Visual) space. Yaw + = right blade swings back; roll + = right blade up.</summary>
    [Serializable]
    public struct PaddlePose
    {
        public Vector3 center;
        public float yaw, roll;

        public static PaddlePose Lerp(PaddlePose a, PaddlePose b, float t) => new PaddlePose
        {
            center = Vector3.Lerp(a.center, b.center, t),
            yaw = Mathf.Lerp(a.yaw, b.yaw, t),
            roll = Mathf.Lerp(a.roll, b.roll, t),
        };
    }

    /// <summary>
    /// Drives the paddle transform procedurally (it's a separate object, never skinned):
    ///   a base pose chosen by PaddlerAnimationController (rest across the lap, ready, rudder, lifted, brace),
    ///   smoothed so nothing pops, plus an overlaid stroke cycle catch → pull → exit → recovery,
    ///   alternating sides and started by KayakController.OnStroke (so the blade bites exactly when the impulse lands).
    /// The paddler's hands follow the paddle with IK; the torso twist follows the paddle yaw.
    /// Visual only: it never changes the physics.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class PaddleAnimator : MonoBehaviour
    {
        public KayakController kayak;
        [Tooltip("The paddle pivot (a child of Visual). Its local X runs along the shaft.")]
        public Transform paddle;
        [Tooltip("Blade centers, for splashes and drips.")]
        public Transform bladeLeft, bladeRight;

        [Header("Poses (kayak space: X right, Y up, Z forward)")]
        public PaddlePose ready = new PaddlePose { center = new Vector3(0f, 0.42f, 0.18f) };
        public PaddlePose rest = new PaddlePose { center = new Vector3(0f, 0.3f, 0.06f), roll = 0f };
        public PaddlePose lifted = new PaddlePose { center = new Vector3(0f, 0.6f, 0.16f) };
        [Tooltip("How fast the base pose follows its target (1/s).")]
        public float poseSmoothness = 6f;

        [Header("Stroke cycle")]
        [Tooltip("Stroke length as a share of the controller's Stroke Interval.")]
        [Range(0.5f, 1.2f)] public float strokeDurationShare = 0.95f;
        [Tooltip("How far the blade swings forward at the catch and back at the exit (degrees of yaw).")]
        public float sweep = 42f;
        [Tooltip("How far the working blade dips (degrees of roll).")]
        public float dipAngle = 36f;
        [Tooltip("Sideways shift of the paddle toward the working side (m).")]
        public float sideShift = 0.12f;
        [Tooltip("Forward reach at the catch (m).")]
        public float reach = 0.12f;
        [Tooltip("Blade entry / exit moments within the stroke (0..1).")]
        public float entryAt = 0.14f, exitAt = 0.72f;

        [Header("Rudder / brace")]
        [Tooltip("Rudder: blade trailing on the inside of the turn (degrees of yaw back).")]
        public float rudderYaw = 38f;
        public float rudderDip = 28f;
        [Tooltip("Brace stroke after a bump or in rapids: shorter and flatter (share of a full stroke).")]
        [Range(0.2f, 1f)] public float braceScale = 0.55f;

        /// <summary>A blade entered the water: (side -1/+1, strength 0..1).</summary>
        public event Action<int, float> BladeEntered;
        /// <summary>A blade left the water (side): drips start.</summary>
        public event Action<int> BladeExited;

        /// <summary>The base pose the controller wants (smoothed here).</summary>
        public PaddlePose Target { get; set; }
        public PaddlePose Current { get; private set; }
        /// <summary>0..1 while a stroke plays, -1 otherwise.</summary>
        public float StrokePhase => strokeTime >= 0f ? Mathf.Clamp01(strokeTime / strokeDuration) : -1f;
        public int StrokeSide { get; private set; } = 1;
        /// <summary>Recommended torso twist (degrees): the torso turns with the paddle.</summary>
        public float Twist => Current.yaw;

        PaddlePose basePose;
        float strokeTime = -1f, strokeDuration = 0.6f, strokeScale = 1f, strokeWeight;
        bool strokeBack, entered, exited;

        void Awake()
        {
            if (!kayak) kayak = GetComponentInParent<KayakController>();
            basePose = Current = Target = ready;
        }

        void OnEnable() { if (kayak) kayak.OnStroke += OnStroked; }
        void OnDisable() { if (kayak) kayak.OnStroke -= OnStroked; }

        void OnStroked(int side, bool back) => Play(side, back, 1f);

        /// <summary>Starts a stroke on a side. Scale below 1 makes it a lighter correction or brace stroke (visual only).</summary>
        public void Play(int side, bool back, float scale)
        {
            StrokeSide = side;
            strokeBack = back;
            strokeScale = scale;
            strokeTime = 0f;
            entered = exited = false;
            strokeDuration = (kayak ? kayak.StrokeInterval : 0.6f) * strokeDurationShare * Mathf.Lerp(0.7f, 1f, scale);
        }

        public bool IsStroking => strokeTime >= 0f;

        void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            basePose = PaddlePose.Lerp(basePose, Target, 1f - Mathf.Exp(-poseSmoothness * dt));
            var pose = basePose;

            if (strokeTime >= 0f)
            {
                strokeTime += dt;
                float p = strokeTime / strokeDuration;
                if (!entered && p >= entryAt) { entered = true; BladeEntered?.Invoke(StrokeSide, strokeScale); }
                if (!exited && p >= exitAt) { exited = true; BladeExited?.Invoke(StrokeSide); }
                if (p >= 1f) strokeTime = -1f;
                strokeWeight = Mathf.MoveTowards(strokeWeight, 1f, dt / 0.08f);
                pose = PaddlePose.Lerp(pose, StrokePose(Mathf.Clamp01(p)), strokeWeight);
            }
            else if (strokeWeight > 0f)
            {
                // Ease out of the last stroke into the base pose
                strokeWeight = Mathf.MoveTowards(strokeWeight, 0f, dt / 0.35f);
                pose = PaddlePose.Lerp(pose, StrokePose(1f), strokeWeight);
            }

            Current = pose;
            if (!paddle) return;
            paddle.localPosition = pose.center;
            paddle.localRotation = Quaternion.AngleAxis(pose.yaw, Vector3.up) * Quaternion.AngleAxis(pose.roll, Vector3.forward);
        }

        /// <summary>The stroke cycle as keyframes over p = 0..1 (for side +1 = right; mirrored for the left).</summary>
        PaddlePose StrokePose(float p)
        {
            int s = StrokeSide;
            float sc = strokeScale;
            // yaw: blade forward (-s * sweep) at the catch, back (+s * sweep * 0.6) at the exit; reversed for a back stroke
            float catchYaw = -s * sweep * sc, exitYaw = s * sweep * 0.6f * sc;
            if (strokeBack) (catchYaw, exitYaw) = (exitYaw, catchYaw);

            float yaw, dip;
            if (p < entryAt) { float k = Smooth(p / entryAt); yaw = catchYaw; dip = Mathf.Lerp(0.25f, 1f, k); }
            else if (p < exitAt) { float k = Smooth((p - entryAt) / (exitAt - entryAt)); yaw = Mathf.Lerp(catchYaw, exitYaw, k); dip = 1f; }
            else { float k = Smooth((p - exitAt) / (1f - exitAt)); yaw = Mathf.Lerp(exitYaw, exitYaw * 1.3f, k); dip = Mathf.Lerp(1f, 0.1f, k); }

            var pose = ready;
            float reachNow = p < exitAt ? Mathf.Lerp(reach, -reach * 0.5f, Smooth(p / exitAt)) : -reach * 0.5f;
            pose.center = ready.center + new Vector3(s * sideShift * dip * sc, -0.05f * dip, reachNow * sc);
            pose.yaw = yaw;
            pose.roll = -s * dipAngle * dip * sc; // working blade down
            return pose;
        }

        /// <summary>Rudder pose: the blade trails in the water on the steering side.</summary>
        public PaddlePose RudderPose(float steer)
        {
            int s = steer >= 0f ? 1 : -1;
            float a = Mathf.Abs(steer);
            var pose = ready;
            pose.center += new Vector3(s * sideShift * a, -0.04f * a, -0.05f * a);
            pose.yaw = s * rudderYaw * a;
            pose.roll = -s * rudderDip * a;
            return pose;
        }

        public Vector3 BladePosition(int side)
        {
            var blade = side < 0 ? bladeLeft : bladeRight;
            return blade ? blade.position : paddle ? paddle.position : transform.position;
        }

        static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
    }
}
