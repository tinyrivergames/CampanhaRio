using UnityEngine;

namespace CampanhaRio.River
{
    public enum RiverFeatureType { Ledge, ChuteWaveTrain, PourOver, LogRamp, Riffle }

    /// <summary>Extra per-vertex surface detail baked into the water mesh (UV3) for features.</summary>
    public interface IRiverSurfaceDetail
    {
        /// <summary>pulse = standing-wave breathing amplitude (m), phase = its phase, chop = tiny choppy waves (0..1), glassy = smooth tongue (0..1).</summary>
        void SurfaceDetail(Vector3 position, ref float pulse, ref float phase, ref float chop, ref float glassy);
    }

    /// <summary>
    /// A water feature with a physical reason to exist, shaping the water HEIGHT, the FLOW, the FOAM and the surface
    /// detail together. Jumps come from these shapes and the kayak's speed (no scripted jump boxes):
    ///   Ledge           a rock shelf step: flat pool → glassy convex lip → drop → foam pile with a slight upstream pull
    ///   ChuteWaveTrain  a narrowing speeds the water up, then 3-5 standing waves of decreasing height that gently breathe
    ///   PourOver        a submerged boulder: a smooth hump, then a small hole with a boil
    ///   LogRamp         a partly submerged log angled against the flow: a small ramp of water over it
    ///   Riffle          a shallow gravel bed: dense tiny chop and glints, no big jumps (the physics sees flat water)
    /// Placed on the river (its transform is snapped to the water; it orients itself to the local flow).
    /// Editing any value rebakes the water mesh.
    /// </summary>
    [ExecuteAlways]
    public class RiverFeature : MonoBehaviour, IRiverModifier, IRiverSurfaceDetail
    {
        public RiverFeatureType type = RiverFeatureType.ChuteWaveTrain;
        [Tooltip("Ledge: the drop. Waves: the first (tallest) wave. Pour-over / log ramp: the hump (m).")]
        public float height = 0.6f;
        [Tooltip("Length along the flow (m). Waves: the whole train. Ledge: the pool + drop + pile window.")]
        public float length = 20f;
        [Tooltip("Width across the flow (m). 0 = the whole river here.")]
        public float width = 0f;
        [Tooltip("Strength of the flow changes and foam (0..1).")]
        [Range(0f, 1.5f)] public float intensity = 1f;

        [Header("Waves (ChuteWaveTrain)")]
        [Range(1, 6)] public int waveCount = 4;
        [Tooltip("Length of the accelerating chute upstream of the first wave (m).")]
        public float chuteLength = 10f;
        [Tooltip("Share of height lost per wave down the train.")]
        [Range(0f, 0.4f)] public float waveDecay = 0.22f;
        [Tooltip("Standing waves breathe: share of the wave height (rate: PulseRate, matched by the water shader).")]
        [Range(0f, 0.4f)] public float pulse = 0.12f;
        public const float PulseRate = 1.4f;

        [Header("Log ramp")]
        [Tooltip("Angle of the log against the flow (degrees, 0 = straight across).")]
        public float logAngle = 25f;

        public string DisplayName => type + " (" + name + ")";
        public Bounds Influence { get; private set; }

        RiverPath river;
        Vector3 center, dir, right;
        float halfWidth, baseSpeed, splineHeight;
        Vector3 lastPosition;
        int lastHash, riverVersion = -1;

        void OnEnable() { Refresh(); RiverModifiers.Register(this); }
        void OnDisable() => RiverModifiers.Unregister(this);
        void OnValidate() { Refresh(); RiverModifiers.Changed(); }

        void Update()
        {
            int hash = Hash();
            if (transform.position == lastPosition && hash == lastHash && (!river || river.Version == riverVersion)) return;
            Refresh();
            RiverModifiers.Changed();
        }

        int Hash() => (type, height, length, width, intensity, waveCount, chuteLength, logAngle).GetHashCode();

