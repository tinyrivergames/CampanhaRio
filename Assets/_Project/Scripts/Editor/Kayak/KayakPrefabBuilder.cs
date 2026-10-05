using CampanhaRio.Dev;
using CampanhaRio.Kayak;
using CampanhaRio.Kayak.Paddler;
using CampanhaRio.Net;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Builds the graybox kayak prefabs (no final art yet):
    ///   Prefabs/Kayak/Kayak.prefab      the player's kayak: Rigidbody + capsule hull (the old project's exact values),
    ///                                   KayakController with the KayakPhysics tuning (the code defaults ARE the old prefab's
    ///                                   tuning, checked in Docs/PORTED.md), KayakInput, LocalPlayer, the autopilot, and a
    ///                                   graybox hull + paddle + the placeholder paddler driven by the procedural animation.
    ///   Prefabs/Kayak/Kayak_Net.prefab  a variant with NetworkObject + KayakNetSync (no LocalPlayer).
    /// Menu: CampanhaRio > Setup > Build Kayak Prefabs. Batch: CampanhaRio.Editor.KayakPrefabBuilder.Build
    /// </summary>
    public static class KayakPrefabBuilder
    {
        public const string KayakPath = "Assets/_Project/Prefabs/Kayak/Kayak.prefab";
        public const string KayakNetPath = "Assets/_Project/Prefabs/Kayak/Kayak_Net.prefab";
        const string InputPath = "Assets/_Project/Input/CampanhaRioInput.inputactions";
        const string PhysicsMaterialPath = "Assets/_Project/Art/Materials/Physics/KayakPhysics.physicMaterial";

        // From the old project's LookSettings: the paddler and paddle sizes and where the hips sit in the cockpit
        const float PaddlerScale = 0.85f, PaddleScale = 0.85f;
        static readonly Vector3 SeatPosition = new Vector3(0f, 0.13f, -0.16f);

        [MenuItem("CampanhaRio/Setup/Build Kayak Prefabs")]
        public static void Build()
        {
            var root = new GameObject("Kayak");
            try
            {
                BuildRoot(root);
                var visual = Child(root.transform, "Visual");
                BuildHull(visual);
                var paddle = BuildPaddle(visual);
                BuildPaddler(root, visual, paddle);
                SetLayer(root, LayerMask.NameToLayer("Kayak"));
                PrefabUtility.SaveAsPrefabAsset(root, KayakPath);
            }
            finally { Object.DestroyImmediate(root); }
            BuildNetVariant();
            AssetDatabase.SaveAssets();
            Debug.Log("[Campanha] Kayak prefabs built.");
        }

        static void BuildRoot(GameObject root)
        {
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 60f; // KayakPhysics sets its own mass, inertia and gravity on Enter; these match the old prefab
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = (RigidbodyConstraints)84;
            var hull = root.AddComponent<CapsuleCollider>();
            hull.radius = 0.5f; hull.height = 3.4f; hull.direction = 2;
            hull.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMaterialPath);

            var input = root.AddComponent<KayakInput>();
            input.actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(InputPath);
            var kayak = root.AddComponent<KayakController>();
            kayak.input = input;
            kayak.physics = new KayakPhysics();
            root.AddComponent<LocalPlayer>();
            root.AddComponent<KayakAutopilot>(); // idle (Off) until F2 or a test turns it on
        }

        const string HullModelPath = "Assets/_Project/Art/Models/Kayak/CaiaqueA.fbx";
        const string MaskMeshPath = "Assets/_Project/Art/Models/Kayak/CaiaqueA_WaterMask.asset";
        const string MaskMaterialPath = "Assets/_Project/Art/Materials/Kayak/M_BoatWaterMask.mat";

        [System.Serializable] class MaskSidecar { public Mask[] waterMasks; }
        [System.Serializable] class Mask { public float[] center, radii; }

        /// <summary>
        /// Invisible caps over the cockpit and the cargo well (from the model's sidecar): they keep the river water from
        /// being drawn inside the boat (BoatWaterMask.shader, stencil bit 2).
        /// </summary>
        static void BuildWaterMasks(Transform visual)
        {
            string json = System.IO.Path.ChangeExtension(HullModelPath, ".softtoon.json");
            if (!System.IO.File.Exists(json)) return;
            var masks = JsonUtility.FromJson<MaskSidecar>(System.IO.File.ReadAllText(json)).waterMasks;
            if (masks == null || masks.Length == 0) return;
            const int segments = 24;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            foreach (var m in masks)
            {
                int c = verts.Count;
                verts.Add(new Vector3(m.center[0], m.center[1], m.center[2]));
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    verts.Add(new Vector3(m.center[0] + Mathf.Cos(a) * m.radii[0], m.center[1], m.center[2] + Mathf.Sin(a) * m.radii[1]));
                    tris.AddRange(new[] { c, c + 1 + (i + 1) % segments, c + 1 + i });
                }
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MaskMeshPath);
            if (!mesh) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, MaskMeshPath); }
            mesh.Clear();
            mesh.name = "CaiaqueA_WaterMask";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaskMaterialPath);
            if (!mat)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MaskMaterialPath));
                mat = new Material(Shader.Find("CampanhaRio/BoatWaterMask"));
                AssetDatabase.CreateAsset(mat, MaskMaterialPath);
            }
            var go = new GameObject("WaterMask", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(visual, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            AssetDatabase.SaveAssets();
        }

        static void BuildHull(Transform visual)
        {
            // The approved art kayak when it exists (its origin and size match the capsule; the physics keep the capsule)
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HullModelPath);
            if (model)
            {
                var art = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
                art.name = "Hull";
                art.transform.localPosition = Vector3.zero;
                art.transform.localRotation = Quaternion.identity;
                BuildWaterMasks(visual);
                return;
            }
            var hull = Primitive(PrimitiveType.Capsule, "Hull", visual, new Vector3(0f, 0.02f, 0f), new Vector3(0.68f, 1.62f, 0.34f),
                GrayboxMaterials.Get("Hull", new Color(0.85f, 0.32f, 0.22f)));
            hull.localRotation = Quaternion.Euler(90f, 0f, 0f);
            hull.localScale = new Vector3(0.68f, 1.62f, 0.34f); // 3.3 m long, 0.68 m wide, flat
            var cockpit = Primitive(PrimitiveType.Cylinder, "Cockpit", visual, new Vector3(0f, 0.15f, -0.12f), new Vector3(0.48f, 0.03f, 0.72f),
                GrayboxMaterials.Get("Dark", new Color(0.18f, 0.17f, 0.16f)));
        }

        static Transform BuildPaddle(Transform visual)
        {
            var paddle = Child(visual, "Paddle");
            paddle.localScale = Vector3.one * PaddleScale;
            var dark = GrayboxMaterials.Get("PaddleShaft", new Color(0.29f, 0.19f, 0.16f)); // the reference: brown shaft, red blades
            var blade = GrayboxMaterials.Get("PaddleBlade", new Color(0.40f, 0.08f, 0.13f));
            var shaft = Primitive(PrimitiveType.Cylinder, "Shaft", paddle, Vector3.zero, new Vector3(0.035f, 1.05f, 0.035f), dark);
            shaft.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Primitive(PrimitiveType.Cube, "BladeLeft", paddle, new Vector3(-1.05f, 0f, 0f), new Vector3(0.42f, 0.02f, 0.17f), blade);
            Primitive(PrimitiveType.Cube, "BladeRight", paddle, new Vector3(1.05f, 0f, 0f), new Vector3(0.42f, 0.02f, 0.17f), blade);
            foreach (float x in new[] { -0.84f, 0.84f }) // the collars where the blades meet the shaft
            {
                var collar = Primitive(PrimitiveType.Cylinder, "Collar", paddle, new Vector3(x, 0f, 0f), new Vector3(0.06f, 0.03f, 0.06f), dark);
                collar.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            // Grips a little behind and above the shaft, so the hands sit on it
            Child(paddle, "Grip_L").localPosition = new Vector3(-0.36f, 0.035f, -0.06f);
            Child(paddle, "Grip_R").localPosition = new Vector3(0.36f, 0.035f, -0.06f);
            return paddle;
        }

        /// <summary>The paddler: the placeholder rig (Mixamo bone names) wired to the procedural animation, as in the old project.</summary>
        static void BuildPaddler(GameObject kayakRoot, Transform visual, Transform paddle)
        {
            var holder = Child(visual, "Paddler");
            holder.localRotation = Quaternion.Euler(0f, 180f, 0f); // the placeholder faces -Z; the kayak faces +Z
            holder.localScale = Vector3.one * PaddlerScale;
            var character = PaddlerPlaceholderRig.Create(holder);
            character.name = "Character";

            var rig = holder.gameObject.AddComponent<PaddlerRig>();
            rig.character = character.transform;
            rig.CaptureBindPose(); // on the untouched T-pose

            Vector3 hipsLocal = visual.InverseTransformPoint(rig.Hips.position);
            holder.localPosition += SeatPosition - hipsLocal;

            var leftTarget = Child(visual, "IK_LeftHand");
            var rightTarget = Child(visual, "IK_RightHand");
            var look = Child(visual, "IK_Look");
            look.localPosition = new Vector3(0f, 0.8f, 8f);
            var trail = Child(visual, "IK_HandTrail");
            trail.localPosition = new Vector3(0.62f, 0.02f, -0.2f);
            rig.leftGrip = paddle.Find("Grip_L");
            rig.rightGrip = paddle.Find("Grip_R");
            rig.lookTarget = look;
            rig.handTrail = trail;
            leftTarget.position = rig.leftGrip.position;
            rightTarget.position = rig.rightGrip.position;

            Vector3 up = visual.up, fwd = visual.forward, right = visual.right;
            var lArm = rig.Find(rig.bones.leftArm);
            var rArm = rig.Find(rig.bones.rightArm);
            rig.leftElbowHint = Child(rig.Chest, "Hint_LeftElbow");
            rig.leftElbowHint.position = lArm.position - right * 0.25f - up * 0.35f - fwd * 0.15f;
            rig.rightElbowHint = Child(rig.Chest, "Hint_RightElbow");
            rig.rightElbowHint.position = rArm.position + right * 0.25f - up * 0.35f - fwd * 0.15f;

            // Animation Rigging: an Animator without controller (pure procedural) + one rig layer
            var animator = holder.gameObject.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            var rigBuilder = holder.gameObject.AddComponent<RigBuilder>();
            var rigRoot = Child(holder, "Rig");
            var rigComponent = rigRoot.gameObject.AddComponent<Rig>();
            rigBuilder.layers.Add(new RigLayer(rigComponent, true));
            rig.rigBuilder = rigBuilder;
            rig.leftArmIK = ArmIK(rigRoot, "LeftArmIK", rig, rig.bones.leftArm, rig.bones.leftForeArm, rig.bones.leftHand, leftTarget, rig.leftElbowHint);
            rig.rightArmIK = ArmIK(rigRoot, "RightArmIK", rig, rig.bones.rightArm, rig.bones.rightForeArm, rig.bones.rightHand, rightTarget, rig.rightElbowHint);
            rig.headAim = Aim(rigRoot, "HeadAim", rig.Head, look, 70f);
            var neck = rig.Find(rig.bones.neck);
            if (neck) rig.neckAim = Aim(rigRoot, "NeckAim", neck, look, 40f);
            foreach (var name in new[] { rig.bones.hips, rig.bones.spine, rig.bones.spine1, rig.bones.spine2, rig.bones.neck, rig.bones.head, "LeftShoulder", "RightShoulder" })
            {
                var bone = rig.Find(name);
                if (bone && !bone.GetComponent<RigTransform>()) bone.gameObject.AddComponent<RigTransform>();
            }

            var secondary = holder.gameObject.AddComponent<PaddlerSecondaryMotion>();
            secondary.rig = rig;
            secondary.renderers = character.GetComponentsInChildren<SkinnedMeshRenderer>();

            var paddleAnimator = kayakRoot.AddComponent<PaddleAnimator>();
            paddleAnimator.kayak = kayakRoot.GetComponent<KayakController>();
            paddleAnimator.paddle = paddle;
            paddleAnimator.bladeLeft = paddle.Find("BladeLeft");
            paddleAnimator.bladeRight = paddle.Find("BladeRight");
            paddle.localPosition = paddleAnimator.ready.center;
            paddle.localRotation = Quaternion.identity;

            var controller = kayakRoot.AddComponent<PaddlerAnimationController>();
            controller.kayak = paddleAnimator.kayak;
            controller.paddle = paddleAnimator;
            controller.rig = rig;
            controller.leftHandTarget = leftTarget;
            controller.rightHandTarget = rightTarget;
        }

        static TwoBoneIKConstraint ArmIK(Transform rigRoot, string name, PaddlerRig rig, string arm, string foreArm, string hand, Transform target, Transform hint)
        {
            var ik = new GameObject(name).AddComponent<TwoBoneIKConstraint>();
            ik.transform.SetParent(rigRoot, false);
            ik.data.root = rig.Find(arm);
            ik.data.mid = rig.Find(foreArm);
            ik.data.tip = rig.Find(hand);
            ik.data.target = target;
            ik.data.hint = hint;
            ik.data.targetPositionWeight = 1f;
            ik.data.targetRotationWeight = 0f; // no finger bones: the hand keeps its flat grip pose
            ik.data.hintWeight = 1f;
            return ik;
        }

        static MultiAimConstraint Aim(Transform rigRoot, string name, Transform bone, Transform target, float limit)
        {
            var aim = new GameObject(name).AddComponent<MultiAimConstraint>();
            aim.transform.SetParent(rigRoot, false);
            aim.data.constrainedObject = bone;
            aim.data.sourceObjects = new WeightedTransformArray { new WeightedTransform(target, 1f) };
            aim.data.aimAxis = MultiAimConstraintData.Axis.Z_NEG; // the face looks down the head's -Z
            aim.data.upAxis = MultiAimConstraintData.Axis.Y;
            aim.data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
            aim.data.limits = new Vector2(-limit, limit);
            aim.data.maintainOffset = false;
            aim.data.constrainedXAxis = aim.data.constrainedYAxis = aim.data.constrainedZAxis = true;
            return aim;
        }

        static void BuildNetVariant()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(KayakPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                Object.DestroyImmediate(instance.GetComponent<LocalPlayer>());
                var no = instance.AddComponent<NetworkObject>();
                no.DontDestroyWithOwner = true;
                no.SynchronizeTransform = true;
                instance.AddComponent<KayakNetSync>();
                instance.name = "Kayak_Net";
                PrefabUtility.SaveAsPrefabAsset(instance, KayakNetPath);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        // ------------------------------------------------------------------ helpers

        internal static Transform Child(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (!t) { t = new GameObject(name).transform; t.SetParent(parent, false); }
            return t;
        }

        internal static Transform Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static void SetLayer(GameObject go, int layer)
        {
            if (layer < 0) return;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }
    }
}
