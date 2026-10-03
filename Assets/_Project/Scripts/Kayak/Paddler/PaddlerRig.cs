using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace CampanhaRio.Kayak.Paddler
{
    /// <summary>
    /// The paddler.s body, for any character (the animals included). Bones are found by the names in <see cref="bones"/>
    /// (a name suffix match, so "mixamorig:Hips", "smartrig:Hips" and "Hips" all work; Mixamo names by default).
    /// Only the hips, the head and both arm chains are required: spine, neck and legs are used when the character has them.
    ///   Update (before the Animator): resets the bones to their bind pose, builds the SEATED pose procedurally
    ///   (legs forward inside the hull) and applies spine lean / twist / breathing from PaddlerAnimationController.
    ///   Those bones carry RigTransform, so Animation Rigging reads them into its stream.
    ///   Animation Rigging then solves Two Bone IK (hands on the paddle grips) and Multi-Aim (head and neck).
    /// In Edit mode (beauty shots) rigs don't evaluate, so a small analytic IK previews the arms instead.
    /// Set Facing to the model.s forward (MeshAI exports face -Z).
    /// </summary>
    [ExecuteAlways]
    public class PaddlerRig : MonoBehaviour
    {
        [Tooltip("Root of the character model (the imported FBX instance or the placeholder rig).")]
        public Transform character;
        [Tooltip("Model-space forward of the character (the MeshAI turtle faces -Z).")]
        public Vector3 facing = Vector3.back;
        [Tooltip("Bone names in this character (suffix match). Leave a name empty if the character has no such bone.")]
        public BoneNames bones = new BoneNames();

        [Header("Animation Rigging")]
        public RigBuilder rigBuilder;
        public TwoBoneIKConstraint leftArmIK, rightArmIK;
        public MultiAimConstraint headAim, neckAim;
        [Tooltip("Hand targets (children of the paddle) and the head look target.")]
        public Transform leftGrip, rightGrip, lookTarget;
        [Tooltip("Where the right hand goes when it trails in the water while resting.")]
        public Transform handTrail;
        public Transform leftElbowHint, rightElbowHint;

        [Header("Seated pose")]
        [Tooltip("Pose the legs forward inside the cockpit (off for characters whose legs are hidden or too short to matter).")]
        public bool seatLegs = true;
        [Tooltip("Thigh angle above horizontal (degrees): legs forward inside the cockpit.")]
        public float thighLift = -4f;
        [Tooltip("Shin angle below horizontal (degrees).")]
        public float shinDrop = 22f;
        [Tooltip("Knees apart (m per m of thigh).")]
        public float kneeSpread = 0.35f;
        [Tooltip("Spine recline (degrees, + = lean back).")]
        public float recline = 4f;

        [Header("Breathing and micro-motion")]
        public float breathRate = 0.22f;
        [Tooltip("Chest rise per breath (degrees).")]
        public float breathAngle = 1.6f;
        [Tooltip("Tiny head/torso sway noise (degrees).")]
        public float microMotion = 1.2f;

        // Set every frame by PaddlerAnimationController (degrees)
        [NonSerialized] public float lean;      // + forward
        [NonSerialized] public float sideLean;  // + toward the right
        [NonSerialized] public float twist;     // + shoulders turn right
        [NonSerialized] public float leftArmWeight = 1f, rightArmWeight = 1f, lookWeight = 0.8f;

        public Transform Hips { get; private set; }
        public Transform Head { get; private set; }
        public Transform Chest { get; private set; }

        Transform spine, spine1, spine2, neck;
        Transform lUpLeg, lLeg, lFoot, lToe, rUpLeg, rLeg, rFoot, rToe;
        Transform lArm, lForeArm, lHand, rArm, rForeArm, rHand;
        Transform[] posed;
        [SerializeField, HideInInspector] Quaternion[] bind; // captured on the T-pose by the builder
        bool ready;

        void OnEnable() => Init();

        public void Init()
        {
            ready = false;
            if (!character) return;
            var n = bones;
            Hips = Find(n.hips); spine = Find(n.spine); spine1 = Find(n.spine1); spine2 = Find(n.spine2);
            neck = Find(n.neck); Head = Find(n.head);
            lUpLeg = Find(n.leftUpLeg); lLeg = Find(n.leftLeg); lFoot = Find(n.leftFoot); lToe = Find(n.leftToe);
            rUpLeg = Find(n.rightUpLeg); rLeg = Find(n.rightLeg); rFoot = Find(n.rightFoot); rToe = Find(n.rightToe);
            lArm = Find(n.leftArm); lForeArm = Find(n.leftForeArm); lHand = Find(n.leftHand);
            rArm = Find(n.rightArm); rForeArm = Find(n.rightForeArm); rHand = Find(n.rightHand);
            Chest = spine2 ? spine2 : spine1 ? spine1 : spine ? spine : Hips;
            if (!Hips || !Head || !lArm || !lForeArm || !lHand || !rArm || !rForeArm || !rHand) return;

            posed = new[] { Hips, spine, spine1, spine2, neck, Head, lUpLeg, lLeg, lFoot, rUpLeg, rLeg, rFoot, lArm, lForeArm, lHand, rArm, rForeArm, rHand };
            if (bind == null || bind.Length != posed.Length)
            {
                bind = new Quaternion[posed.Length];
                for (int i = 0; i < posed.Length; i++) bind[i] = posed[i] ? posed[i].localRotation : Quaternion.identity;
            }
            ready = true;
        }

        /// <summary>Stores the current local rotations as the bind pose (call on the untouched T-pose, e.g. when building).</summary>
        public void CaptureBindPose() { bind = null; Init(); }

        public Transform Find(string boneName) => FindBone(character, boneName);

        public static Transform FindBone(Transform root, string boneName)
        {
            if (!root) return null;
            if (string.IsNullOrEmpty(boneName)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == boneName || t.name.EndsWith(":" + boneName)) return t;
            return null;
        }

        void Update() => PreviewPose();

        /// <summary>Poses the body (and, outside Play mode, previews the arm IK). Tools call it before rendering in Edit mode.</summary>
        public void PreviewPose()
        {
            if (!ready) { Init(); if (!ready) return; }
            Pose(Application.isPlaying ? Time.time : (float)(DateTime.Now.TimeOfDay.TotalSeconds));
            ApplyRigWeights();
        }

        /// <summary>Builds this frame's body pose (before the rig solves arms and head).</summary>
        public void Pose(float time)
        {
            for (int i = 0; i < posed.Length; i++) if (posed[i]) posed[i].localRotation = bind[i];

            Vector3 forward = character.TransformDirection(facing).normalized;
            Vector3 up = character.up;
            Vector3 right = Vector3.Cross(up, forward).normalized;

            // Legs forward inside the hull
            if (seatLegs) SeatLeg(lUpLeg, lLeg, lFoot, lToe, forward, up, -right);
            if (seatLegs) SeatLeg(rUpLeg, rLeg, rFoot, rToe, forward, up, right);

            // Spine: recline + lean + side lean + twist spread over the spine bones, breathing on the chest
            float breath = Mathf.Sin(time * breathRate * Mathf.PI * 2f);
            float micro = (Mathf.PerlinNoise(time * 0.35f, 3.1f) - 0.5f) * 2f * microMotion;
            Bend(spine, 0.35f, forward, right, up, -recline, micro * 0.5f);
            Bend(spine1, 0.35f, forward, right, up, breath * breathAngle * 0.5f, micro * 0.3f);
            Bend(spine2, 0.3f, forward, right, up, -breath * breathAngle, 0f);
        }

        void Bend(Transform bone, float share, Vector3 forward, Vector3 right, Vector3 up, float extraPitch, float extraRoll)
        {
            if (!bone) return;
            // Positive lean = chest forward = rotate around the right axis
            Quaternion q = Quaternion.AngleAxis((lean + extraPitch) * share, right)
                         * Quaternion.AngleAxis(-(sideLean + extraRoll) * share, forward)
                         * Quaternion.AngleAxis(twist * share, up);
            bone.rotation = q * bone.rotation;
        }

        void SeatLeg(Transform upLeg, Transform leg, Transform foot, Transform toe, Vector3 forward, Vector3 up, Vector3 outward)
        {
            if (!upLeg || !leg || !foot) return;
            Vector3 thighDir = (forward * Mathf.Cos(thighLift * Mathf.Deg2Rad) + up * Mathf.Sin(thighLift * Mathf.Deg2Rad) + outward * kneeSpread).normalized;
            Aim(upLeg, leg.position, thighDir);
            Vector3 shinDir = (forward * Mathf.Cos(shinDrop * Mathf.Deg2Rad) - up * Mathf.Sin(shinDrop * Mathf.Deg2Rad) - outward * kneeSpread * 0.5f).normalized;
            Aim(leg, foot.position, shinDir);
            if (toe) Aim(foot, toe.position, (forward * 0.85f + up * 0.25f).normalized); // feet flat, toes forward under the deck
        }

        /// <summary>Rotates a bone so the direction to its child points along dir.</summary>
        static void Aim(Transform bone, Vector3 childPosition, Vector3 dir)
        {
            Vector3 current = childPosition - bone.position;
            if (current.sqrMagnitude < 1e-8f) return;
            bone.rotation = Quaternion.FromToRotation(current, dir) * bone.rotation;
        }

        void ApplyRigWeights()
        {
            if (!Application.isPlaying)
            {
                // Edit-mode preview: the rig graph doesn't run, solve the arms here
                if (leftGrip) SolveTwoBone(lArm, lForeArm, lHand, leftGrip.position, leftElbowHint);
                if (rightGrip) SolveTwoBone(rArm, rForeArm, rHand, rightGrip.position, rightElbowHint);
                return;
            }
            if (leftArmIK) leftArmIK.weight = leftArmWeight;
            if (rightArmIK) rightArmIK.weight = rightArmWeight;
            if (headAim) headAim.weight = lookWeight;
            if (neckAim) neckAim.weight = lookWeight * 0.4f;
        }

        /// <summary>Analytic two-bone IK (edit-mode preview only).</summary>
        static void SolveTwoBone(Transform root, Transform mid, Transform tip, Vector3 target, Transform hint)
        {
            if (!root || !mid || !tip) return;
            float a = Vector3.Distance(root.position, mid.position), b = Vector3.Distance(mid.position, tip.position);
            Vector3 toTarget = target - root.position;
            float d = Mathf.Clamp(toTarget.magnitude, 0.01f, (a + b) * 0.999f);
            Vector3 bendHint = hint ? hint.position - root.position : Vector3.down;
            Vector3 axis = toTarget.normalized;
            Vector3 bendDir = Vector3.ProjectOnPlane(bendHint, axis).normalized;
            float cos = Mathf.Clamp((a * a + d * d - b * b) / (2f * a * d), -1f, 1f);
            Vector3 elbow = root.position + axis * (a * cos) + bendDir * (a * Mathf.Sqrt(1f - cos * cos));
            Aim(root, mid.position, elbow - root.position);
            Aim(mid, tip.position, root.position + axis * d - mid.position);
        }
    }
}

namespace CampanhaRio.Kayak.Paddler
{
    /// <summary>The bone names a paddler character uses (Mixamo names by default). Empty = the character has no such bone.</summary>
    [Serializable]
    public class BoneNames
    {
        public string hips = "Hips", spine = "Spine", spine1 = "Spine1", spine2 = "Spine2", neck = "Neck", head = "Head";
        public string leftArm = "LeftArm", leftForeArm = "LeftForeArm", leftHand = "LeftHand";
        public string rightArm = "RightArm", rightForeArm = "RightForeArm", rightHand = "RightHand";
        public string leftUpLeg = "LeftUpLeg", leftLeg = "LeftLeg", leftFoot = "LeftFoot", leftToe = "LeftToeBase";
        public string rightUpLeg = "RightUpLeg", rightLeg = "RightLeg", rightFoot = "RightFoot", rightToe = "RightToeBase";
    }
}
