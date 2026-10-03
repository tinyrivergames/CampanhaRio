using UnityEngine;

namespace CampanhaRio.River
{
    /// <summary>
    /// Makes the water react to a rock, a logjam or a gravel spit in the flow:
    ///   upstream: the flow splits and deflects around it;
    ///   downstream: an EDDY, a teardrop pocket of slow water circling back upstream, separated from the main current
    ///   by a sharp eddy line (strong shear, a thin broken foam seam). Steer into the pocket and the kayak parks there;
    ///   cross the line out and the current catches the bow and spins you downstream.
    /// Put it on the obstacle (radius auto from its collider). It reads the base flow at its position.
    /// </summary>
    [ExecuteAlways]
    public class FlowObstacle : MonoBehaviour, IRiverModifier
    {
        [Tooltip("Obstacle radius at the water (m). 0 = from its renderer bounds.")]
        public float radius;
        [Tooltip("Eddy length behind the obstacle, in radii.")]
        [Range(1f, 10f)] public float eddyLength = 4f;
        [Tooltip("Eddy width relative to the obstacle.")]
        [Range(0.5f, 2.5f)] public float eddyWidth = 1.15f;
        [Tooltip("Playable minimums: an eddy must hold a 3.5 m kayak (length m, half-width m).")]
        public float minEddyLength = 6f;
        public float minEddyHalfWidth = 1.2f;
        [Tooltip("Upstream return flow in the eddy core (share of the main current).")]
        [Range(0f, 1f)] public float returnFlow = 0.45f;
        [Tooltip("Circulation inside the pocket (share of the main current).")]
        [Range(0f, 1f)] public float circulation = 0.25f;
        [Tooltip("How strongly the flow is pushed around the upstream face (share of the main current).")]
        [Range(0f, 1f)] public float deflection = 0.5f;
        [Tooltip("Foam seam on the eddy line.")]
        [Range(0f, 1f)] public float seamFoam = 0.7f;

        public string DisplayName => "Eddy (" + name + ")";
        /// <summary>Effective radius and pocket length (m), for tools and bots.</summary>
        public float Radius => r;
        public float PocketLength => length;
        public Bounds Influence { get; private set; }

        RiverPath river;
        Vector3 center, flowDir, flowRight;
        float speed, r, length;
        Vector3 lastPosition;
        float lastRadius = -1f, lastLength = -1f;
        int riverVersion = -1;

        void OnEnable() { river = RiverPath.Resolve(transform.position); Refresh(); RiverModifiers.Register(this); }
        void OnDisable() => RiverModifiers.Unregister(this);
        void OnValidate() { Refresh(); RiverModifiers.Changed(); }

        void Update()
        {
            if (transform.position != lastPosition || radius != lastRadius || eddyLength != lastLength || (river && river.Version != riverVersion))
            {
                Refresh();
                RiverModifiers.Changed();
            }
        }

        public void Refresh()
        {
            if (!river) river = RiverPath.Resolve(transform.position);
            lastPosition = transform.position; lastRadius = radius; lastLength = eddyLength;
            r = radius;
            if (r <= 0f)
            {
                // Renderer bounds are always current (collider bounds lag in Edit mode until physics syncs)
                var rend = GetComponentInChildren<Renderer>();
                r = rend ? Mathf.Max(rend.bounds.extents.x, rend.bounds.extents.z) * 0.85f : 1f;
            }
            center = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 v = river ? river.BaseFlow(transform.position) : Vector3.forward;
            speed = v.magnitude;
            if (river) riverVersion = river.Version;
            flowDir = speed > 0.05f ? v / speed : (river ? river.Sample(transform.position).direction : Vector3.forward);
            flowDir.y = 0f; flowDir.Normalize();
            flowRight = new Vector3(flowDir.z, 0f, -flowDir.x);
            length = Mathf.Max(r * eddyLength, minEddyLength);
            float reach = length + r * 3f;
            Influence = new Bounds(transform.position, new Vector3(reach * 2f, 50f, reach * 2f));
        }

