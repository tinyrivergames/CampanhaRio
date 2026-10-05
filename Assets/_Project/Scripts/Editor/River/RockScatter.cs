using CampanhaRio.River;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Test rocks along the KayakTest river: the approved rock in small groups (one bigger stone and a few smaller ones
    /// around it), mostly at the water's edge (some half in the shallows), fewer further into the forest; random size
    /// (0.35x to 1.25x), yaw and a slight tilt, sunk a little into the terrain. A preview of the rock in the game, not the
    /// final dressing (the family's variants come in Phase 3).
    /// Menu: CampanhaRio > Setup > Scatter Test Rocks. Batch: CampanhaRio.Editor.RockScatter.Build
    /// </summary>
    public static class RockScatter
    {
        const string RockPath = "Assets/_Project/Art/Models/Rock/RochaA.fbx";
        const string RootName = "Rocks (rock test)";
        const string PinesName = "Forest (pine test)";

        [MenuItem("CampanhaRio/Setup/Scatter Test Rocks")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(KayakTestBuilder.ScenePath);
            var old = GameObject.Find(RootName);
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject(RootName).transform;
            var pines = GameObject.Find(PinesName);

            var rock = AssetDatabase.LoadAssetAtPath<GameObject>(RockPath);
            var river = Object.FindAnyObjectByType<RiverPath>();
            var terrain = Object.FindAnyObjectByType<Terrain>();
            var rng = new System.Random(4242);
            float Rand(float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
            int groups = 0, placed = 0, hint = -1;
            var bounds = terrain.terrainData.bounds;
            Vector3 origin = terrain.transform.position;
            for (int attempt = 0; attempt < 20000 && groups < 110; attempt++)
            {
                // Mostly right at the bank (what the kayaker passes by), some further into the forest
                float along = Rand(0f, river.Length);
                var s = river.GetPointAtDistance(along);
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float edge = rng.NextDouble() < 0.7 ? Rand(-1.2f, 3f) : Rand(3f, 30f);
                Vector3 centre = s.point + s.right * side * ((side > 0 ? s.rightWidth : s.leftWidth) + edge);
                int count = 1 + rng.Next(4);
                bool any = false;
                for (int k = 0; k < count; k++)
                {
                    float scale = k == 0 ? Rand(0.7f, 1.25f) : Rand(0.35f, 0.7f);
                    Vector3 p = centre + (k == 0 ? Vector3.zero : Quaternion.Euler(0f, Rand(0f, 360f), 0f) * Vector3.forward * Rand(1.6f, 3.2f));
                    if (river.SampleShape(p, ref hint).edgeDistance < -1.5f) continue; // never out in the channel
                    if (p.x < origin.x + 2f || p.z < origin.z + 2f || p.x > origin.x + bounds.size.x - 2f || p.z > origin.z + bounds.size.z - 2f) continue;
                    if (Near(root, p, 1.2f * scale + 0.6f) || (pines && Near(pines.transform, p, 2f))) continue;
                    p.y = terrain.SampleHeight(p) + origin.y - 0.2f * scale; // sunk a little, never sitting on top
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(rock, root);
                    go.transform.SetPositionAndRotation(p, Quaternion.Euler(Rand(-6f, 6f), Rand(0f, 360f), Rand(-6f, 6f)));
                    go.transform.localScale = Vector3.one * scale;
                    placed++;
                    any = true;
                }
                if (any) groups++;
            }
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Campanha] Test rocks: {placed} rocks in {groups} groups along the KayakTest river.");
        }

        static bool Near(Transform parent, Vector3 p, float distance)
        {
            foreach (Transform t in parent)
                if ((t.position - p).sqrMagnitude < distance * distance) return true;
            return false;
        }
    }
}
