using System.Collections.Generic;
using UnityEngine;

namespace CampanhaRio.World
{
    /// <summary>
    /// A point of interest the paddler turns to look at when nearby (a pretty tree, a cliff, the campfire, a log).
    /// Put it on the object (or an empty at the interesting spot).
    /// </summary>
    public class LookTarget : MonoBehaviour
    {
        [Tooltip("How interesting this is compared to other targets.")]
        [Range(0.1f, 3f)] public float interest = 1f;
        [Tooltip("Noticed within this distance (m).")]
        public float radius = 25f;
        [Tooltip("Look at this offset from the transform (local).")]
        public Vector3 offset;

        static readonly List<LookTarget> all = new List<LookTarget>();
        public static IReadOnlyList<LookTarget> All => all;

        public Vector3 Point => transform.TransformPoint(offset);

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(Point, 0.4f);
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.15f);
            Gizmos.DrawWireSphere(Point, radius);
        }
    }
}