        public void Refresh()
        {
            if (!river) river = RiverPath.Resolve(transform.position);
            lastPosition = transform.position;
            lastHash = Hash();
            if (!river) return;
            riverVersion = river.Version;
            int hint = -1;
            var s = river.SampleShape(transform.position, ref hint);
            center = new Vector3(transform.position.x, 0f, transform.position.z);
            dir = s.direction;
            right = s.right;
            splineHeight = s.point.y;
            float riverWidth = s.leftWidth + s.rightWidth;
            halfWidth = (width > 0f ? width : riverWidth + 2f) * 0.5f;
            baseSpeed = river.BaseFlow(transform.position).magnitude;
            float front = type == RiverFeatureType.ChuteWaveTrain ? chuteLength : length * 0.6f;
            float reach = Mathf.Max(front, length) + halfWidth + 4f;
            Influence = new Bounds(transform.position, new Vector3(reach * 2f, 60f, reach * 2f));
        }

        void Local(Vector3 p, out float a, out float x)
        {
            Vector3 d = new Vector3(p.x, 0f, p.z) - center;
            a = Vector3.Dot(d, dir);
            x = Vector3.Dot(d, right);
        }

        float SideFade(float x)
        {
            float fade = Mathf.Min(2f, halfWidth * 0.5f);
            return 1f - Smooth(halfWidth - fade, halfWidth, Mathf.Abs(x));
        }

        // ------------------------------------------------------------------ height

        public float HeightOffset(Vector3 p, RiverPath r)
        {
            Local(p, out float a, out float x);
            float side = SideFade(x);
            if (side <= 0f) return 0f;
            switch (type)
            {
                case RiverFeatureType.Ledge: return height * LedgeProfile(a) * side;
                case RiverFeatureType.ChuteWaveTrain: return WaveTrain(a, out _, out _) * side;
                case RiverFeatureType.PourOver:
                {
                    float half = length * 0.5f;
                    float round = RoundFade(a, x, half);
                    return (height * Bump(a, half * 0.8f) - height * 0.35f * Bump(a - half * 0.7f, half * 0.45f)) * round;
                }
                case RiverFeatureType.LogRamp:
                {
                    float u = LogDistance(a, x, out float along);
                    if (Mathf.Abs(along) > halfWidth) return 0f;
                    float ramp = u < 0f ? Mathf.Pow(Smooth(-length * 0.25f, 0f, u), 1.5f) : 1f - Smooth(0f, 0.9f, u);
                    return height * ramp * (1f - Smooth(halfWidth - 1f, halfWidth, Mathf.Abs(along)));
                }
                default: return 0f; // riffle: visual chop only
            }
        }

        /// <summary>-0.5..+0.5 of the drop: pool rises, convex lip, drop, foam pile, back to the river surface.</summary>
        float LedgeProfile(float a)
        {
            float up = length * 0.6f, down = length * 0.4f, lip = 1.1f;
            if (a <= -up || a >= down) return 0f;
            if (a < -lip) return 0.5f * Smooth(-up, -lip, a);
            if (a < lip * 0.35f)
            {
                float t = (a + lip) / (lip * 1.35f);
                return 0.5f - (1f - Mathf.Cos(Mathf.PI * t)) * 0.5f;      // convex glassy lip into the drop
            }
            float back = Smooth(lip * 0.35f, down, a);
            float pile = 0.14f * Mathf.Exp(-Sq((a - 2f) / 1f));              // the hydraulic foam pile
            return -0.5f + 0.5f * back + pile;
        }

        float WaveTrain(float a, out float crest, out int index)
        {
            float spacing = length / Mathf.Max(1, waveCount);
            float h = 0f;
            crest = 0f;
            index = -1;
            for (int i = 0; i < waveCount; i++)
            {
                float b = Bump(a - (i + 0.5f) * spacing, spacing * 0.55f);
                if (b <= 0f) continue;
                float hi = height * Mathf.Max(0.1f, 1f - waveDecay * i);
                h += hi * b;
                if (b > crest) { crest = b; index = i; }
            }
            return h;
        }

        /// <summary>Standing waves breathe a little at runtime (the water shader does the same from UV3).</summary>
        public float DynamicHeight(Vector3 p, float time)
        {
            if (type != RiverFeatureType.ChuteWaveTrain || pulse <= 0f) return 0f;
            float amp = 0f, phase = 0f, chop = 0f, glassy = 0f;
            SurfaceDetail(p, ref amp, ref phase, ref chop, ref glassy);
            return amp * Mathf.Sin(time * PulseRate + phase);
        }

