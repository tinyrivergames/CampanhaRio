using UnityEngine;

namespace CampanhaRio.Van
{
    /// <summary>
    /// The van's road (Stage 11, built by VanRoadBuilder from a closed Splines road): one loop that starts at the take-out's
    /// stop (distance 0), climbs the road to the camp's turning loop and its stop (CampStopDistance), and comes back down the
    /// same road to the take-out's turning loop. Going up is 0 → CampStop, going down is CampStop → Length. The two lanes
    /// share the road between the loops (one road, driven both ways). At runtime it is a baked polyline, one point a metre
    /// at the road's surface, for the auto-driver, the resets and "where is the van".
    /// </summary>
    public class VanRoad : MonoBehaviour
    {
        public static VanRoad Instance { get; private set; }

        [Tooltip("The circuit's centre line at the road surface, one point a metre (closed: the last point leads back to the first).")]
        public Vector3[] points = new Vector3[0];
        [Tooltip("Where the camp's stop is along the circuit (m).")]
        public float campStopDistance;
        [Tooltip("Half the road's width (m).")]
        public float halfWidth = 2.2f;
        public Transform takeOutStop, campStop;

        /// <summary>The circuit's length (m).</summary>
        public float Length => points.Length;
        /// <summary>The road from the take-out to the camp (m): the drive up.</summary>
        public float UpLength => campStopDistance;

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        public float Wrap(float d) { float l = Mathf.Max(Length, 1f); return ((d % l) + l) % l; }

        /// <summary>The point at a distance along the circuit (wraps).</summary>
        public Vector3 PointAt(float d)
        {
            if (points.Length == 0) return transform.position;
            d = Wrap(d);
            int i = Mathf.FloorToInt(d) % points.Length;
            return Vector3.Lerp(points[i], points[(i + 1) % points.Length], d - Mathf.Floor(d));
        }

        /// <summary>The direction of travel at a distance (flat).</summary>
        public Vector3 DirectionAt(float d)
        {
            Vector3 dir = PointAt(d + 2f) - PointAt(d - 2f); dir.y = 0f;
            return dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
        }

        /// <summary>
        /// The circuit distance nearest to a position, and how far off the road it is. Where the two lanes overlap, the one
        /// whose direction matches <paramref name="heading"/> wins (zero: either).
        /// </summary>
        public float Nearest(Vector3 p, Vector3 heading, out float offset)
        {
            float best = float.MaxValue, bestD = 0f;
            heading.y = 0f;
            bool useHeading = heading.sqrMagnitude > 0.01f;
            if (useHeading) heading.Normalize();
            int n = points.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = points[i], b = points[(i + 1) % n];
                Vector2 pa = new Vector2(p.x - a.x, p.z - a.z), ab = new Vector2(b.x - a.x, b.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(pa, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                float dist = (pa - ab * t).magnitude;
                // The wrong lane costs 6 m: the overlapping lanes are a metre or two apart
                if (useHeading && ab.sqrMagnitude > 1e-5f && Vector2.Dot(ab.normalized, new Vector2(heading.x, heading.z)) < 0f) dist += 6f;
                if (dist < best) { best = dist; bestD = i + t; }
            }
            offset = best;
            return bestD;
        }

        public float Nearest(Vector3 p) => Nearest(p, Vector3.zero, out _);

        /// <summary>The nearest circuit distance within a window around a known one (from back to ahead metres).</summary>
        public float NearestAround(Vector3 p, float around, float back, float ahead, out float offset)
        {
            float best = float.MaxValue, bestD = around;
            int n = points.Length;
            for (int k = -Mathf.CeilToInt(back); k <= Mathf.CeilToInt(ahead); k++)
            {
                int i = ((Mathf.FloorToInt(around) + k) % n + n) % n;
                Vector3 a = points[i], b = points[(i + 1) % n];
                Vector2 pa = new Vector2(p.x - a.x, p.z - a.z), ab = new Vector2(b.x - a.x, b.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(pa, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                float dist = (pa - ab * t).magnitude;
                if (dist < best) { best = dist; bestD = Mathf.Floor(around) + k + t; }
            }
            offset = best;
            return Wrap(bestD);
        }

        /// <summary>Is this circuit distance on the way up (take-out → camp)?</summary>
        public bool IsUp(float d) { d = Wrap(d); return d < campStopDistance; }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.6f, 0.3f);
            for (int i = 0; i + 5 < points.Length; i += 5) Gizmos.DrawLine(points[i], points[i + 5]);
        }
    }
}
