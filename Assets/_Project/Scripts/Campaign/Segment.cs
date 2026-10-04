using System.Collections.Generic;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// One stretch of the world (a road, a river, a camp), in its own scene, loaded additively on top of Core.
    /// The streamer reads two boxes: <see cref="area"/> (who is in this segment) and <see cref="loadNextZone"/>
    /// (entering it loads the next segment). Plain scene objects only: everyone has the same copy.
    /// </summary>
    public class Segment : MonoBehaviour
    {
        static readonly List<Segment> loaded = new List<Segment>();
        public static IReadOnlyList<Segment> Loaded => loaded;

        [Tooltip("The scene's name (also the checkpoint id).")]
        public string id;
        [Tooltip("The segment's area in world space (only X and Z matter).")]
        public Bounds area = new Bounds(new Vector3(0f, 0f, 60f), new Vector3(40f, 20f, 120f));
        [Tooltip("When any player enters this box, the next segment starts loading.")]
        public Bounds loadNextZone = new Bounds(new Vector3(0f, 0f, 100f), new Vector3(40f, 20f, 40f));
        [Tooltip("Where players appear when this segment is the checkpoint.")]
        public Transform entry;

        void OnEnable() => loaded.Add(this);
        void OnDisable() => loaded.Remove(this);

        public bool Contains(Vector3 p) => Flat(area, p);
        public bool InLoadNextZone(Vector3 p) => Flat(loadNextZone, p);

        static bool Flat(Bounds b, Vector3 p) =>
            p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;

        public static Segment Find(string id) => loaded.Find(s => s.id == id);

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(area.center, area.size);
            Gizmos.color = new Color(1f, 0.7f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(loadNextZone.center, loadNextZone.size);
        }
    }
}
