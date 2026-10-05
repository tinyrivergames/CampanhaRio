using CampanhaRio.River;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// A test forest along the KayakTest river: the approved pine scattered on both banks (6 to 70 m from the water), with
    /// a random size (0.75x to 1.35x) and rotation, sitting on the terrain. A preview of the pine in the game, not the final
    /// dressing (the family's variants and the forest kit come in Phase 3).
    /// Menu: CampanhaRio > Setup > Scatter Test Forest. Batch: CampanhaRio.Editor.ForestScatter.Build
    /// </summary>
    public static class ForestScatter
    {
        const string PinePath = "Assets/_Project/Art/Models/Pine/PinheiroA.fbx";
        const string RootName = "Forest (pine test)";

        [MenuItem("CampanhaRio/Setup/Scatter Test Forest")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(KayakTestBuilder.ScenePath);
            var old = GameObject.Find(RootName);
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;

            var pine = AssetDatabase.LoadAssetAtPath<GameObject>(PinePath);
            var river = Object.FindAnyObjectByType<RiverPath>();
            var terrain = Object.FindAnyObjectByType<Terrain>();
            var rng = new System.Random(2026);
            int placed = 0, hint = -1;
            var bounds = terrain.terrainData.bounds;
            Vector3 origin = terrain.transform.position;
            for (int attempt = 0; attempt < 40000 && placed < 450; attempt++)
            {
                // Mostly near the river (what the kayaker sees), some further back
                float along = (float)rng.NextDouble() * river.Length;
                var s = river.GetPointAtDistance(along);
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float edge = 6f + Mathf.Pow((float)rng.NextDouble(), 1.6f) * 64f;
                Vector3 p = s.point + s.right * side * ((side > 0 ? s.rightWidth : s.leftWidth) + edge);
                if (river.SampleShape(p, ref hint).edgeDistance < 5f) continue; // never in or at the water
                if (p.x < origin.x + 2f || p.z < origin.z + 2f || p.x > origin.x + bounds.size.x - 2f || p.z > origin.z + bounds.size.z - 2f) continue;
                bool crowded = false;
                foreach (Transform t in root) if ((t.position - p).sqrMagnitude < 9f) { crowded = true; break; }
                if (crowded) continue;
                p.y = terrain.SampleHeight(p) + origin.y - 0.1f;
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(pine, root);
                tree.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                tree.transform.localScale = Vector3.one * Mathf.Lerp(0.75f, 1.35f, (float)rng.NextDouble());
                placed++;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Campanha] Test forest: {placed} pines along the KayakTest river.");
        }
    }
}