        public void SurfaceDetail(Vector3 p, ref float pulseAmp, ref float phase, ref float chop, ref float glassy)
        {
            Local(p, out float a, out float x);
            float side = SideFade(x);
            if (side <= 0f) return;
            switch (type)
            {
                case RiverFeatureType.ChuteWaveTrain:
                {
                    float h = WaveTrain(a, out _, out int index);
                    if (index >= 0) { pulseAmp = Mathf.Max(pulseAmp, h * pulse * side); phase = index * 1.7f; }
                    chop = Mathf.Max(chop, 0.5f * intensity * side * (a > 0f && a < length ? 1f : 0f));
                    if (a > -chuteLength && a < 0f) glassy = Mathf.Max(glassy, 0.5f * side); // smooth fast tongue
                    break;
                }
                case RiverFeatureType.Ledge:
                    if (a > -2.5f && a < 0.3f) glassy = Mathf.Max(glassy, side * Smooth(-2.5f, -1.2f, a));
                    if (a > 0.5f && a < length * 0.4f) chop = Mathf.Max(chop, side * intensity);
                    break;
                case RiverFeatureType.Riffle:
                    if (a > -length * 0.5f && a < length * 0.5f)
                        chop = Mathf.Max(chop, intensity * side * (1f - Smooth(length * 0.35f, length * 0.5f, Mathf.Abs(a))));
                    break;
                case RiverFeatureType.PourOver:
                    if (RoundFade(a, x, length * 0.5f) > 0f && a < 0f) glassy = Mathf.Max(glassy, 0.6f * RoundFade(a, x, length * 0.5f));
                    break;
            }
        }

        // ------------------------------------------------------------------ flow and foam

        public void ModifyFlow(Vector3 p, in RiverPath.RiverSample sample, ref Vector3 velocity)
        {
            Local(p, out float a, out float x);
            float side = SideFade(x);
            if (side <= 0f) return;
            float speed = Mathf.Max(baseSpeed, velocity.magnitude);
            switch (type)
            {
                case RiverFeatureType.Ledge:
                    if (a > -3f && a < 0.2f) velocity *= 1f + 0.25f * intensity * side;                      // the tongue speeds up
                    if (a > 0.3f && a < 3f) velocity -= dir * (speed * 0.35f * intensity * side * Bump(a - 1.5f, 1.3f)); // hydraulic pull
                    break;
                case RiverFeatureType.ChuteWaveTrain:
                    if (a > -chuteLength && a < length * 0.3f) velocity *= 1f + 0.35f * intensity * side * Smooth(-chuteLength, -chuteLength * 0.4f, a);
                    break;
                case RiverFeatureType.PourOver:
                {
                    float half = length * 0.5f, round = RoundFade(a, x, half);
                    velocity *= 1f + 0.15f * round * Bump(a, half);
                    velocity -= dir * (speed * 0.3f * intensity * round * Bump(a - half * 0.7f, half * 0.45f));
                    break;
                }
                case RiverFeatureType.Riffle:
                    if (Mathf.Abs(a) < length * 0.5f) velocity *= 1f + 0.1f * side;
                    break;
            }
        }

        public float Foam(Vector3 p, RiverPath r)
        {
            Local(p, out float a, out float x);
            float side = SideFade(x);
            if (side <= 0f) return 0f;
            switch (type)
            {
                case RiverFeatureType.Ledge:
                    return Smooth(0f, 0.6f, a) * (1f - Smooth(2.5f, length * 0.4f, a)) * intensity * side;
                case RiverFeatureType.ChuteWaveTrain:
                {
                    WaveTrain(a, out float crest, out _);
                    return Smooth(0.55f, 0.95f, crest) * 0.85f * intensity * side;
                }
                case RiverFeatureType.PourOver:
                {
                    float half = length * 0.5f;
                    return Bump(a - half * 0.75f, half * 0.5f) * RoundFade(a, x, half) * 0.8f * intensity;
                }
                case RiverFeatureType.LogRamp:
                {
                    float u = LogDistance(a, x, out float along);
                    if (Mathf.Abs(along) > halfWidth) return 0f;
                    return Mathf.Exp(-Sq(u / 0.45f)) * 0.8f * intensity + (u > 0f ? Bump(u - 1f, 1.2f) * 0.4f * intensity : 0f);
                }
                default:
                    return 0f;
            }
        }

