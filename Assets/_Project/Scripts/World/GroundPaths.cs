using System.Collections.Generic;
using UnityEngine;

namespace CampanhaRio.World
{
    /// <summary>
    /// The walking paths of a place (earth trails) and the spots kept clear (buildings, the road): the ground painter
    /// draws the paths in earth, and the grass and the trees stay off them. Plain data, placed by the dressing builders.
    /// </summary>
    public class GroundPaths : MonoBehaviour
    {
        [System.Serializable]
        public class Path
        {
            public Vector3[] points = new Vector3[0];
            public float width = 2f;
        }

        public List<Path> paths = new List<Path>();
        [Tooltip("Areas kept clear (XZ rects: x, z, width, depth).")]
        public List<Rect> keepClear = new List<Rect>();

        static readonly List<GroundPaths> all = new List<GroundPaths>();
        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// <summary>How far p is from the nearest path's edge (negative = on the path), over every loaded place.</summary>
        public static float DistanceToPath(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var g in all) best = Mathf.Min(best, g.Distance(p));
            return best;
        }

        public static bool Cleared(Vector3 p)
        {
            foreach (var g in all) foreach (var r in g.keepClear) if (r.Contains(new Vector2(p.x, p.z))) return true;
            return false;
        }

        public float Distance(Vector3 p)
        {
            float best = float.MaxValue;
            var q = new Vector2(p.x, p.z);
            foreach (var path in paths)
                for (int i = 0; i + 1 < path.points.Length; i++)
                {
                    var a = new Vector2(path.points[i].x, path.points[i].z);
                    var b = new Vector2(path.points[i + 1].x, path.points[i + 1].z);
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
                    best = Mathf.Min(best, Vector2.Distance(q, a + ab * t) - path.width * 0.5f);
                }
            return best;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.4f, 0.2f);
            foreach (var path in paths)
                for (int i = 0; i + 1 < path.points.Length; i++) Gizmos.DrawLine(path.points[i], path.points[i + 1]);
        }
    }
}
