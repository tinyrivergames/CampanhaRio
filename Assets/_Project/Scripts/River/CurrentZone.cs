using System.Collections.Generic;
using UnityEngine;

namespace CampanhaRio.River
{
    public enum CurrentZoneType { Strong, Normal, Slow, Backwater }

    /// <summary>
    /// A box that scales the river current inside it (e.g. 0.05 = backwater, 1.5 = strong current).
    /// Edges fade smoothly so the kayak never hits a sudden speed wall.
    /// Pure math, no physics triggers: RiverPath asks every zone for its multiplier at a position.
    /// Keep the transform's scale at 1 and use Size instead.
    /// </summary>
    [ExecuteAlways]
    public class CurrentZone : MonoBehaviour
    {
        public CurrentZoneType type = CurrentZoneType.Backwater;
        [Tooltip("Multiplies the river current inside the zone. 0 = still water, 1 = unchanged, >1 = stronger.")]
        [Min(0f)] public float currentMultiplier = 0.05f;
        [Tooltip("Zone size in meters (X = across, Z = along). Y is only for the gizmo.")]
        public Vector3 size = new Vector3(20f, 3f, 40f);
        [Tooltip("Meters over which the effect fades in at the edges.")]
        [Min(0.01f)] public float edgeFade = 6f;

        static readonly List<CurrentZone> zones = new List<CurrentZone>();

        /// <summary>Increments whenever any zone is added, removed or edited (the water mesh rebuilds on change).</summary>
        public static int Version { get; private set; }

        void OnEnable() { zones.Add(this); Version++; }
        void OnDisable() { zones.Remove(this); Version++; }
        void OnValidate() => Version++;

        void Update()
        {
            if (!transform.hasChanged) return;
            transform.hasChanged = false;
            Version++;
        }

        /// <summary>0 outside, 1 fully inside.</summary>
        public float GetWeight(Vector3 worldPos)
        {
            Vector3 local = transform.InverseTransformPoint(worldPos);
            float insideX = size.x * 0.5f - Mathf.Abs(local.x);
            float insideZ = size.z * 0.5f - Mathf.Abs(local.z);
            return Mathf.Clamp01(Mathf.Min(insideX, insideZ) / edgeFade);
        }

        /// <summary>Combined current multiplier of all zones at a position (1 when outside every zone).</summary>
        public static float Evaluate(Vector3 worldPos, out CurrentZone strongest)
        {
            float multiplier = 1f;
            float bestWeight = 0f;
            strongest = null;
            foreach (var zone in zones)
            {
                float weight = zone.GetWeight(worldPos);
                if (weight <= 0f) continue;
                multiplier *= Mathf.Lerp(1f, zone.currentMultiplier, weight);
                if (weight > bestWeight) { bestWeight = weight; strongest = zone; }
            }
            return multiplier;
        }

        void OnDrawGizmos()
        {
            Color color = type switch
            {
                CurrentZoneType.Strong => new Color(1f, 0.3f, 0.2f),
                CurrentZoneType.Slow => new Color(1f, 0.85f, 0.2f),
                CurrentZoneType.Backwater => new Color(0.2f, 1f, 0.6f),
                _ => Color.white,
            };
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = color;
            Gizmos.DrawWireCube(Vector3.zero, size);
            Vector3 inner = new Vector3(Mathf.Max(0f, size.x - edgeFade * 2f), size.y, Mathf.Max(0f, size.z - edgeFade * 2f));
            Gizmos.color = new Color(color.r, color.g, color.b, 0.12f);
            Gizmos.DrawCube(Vector3.zero, inner);
        }
    }
}