        public float Weight(Vector3 p)
        {
            Local(p, out float a, out float x);
            float side = SideFade(x);
            float front = type == RiverFeatureType.ChuteWaveTrain ? chuteLength : type == RiverFeatureType.Ledge ? length * 0.6f : length * 0.5f;
            float back = type == RiverFeatureType.Ledge ? length * 0.4f : length * (type == RiverFeatureType.ChuteWaveTrain ? 1f : 0.5f);
            return a > -front && a < back ? side : 0f;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Signed distance from the log line (+ downstream) and position along the log.</summary>
        float LogDistance(float a, float x, out float along)
        {
            float t = logAngle * Mathf.Deg2Rad;
            Vector2 logDir = new Vector2(Mathf.Cos(t), Mathf.Sin(t));   // in (across, along) space
            Vector2 p = new Vector2(x, a);
            along = Vector2.Dot(p, logDir);
            return Vector2.Dot(p, new Vector2(-logDir.y, logDir.x));
        }

        float RoundFade(float a, float x, float half)
        {
            float w = Mathf.Max(halfWidth, 0.5f);
            float r = Mathf.Sqrt(Sq(a / (half * 1.6f)) + Sq(x / w));
            return 1f - Smooth(0.7f, 1f, r);
        }

        /// <summary>cos² bump of half-width w centered at 0.</summary>
        static float Bump(float u, float w) => Mathf.Abs(u) >= w ? 0f : Sq(Mathf.Cos(Mathf.PI * 0.5f * u / w));
        static float Smooth(float a, float b, float v) { float t = Mathf.Clamp01((v - a) / (b - a)); return t * t * (3f - 2f * t); }
        static float Sq(float v) => v * v;

        /// <summary>The log's world direction (for placing its mesh).</summary>
        public Vector3 LogDirection => Quaternion.AngleAxis(-logAngle, Vector3.up) * right;
        public Vector3 FlowDirection => dir;
        public Vector3 AcrossDirection => right;

        void OnDrawGizmos()
        {
            if (!river) Refresh();
            Vector3 c = new Vector3(center.x, splineHeight + 0.2f, center.z);
            Gizmos.color = type switch
            {
                RiverFeatureType.Ledge => new Color(1f, 0.4f, 0.2f),
                RiverFeatureType.ChuteWaveTrain => new Color(0.3f, 0.8f, 1f),
                RiverFeatureType.PourOver => new Color(0.9f, 0.9f, 0.3f),
                RiverFeatureType.LogRamp => new Color(0.7f, 0.5f, 0.3f),
                _ => new Color(0.6f, 1f, 0.6f),
            };
            float front = type == RiverFeatureType.ChuteWaveTrain ? chuteLength : type == RiverFeatureType.Ledge ? length * 0.6f : length * 0.5f;
            float back = type == RiverFeatureType.Ledge ? length * 0.4f : type == RiverFeatureType.ChuteWaveTrain ? length : length * 0.5f;
            Vector3 a0 = c - dir * front, a1 = c + dir * back;
            Gizmos.DrawLine(a0 - right * halfWidth, a1 - right * halfWidth);
            Gizmos.DrawLine(a0 + right * halfWidth, a1 + right * halfWidth);
            Gizmos.DrawLine(a0 - right * halfWidth, a0 + right * halfWidth);
            Gizmos.DrawLine(a1 - right * halfWidth, a1 + right * halfWidth);
            // Height profile along the center line
            Vector3 prev = a0;
            for (int i = 1; i <= 40; i++)
            {
                float a = Mathf.Lerp(-front, back, i / 40f);
                Vector3 p = c + dir * a;
                p.y = splineHeight + 0.2f + HeightOffset(p, river);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
