using UnityEngine;

namespace CampanhaRio.River
{
    /// <summary>
    /// A creek (or a bank shape) pushing water sideways into the river. The push follows the transform's forward
    /// (flat) inside a box that fades at its edges; a soft foam seam shows where the two waters meet.
    /// Keep the transform's scale at 1 and use Size instead.
    /// </summary>
    [ExecuteAlways]
    public class CrossCurrent : MonoBehaviour, IRiverModifier
    {
        [Tooltip("Sideways push at the core (m/s), along the transform's forward.")]
        public float strength = 1.6f;
        [Tooltip("Box size (m): X = width across the inflow, Z = how far it reaches into the river.")]
        public Vector2 size = new Vector2(8f, 10f);
        [Tooltip("Fade at the box edges (m).")]
        [Min(0.1f)] public float edgeFade = 3f;
        [Range(0f, 1f)] public float seamFoam = 0.4f;

        public string DisplayName => "Cross current (" + name + ")";
        public Bounds Influence { get; private set; }

        Vector3 lastPosition; Quaternion lastRotation; Vector2 lastSize;

        void OnEnable() { Refresh(); RiverModifiers.Register(this); }
        void OnDisable() => RiverModifiers.Unregister(this);
        void OnValidate() { Refresh(); RiverModifiers.Changed(); }

        void Update()
        {
            if (transform.position == lastPosition && transform.rotation == lastRotation && size == lastSize) return;
            Refresh();
            RiverModifiers.Changed();
        }

        void Refresh()
        {
            lastPosition = transform.position; lastRotation = transform.rotation; lastSize = size;
            float reach = Mathf.Max(size.x, size.y);
            Influence = new Bounds(transform.position, new Vector3(reach * 1.5f, 50f, reach * 1.5f));
        }

        public float Weight(Vector3 position)
        {
            Vector3 local = transform.InverseTransformPoint(position);
            float inX = size.x * 0.5f - Mathf.Abs(local.x);
            float inZ = size.y * 0.5f - Mathf.Abs(local.z);
            return Mathf.Clamp01(Mathf.Min(inX, inZ) / edgeFade);
        }

        public float HeightOffset(Vector3 position, RiverPath river) => 0f;

        public void ModifyFlow(Vector3 position, in RiverPath.RiverSample sample, ref Vector3 velocity)
        {
            float w = Weight(position);
            if (w <= 0f) return;
            Vector3 push = transform.forward; push.y = 0f;
            velocity += push.normalized * (strength * w);
        }

        public float Foam(Vector3 position, RiverPath river)
        {
            float w = Weight(position);
            return w * (1f - w) * 4f * seamFoam;
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.7f);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 1f, size.y));
            Gizmos.matrix = Matrix4x4.identity;
            RiverPath.DrawArrow(transform.position + Vector3.up * 0.4f, transform.forward * strength, Color.cyan);
        }
    }
}