        void Local(Vector3 p, out float along, out float across)
        {
            Vector3 d = new Vector3(p.x, 0f, p.z) - center;
            along = Vector3.Dot(d, flowDir);
            across = Vector3.Dot(d, flowRight);
        }

        float PocketHalfWidth(float along) => Mathf.Max(r * eddyWidth, minEddyHalfWidth) * Mathf.Lerp(1f, 0.15f, Mathf.Clamp01(along / length));

        /// <summary>0..1: how deep inside the eddy pocket a point is.</summary>
        float Pocket(float along, float across)
        {
            if (along <= 0f || along >= length) return 0f;
            float w = PocketHalfWidth(along);
            float inside = 1f - Smooth(w * 0.7f, w * 1.1f, Mathf.Abs(across));
            float taper = Smooth(0f, r * 0.6f, along) * (1f - Smooth(length * 0.7f, length, along));
            return inside * taper;
        }

        public float HeightOffset(Vector3 position, RiverPath river) => 0f;

        public void ModifyFlow(Vector3 position, in RiverPath.RiverSample sample, ref Vector3 velocity)
        {
            Local(position, out float along, out float across);
            float main = Mathf.Max(speed, velocity.magnitude);

            // Split around the upstream face
            if (along > -r * 3f && along < r * 0.5f && Mathf.Abs(across) < r * 2.2f)
            {
                float face = Mathf.Exp(-Sq((along + r * 0.8f) / (r * 1.4f))) * (1f - Smooth(r * 1.2f, r * 2.2f, Mathf.Abs(across)));
                velocity += flowRight * (Mathf.Sign(across == 0f ? 1f : across) * main * deflection * face);
                velocity *= 1f - 0.35f * face * (1f - Smooth(0f, r * 1.1f, Mathf.Abs(across))); // pillow: slows right in front
            }

            // Eddy pocket behind: return flow upstream in the core, circulation toward the line
            float pocket = Pocket(along, across);
            if (pocket > 0f)
            {
                float w = PocketHalfWidth(along);
                float core = 1f - Mathf.Clamp01(Mathf.Abs(across) / w);
                Vector3 eddy = -flowDir * (main * returnFlow * core)
                             + flowRight * (-Mathf.Sign(across) * main * circulation * (along / length));
                velocity = Vector3.Lerp(velocity, eddy, pocket);
            }
        }

        public float Foam(Vector3 position, RiverPath river)
        {
            Local(position, out float along, out float across);
            if (along < r * 0.5f || along > length) return 0f;
            float w = PocketHalfWidth(along);
            float seam = Mathf.Exp(-Sq((Mathf.Abs(across) - w) / 0.35f));
            float fade = 1f - Mathf.Clamp01(along / length);
            return seam * fade * Mathf.Clamp01(speed / 2.5f) * seamFoam;
        }

        public float Weight(Vector3 position)
        {
            Local(position, out float along, out float across);
            return Pocket(along, across);
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
        static float Sq(float x) => x * x;

        void OnDrawGizmosSelected()
        {
            if (lastRadius < 0f) Refresh();
            Vector3 c = new Vector3(center.x, transform.position.y + 0.2f, center.z);
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(c, r);
            Vector3 prevL = c, prevR = c;
            for (int i = 1; i <= 12; i++)
            {
                float along = length * i / 12f, w = PocketHalfWidth(along);
                Vector3 l = c + flowDir * along - flowRight * w, rr = c + flowDir * along + flowRight * w;
                Gizmos.DrawLine(prevL, l); Gizmos.DrawLine(prevR, rr);
                prevL = l; prevR = rr;
            }
            RiverPath.DrawArrow(c + flowDir * length * 0.5f, -flowDir * 1.5f, Color.cyan);
        }
    }
}
