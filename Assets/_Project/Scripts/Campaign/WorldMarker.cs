using System.Collections.Generic;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// A named spot in a segment that the campaign flow uses (the orders board, the van's stops, the road down to the
    /// river, Seu Alce's spot...). Segments are plain scenes: everyone has the same markers. Placed by WorldBuilder.
    /// </summary>
    public class WorldMarker : MonoBehaviour
    {
        public enum Kind { Entry, JobBoard, VanStopAgency, VanStopPutIn, VanStopArrival, SeuAlce, Landing, RoadPoint }

        static readonly List<WorldMarker> all = new List<WorldMarker>();
        public static IReadOnlyList<WorldMarker> All => all;

        public Kind kind;
        [Tooltip("Order among markers of the same kind (the road's points).")]
        public int index;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static WorldMarker Find(Kind kind) => all.Find(m => m.kind == kind);

        /// <summary>The road points loaded now, in order.</summary>
        public static List<WorldMarker> Road()
        {
            var road = all.FindAll(m => m.kind == Kind.RoadPoint);
            road.Sort((a, b) => a.index.CompareTo(b.index));
            return road;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, 0.6f);
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}
