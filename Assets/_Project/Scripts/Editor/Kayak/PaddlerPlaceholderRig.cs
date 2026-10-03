using UnityEditor;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// FALLBACK paddler: a primitive humanoid on a real bone hierarchy with Mixamo bone names (Hips, Spine,
    /// Spine1, Spine2, Neck, Head, Left/RightShoulder, Arm, ForeArm, Hand, UpLeg, Leg, Foot, ToeBase), in a T-pose facing -Z,
    /// with the MeshAI turtle's proportions. The same PaddlerRig / IK / controller drive it, so swapping between it and
    /// a rigged model only re-links references. Used automatically when the rigged model is missing.
    /// Menu: CampanhaRio/Paddler/Save Placeholder Rig Prefab.
    /// </summary>
    public static class PaddlerPlaceholderRig
    {
        const string PrefabPath = "Assets/_Project/Prefabs/Kayak/Paddler_PlaceholderRig.prefab";

        [MenuItem("CampanhaRio/Paddler/Save Placeholder Rig Prefab")]
        static void SavePrefab()
        {
            var go = Create(null);
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            Debug.Log("[Campanha] Saved " + PrefabPath);
        }

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("PlaceholderRig");
            root.transform.SetParent(parent, false);
            var shirt = Mat("Shirt"); var skin = Mat("Skin"); var hat = Mat("StrawHat"); var dark = Mat("Cockpit");

            var hips = Bone("Hips", root.transform, new Vector3(0f, 0.674f, 0f));
            var spine = Bone("Spine", hips, new Vector3(0f, 0.106f, 0f));
            var spine1 = Bone("Spine1", spine, new Vector3(0f, 0.106f, 0f));
            var spine2 = Bone("Spine2", spine1, new Vector3(0f, 0.1f, 0f));
            var neck = Bone("Neck", spine2, new Vector3(0f, 0.08f, 0f));
            var head = Bone("Head", neck, new Vector3(0f, 0.07f, 0f));
            Bone("HeadTop_End", head, new Vector3(0f, 0.32f, 0f));
            Part(PrimitiveType.Capsule, hips, new Vector3(0f, 0.16f, 0f), new Vector3(0.34f, 0.2f, 0.24f), shirt);
            Part(PrimitiveType.Capsule, spine1, new Vector3(0f, 0.05f, 0f), new Vector3(0.36f, 0.22f, 0.26f), shirt);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.13f, -0.02f), new Vector3(0.26f, 0.28f, 0.26f), skin);
            Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.26f, 0f), new Vector3(0.62f, 0.012f, 0.62f), hat);   // brim
            Part(PrimitiveType.Cylinder, head, new Vector3(0f, 0.31f, 0f), new Vector3(0.26f, 0.06f, 0.26f), hat);    // crown
            Part(PrimitiveType.Capsule, head, new Vector3(0f, 0.0f, 0.1f), new Vector3(0.24f, 0.26f, 0.1f), dark);    // long dark hair

            foreach (int s in new[] { -1, 1 })
            {
                string side = s < 0 ? "Right" : "Left"; // the model faces -Z, so its left is +X
                var shoulder = Bone(side + "Shoulder", spine2, new Vector3(0.055f * s, -0.015f, 0f));
                var arm = Bone(side + "Arm", shoulder, new Vector3(0.185f * s, -0.045f, 0f));
                var fore = Bone(side + "ForeArm", arm, new Vector3(0.214f * s, 0f, 0f));
                var hand = Bone(side + "Hand", fore, new Vector3(0.234f * s, 0f, 0f));
                Limb(arm, fore.localPosition, 0.09f, shirt);
                Limb(fore, hand.localPosition, 0.08f, skin);
                Part(PrimitiveType.Sphere, hand, new Vector3(0.05f * s, 0f, 0f), new Vector3(0.1f, 0.06f, 0.09f), skin);

                var upLeg = Bone(side + "UpLeg", hips, new Vector3(0.12f * s, -0.08f, 0f));
                var leg = Bone(side + "Leg", upLeg, new Vector3(0.067f * s, -0.211f, -0.05f));
                var foot = Bone(side + "Foot", leg, new Vector3(0.04f * s, -0.215f, 0f));
                Bone(side + "ToeBase", foot, new Vector3(0.045f * s, -0.1f, -0.04f));
                Limb(upLeg, leg.localPosition, 0.12f, dark);
                Limb(leg, foot.localPosition, 0.1f, dark);
            }
            return root;
        }

        static Transform Bone(string name, Transform parent, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        /// <summary>A capsule from the bone to its child, parented to the bone so it moves with it.</summary>
        static void Limb(Transform bone, Vector3 toChild, float thickness, Material mat)
        {
            var part = Part(PrimitiveType.Capsule, bone, toChild * 0.5f, new Vector3(thickness, toChild.magnitude * 0.5f + thickness * 0.3f, thickness), mat);
            part.localRotation = Quaternion.FromToRotation(Vector3.up, toChild);
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            if (mat) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        static Material Mat(string name) => GrayboxMaterials.Get("Paddler" + name, name switch
        {
            "Shirt" => new Color(0.31f, 0.49f, 0.76f),
            "Skin" => new Color(0.86f, 0.7f, 0.55f),
            "StrawHat" => new Color(0.9f, 0.8f, 0.5f),
            _ => new Color(0.18f, 0.17f, 0.16f),
        });
    }
}
