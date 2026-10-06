using CampanhaRio.CameraSystem;
using CampanhaRio.Campaign;
using CampanhaRio.Jobs;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Puts the vertical slice's loop into Core (Phase 2, part D): the agency's van (graybox, Seu Alce at the wheel; a
    /// NetworkObject the host drives), the AgencyFlow with the orders board, and the kayak camera on Core's camera (on
    /// while the local player paddles). Re-running replaces them.
    /// Menu: CampanhaRio > Setup > Build Slice Core. Batch: CampanhaRio.Editor.SliceCoreBuilder.Build
    /// </summary>
    public static class SliceCoreBuilder
    {
        const string CorePath = "Assets/_Project/Scenes/Core/Core.unity";

        [MenuItem("CampanhaRio/Setup/Build Slice Core")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(CorePath);
            foreach (string old in new[] { "Van (placeholder)", "Valley Van", "Agency Flow" })
            {
                var go = GameObject.Find(old);
                if (go) Object.DestroyImmediate(go);
            }

            // The van: body, cab, wheels, Seu Alce at the wheel (no colliders: it never touches the kayaks)
            var van = new GameObject("Valley Van");
            van.transform.position = new Vector3(0f, 37f, -285f);
            var body = GrayboxMaterials.Get("VanBody", new Color(0.36f, 0.55f, 0.62f));
            var dark = GrayboxMaterials.Get("Dark", new Color(0.18f, 0.17f, 0.16f));
            var alce = GrayboxMaterials.Get("Alce", new Color(0.42f, 0.29f, 0.2f));
            Part(van.transform, PrimitiveType.Cube, "Body", new Vector3(0f, 1.25f, -0.4f), new Vector3(2f, 1.7f, 3.8f), body);
            Part(van.transform, PrimitiveType.Cube, "Cab", new Vector3(0f, 1.0f, 1.9f), new Vector3(2f, 1.2f, 1.1f), body);
            Part(van.transform, PrimitiveType.Cube, "Windshield", new Vector3(0f, 1.45f, 2.46f), new Vector3(1.7f, 0.5f, 0.05f), GrayboxMaterials.Get("Glass", new Color(0.7f, 0.82f, 0.88f)));
            foreach (var w in new[] { new Vector3(-0.95f, 0.4f, 1.6f), new Vector3(0.95f, 0.4f, 1.6f), new Vector3(-0.95f, 0.4f, -1.4f), new Vector3(0.95f, 0.4f, -1.4f) })
                Part(van.transform, PrimitiveType.Cylinder, "Wheel", w, new Vector3(0.75f, 0.15f, 0.75f), dark).localRotation = Quaternion.Euler(0f, 0f, 90f);
            var driver = Part(van.transform, PrimitiveType.Capsule, "SeuAlce_Driver", new Vector3(-0.45f, 1.55f, 1.6f), new Vector3(0.6f, 0.45f, 0.6f), alce);
            Part(driver, PrimitiveType.Cube, "Antlers", new Vector3(0f, 1.2f, 0f), new Vector3(1.6f, 0.15f, 0.4f), GrayboxMaterials.Get("Antler", new Color(0.85f, 0.78f, 0.62f)));
            van.AddComponent<NetworkObject>();
            var nt = van.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            van.AddComponent<ValleyVan>();

            // The loop and the orders board
            var flow = new GameObject("Agency Flow");
            flow.AddComponent<NetworkObject>();
            flow.AddComponent<JobBoard>().catalog = AssetDatabase.LoadAssetAtPath<JobCatalog>("Assets/_Project/Resources/JobCatalog.asset");
            flow.AddComponent<AgencyFlow>();
            flow.AddComponent<SeuAlce>();
            flow.AddComponent<UpgradesPanel>();

            // The kayak camera on Core's camera (AgencyFlow switches between it and CoreView)
            var cam = Camera.main;
            var kc = cam.GetComponent<KayakCamera>();
            if (!kc) kc = cam.gameObject.AddComponent<KayakCamera>();
            kc.enabled = false;
            cam.farClipPlane = 1500f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Campanha] Slice Core built: van, flow, board, kayak camera.");
        }

        static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var t = KayakPrefabBuilder.Primitive(type, name, parent, pos, scale, mat); // (no collider)
            return t;
        }
    }
}
