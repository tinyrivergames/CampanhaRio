using CampanhaRio.River;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>Prints the KayakTest river's layout (start, finish, terrain) for the segment builders. Batch only.</summary>
    public static class SceneProbe
    {
        public static void KayakTestLayout()
        {
            EditorSceneManager.OpenScene(KayakTestBuilder.ScenePath);
            var river = Object.FindAnyObjectByType<RiverPath>();
            var terrain = Object.FindAnyObjectByType<Terrain>();
            foreach (float d in new[] { 0f, 14f, 60f, river.Length * 0.5f, river.Length - 60f, river.Length - 20f, river.Length })
            {
                var s = river.GetPointAtDistance(d);
                Debug.Log($"[Probe] d {d:0}: point {s.point}, dir {s.direction}, widths L {s.leftWidth:0.0} R {s.rightWidth:0.0}");
            }
            Debug.Log($"[Probe] river length {river.Length:0}, terrain at {terrain.transform.position} size {terrain.terrainData.size}");
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (!t.parent) Debug.Log($"[Probe] root: {t.name} ({t.childCount} children)");
        }
    }
}
