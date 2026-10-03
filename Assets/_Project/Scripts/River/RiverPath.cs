using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.Splines.Interpolators;

namespace CampanhaRio.River
{
    /// <summary>
    /// The river: a centerline authored with a Unity Spline (its height IS the water surface height), left/right widths
    /// stored along it (SplineData, editable in the Scene view), and the FLOW FIELD. Anything that needs to know
    /// "where is the water going here, and how high is it?" calls Sample() / GetCurrent() / GetWaterHeight().
    ///
    /// Flow field rules (all Inspector values below), in order:
    ///   1. Slope:    base speed from the local (smoothed) downhill gradient of the spline.
    ///   2. Width:    narrow = faster, wide pools = slower (continuity), clamped.
    ///   3. Bends:    the fastest line (thalweg) shifts toward the outside of bends; the inside is slow.
    ///   4. Banks:    slower near the banks (cross-channel profile around the thalweg).
    ///   5-6. Modifiers: FlowObstacle eddies, CrossCurrent inflows, RiverFeature chutes/ledges (IRiverModifier).
    ///   7. Boils:    subtle moving upwellings in strong water (time-varying: GetCurrent only, not baked).
    ///   8. CurrentZone multipliers, applied last (artist override).
    /// Keep the transform unscaled; it no longer sets the water height (the spline does).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer))]
    public class RiverPath : MonoBehaviour
    {
        public struct RiverSample
        {
            public Vector3 point;          // closest point on the centerline, at the water surface (spline height)
            public Vector3 direction;      // downstream, flat, normalized
            public Vector3 right;          // toward the right bank
            public float distanceAlong;    // meters from the river start
            public float lateralOffset;    // meters from the centerline (+ = right)
            public float leftWidth;
            public float rightWidth;
            public float edgeDistance;     // meters past the bank edge (negative = in the water)
            public float slope;            // downhill gradient here (m per m, smoothed)
            public float curvature;        // 1/m, + = bending right (smoothed)
            public float thalweg;          // lateral offset of the fastest line (m)
            public float zoneMultiplier;
            public CurrentZone zone;       // strongest zone here, or null
            public Vector3 waterVelocity;  // static flow field (no boils)
        }

        [Header("Shape")]
        [Tooltip("Half-width to the left bank (meters), stored along the spline by distance.")]
        public SplineData<float> leftWidth = new SplineData<float>();
        [Tooltip("Half-width to the right bank (meters), stored along the spline by distance.")]
        public SplineData<float> rightWidth = new SplineData<float>();
        [Tooltip("Width used when a side has no width points.")]
        [Min(1f)] public float defaultWidth = 12f;
        [Tooltip("Spacing of the internal lookup table (meters). Smaller = more precise, slower rebuilds.")]
        [Min(0.25f)] public float sampleSpacing = 1f;

        [Header("Flow 1: slope")]
        [Tooltip("Current speed on flat water, before width and banks (m/s).")]
        public float baseSpeed = 1.1f;
        [Tooltip("Extra speed per unit of downhill slope (m/s per m/m). 250 = +2.5 m/s at a 1% slope.")]
        public float slopeGain = 250f;
        [Tooltip("Speed limits on the main line (m/s).")]
        public float minSpeed = 0.35f, maxSpeed = 6.5f;
        [Tooltip("Slope is averaged over this length (m), so ledges (features) don't spike the base current.")]
        public float slopeSmoothing = 30f;
        [Tooltip("Final limit on the current after every feature, zone and boil (m/s): feature speed-ups never stack past it.")]
        public float maxCurrentFinal = 6.8f;

        [Header("Flow 2: width (continuity)")]
        [Tooltip("Half-width at which width has no effect (m). Narrower = faster, wider = slower.")]
        public float referenceHalfWidth = 7f;
        [Tooltip("Width factor limits: (slowest in wide pools, fastest in narrows).")]
        public Vector2 widthFactorRange = new Vector2(0.4f, 1.6f);

        [Header("Flow 3: bends")]
        [Tooltip("How far the fast line moves to the outside of a bend: share of the half-width per 1/m of curvature.")]
        public float bendShift = 24f;
        [Tooltip("Maximum thalweg shift (share of the half-width).")]
        [Range(0f, 0.9f)] public float maxBendShift = 0.6f;
        [Tooltip("How much slower the inside of a bend is at the full shift (0..1).")]
        [Range(0f, 1f)] public float insideSlow = 0.55f;
        [Tooltip("Curvature is averaged over this length (m).")]
        public float curvatureSmoothing = 24f;

        [Header("Flow 4: banks")]
        [Tooltip("Current across the channel. X: 0 = fast line, 1 = bank. Y: speed multiplier.")]
        public AnimationCurve crossChannelProfile = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0.3f));

        [Header("Flow 7: boils (runtime only)")]
        [Tooltip("Sideways nudge from upwellings in strong water (m/s).")]
        public float boilStrength = 0.35f;
        [Tooltip("Boils start above this current speed (m/s).")]
        public float boilMinSpeed = 3.5f;
        [Tooltip("Size of a boil (m) and how fast the pattern moves.")]
        public float boilScale = 7f;
        public float boilDrift = 0.25f;

        [Header("Debug")]
        public bool drawCurrentGizmos = true;
        [Min(2f)] public float gizmoSpacing = 10f;

        SplineContainer container;

        // Dense lookup table rebuilt from the spline whenever it changes. Positions are flat (y = 0).
        readonly List<Vector3> samplePos = new List<Vector3>();
        readonly List<Vector3> sampleDir = new List<Vector3>();
        readonly List<float> sampleDist = new List<float>();
        readonly List<float> sampleT = new List<float>();
        readonly List<float> sampleLeft = new List<float>();
        readonly List<float> sampleRight = new List<float>();
        readonly List<float> sampleHeight = new List<float>();
        readonly List<float> sampleSlope = new List<float>();
        readonly List<float> sampleCurve = new List<float>();
        int builtVersion = -1;

        /// <summary>Increments whenever the river shape or widths change (listeners rebuild on change).</summary>
        public int Version { get; private set; }

        public SplineContainer Container => container ? container : container = GetComponent<SplineContainer>();
        /// <summary>Height of the river transform. The actual surface follows the spline: use GetWaterHeight.</summary>
        public float WaterLevel => transform.position.y;

        public float Length
        {
            get { EnsureSamples(); return sampleDist.Count > 0 ? sampleDist[sampleDist.Count - 1] : 0f; }
        }

        public int KnotCount => Container && Container.Spline != null ? Container.Spline.Count : 0;

        void OnEnable() { Spline.Changed += OnSplineChanged; if (!All.Contains(this)) All.Add(this); }
        // ------------------------------------------------------------------ several rivers in one scene (Stage 12)

        /// <summary>Every river in the loaded scenes (a track complex has several: one per track).</summary>
        public static readonly List<RiverPath> All = new List<RiverPath>();

        [Header("Several rivers (Stage 12)")]
        [Tooltip("Where rivers overlap (the shared start pool and finish lake), the higher priority owns the water: the home river.")]
        public int priority;

        /// <summary>
        /// The water body at a point: a river whose water contains it (the higher priority first, then the deeper inside),
        /// else the nearest one. With one river it is that river.
        /// </summary>
        public static RiverPath Resolve(Vector3 p)
        {
            if (All.Count == 0) foreach (var r in FindObjectsByType<RiverPath>()) All.Add(r); // (asked before any river's OnEnable)
            return ResolveIn(p);
        }

        /// <summary>The home river: the one that owns the shared pools (the highest priority).</summary>
        public static RiverPath Home
        {
            get
            {
                if (All.Count == 0) foreach (var r in FindObjectsByType<RiverPath>()) All.Add(r);
                RiverPath best = null;
                foreach (var r in All) if (r && (!best || r.priority > best.priority)) best = r;
                return best ? best : FindAnyObjectByType<RiverPath>();
            }
        }

        static RiverPath ResolveIn(Vector3 p)
        {
            RiverPath best = null;
            float bestEdge = float.MaxValue; int bestPriority = int.MinValue; bool bestInside = false;
            foreach (var r in All)
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                float e = r.WaterEdge(p);
                bool inside = e < 0.5f;
                bool better = best == null
                    || (inside && !bestInside)
                    || (inside == bestInside && inside && (r.priority > bestPriority || (r.priority == bestPriority && e < bestEdge)))
                    || (inside == bestInside && !inside && e < bestEdge);
                if (better) { best = r; bestEdge = e; bestPriority = r.priority; bestInside = inside; }
            }
            return best ? best : FindAnyObjectByType<RiverPath>();
        }

        /// <summary>Is this point on this river's water (or within margin m of it)?</summary>
        public bool Contains(Vector3 p, float margin = 0.5f) => WaterEdge(p) < margin;

        /// <summary>How far outside this river's water a point is (negative = on it). Past either end of the spline counts as
        /// outside, by how far past it is: a channel's mouth in a lake hands the lake over to the lake's own river.</summary>
        public float WaterEdge(Vector3 p)
        {
            int hint = -1;
            var s = SampleShape(p, ref hint);
            float e = s.edgeDistance;
            if (s.distanceAlong <= 0.01f || s.distanceAlong >= Length - 0.01f)
            {
                Vector3 off = p - s.point; off.y = 0f;
                float past = Vector3.Dot(off, s.direction) * (s.distanceAlong <= 0.01f ? -1f : 1f);
                if (past > 0f) e = Mathf.Max(e, past);
            }
            return e;
        }

        void OnDisable() { Spline.Changed -= OnSplineChanged; All.Remove(this); }
        void OnValidate() => MarkChanged();

        void Update()
        {
            if (!transform.hasChanged) return;
            transform.hasChanged = false;
            MarkChanged();
        }

        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (Container && spline == Container.Spline) MarkChanged();
        }

        public void MarkChanged() => Version++;

        // ------------------------------------------------------------------ heights

        /// <summary>Water surface height at a position: the spline height at the nearest centerline point plus river features.</summary>
        public float GetWaterHeight(Vector3 worldPos)
        {
            int hint = -1;
            return GetWaterHeight(worldPos, ref hint);
        }

        public float GetWaterHeight(Vector3 worldPos, ref int hint)
        {
            var s = SampleShape(worldPos, ref hint);
            return s.point.y + ModifierHeight(worldPos);
        }

        /// <summary>Height of the smooth spline surface at a distance along the river (no features).</summary>
        public float SurfaceHeightAt(float distance)
        {
            EnsureSamples();
            if (sampleDist.Count < 2) return WaterLevel;
            int i = Segment(distance, out float t);
            return Mathf.Lerp(sampleHeight[i], sampleHeight[i + 1], t);
        }

        float ModifierHeight(Vector3 p)
        {
            float h = 0f;
            var all = RiverModifiers.All;
            float time = Application.isPlaying ? Time.timeSinceLevelLoad : 0f; // = the shader's _Time.y
            for (int i = 0; i < all.Count; i++)
            {
                if (!RiverModifiers.Touches(all[i], p)) continue;
                h += all[i].HeightOffset(p, this);
                if (time > 0f && all[i] is RiverFeature feature) h += feature.DynamicHeight(p, time);
            }
            return h;
        }

        public float GetKnotDistance(int knotIndex)
        {
            EnsureSamples();
            if (sampleT.Count < 2) return 0f;
            float t = Container.Spline.CurveToSplineT(Mathf.Clamp(knotIndex, 0, KnotCount - 1));
            int i = sampleT.BinarySearch(t);
            if (i >= 0) return sampleDist[i];
            i = Mathf.Clamp(~i - 1, 0, sampleT.Count - 2);
            return Mathf.Lerp(sampleDist[i], sampleDist[i + 1], Mathf.InverseLerp(sampleT[i], sampleT[i + 1], t));
        }

        /// <summary>River info at a distance along the centerline (lateral offset 0).</summary>
        public RiverSample GetPointAtDistance(float distance)
        {
            EnsureSamples();
            if (samplePos.Count < 2) return new RiverSample { zoneMultiplier = 1f };
            int i = Segment(distance, out float t);
            int hint = i;
            return Sample(Vector3.Lerp(samplePos[i], samplePos[i + 1], t), ref hint);
        }

        int Segment(float distance, out float t)
        {
            distance = Mathf.Clamp(distance, 0f, sampleDist[sampleDist.Count - 1]);
            int i = sampleDist.BinarySearch(distance);
            if (i < 0) i = ~i - 1;
            i = Mathf.Clamp(i, 0, samplePos.Count - 2);
            t = Mathf.InverseLerp(sampleDist[i], sampleDist[i + 1], distance);
            return i;
        }

        // ------------------------------------------------------------------ sampling

        public RiverSample Sample(Vector3 worldPos)
        {
            int hint = -1;
            return Sample(worldPos, ref hint);
        }

        /// <summary>Geometry + static flow field at a position.</summary>
        /// <param name="hint">Last sample index for this caller; makes repeated queries cheap. Use -1 if unknown.</param>
        public RiverSample Sample(Vector3 worldPos, ref int hint)
        {
            var s = SampleShape(worldPos, ref hint);
            s.waterVelocity = FlowAt(worldPos, ref s, true);
            return s;
        }

        /// <summary>Geometry only (no flow): cheap, for terrain, dressing and heights.</summary>
        public RiverSample SampleShape(Vector3 worldPos, ref int hint)
        {
            EnsureSamples();
            var result = new RiverSample { zoneMultiplier = 1f };
            int count = samplePos.Count;
            if (count < 2) return result;

            Vector3 pos = new Vector3(worldPos.x, 0f, worldPos.z);
            int nearest = FindNearestSample(pos, hint);
            hint = nearest;

            // Project onto whichever neighbouring segment is closer
            int seg = 0;
            float segT = 0f, rawT = 0f, bestSqr = float.MaxValue;
            for (int i = nearest - 1; i <= nearest; i++)
            {
                if (i < 0 || i > count - 2) continue;
                Vector3 d = samplePos[i + 1] - samplePos[i];
                float t = Vector3.Dot(pos - samplePos[i], d) / Mathf.Max(d.sqrMagnitude, 1e-6f);
                float tc = Mathf.Clamp01(t);
                float sqr = (pos - (samplePos[i] + d * tc)).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; seg = i; segT = tc; rawT = t; }
            }

            Vector3 segVec = samplePos[seg + 1] - samplePos[seg];
            Vector3 flatPoint = samplePos[seg] + segVec * segT;
            result.point = new Vector3(flatPoint.x, Mathf.Lerp(sampleHeight[seg], sampleHeight[seg + 1], segT), flatPoint.z);
            result.direction = Vector3.Lerp(sampleDir[seg], sampleDir[seg + 1], segT).normalized;
            result.right = new Vector3(result.direction.z, 0f, -result.direction.x);
            result.distanceAlong = Mathf.Lerp(sampleDist[seg], sampleDist[seg + 1], segT);
            result.leftWidth = Mathf.Lerp(sampleLeft[seg], sampleLeft[seg + 1], segT);
            result.rightWidth = Mathf.Lerp(sampleRight[seg], sampleRight[seg + 1], segT);
            result.slope = Mathf.Lerp(sampleSlope[seg], sampleSlope[seg + 1], segT);
            result.curvature = Mathf.Lerp(sampleCurve[seg], sampleCurve[seg + 1], segT);
            result.lateralOffset = Vector3.Dot(pos - flatPoint, result.right);

            float sideWidth = result.lateralOffset >= 0f ? result.rightWidth : result.leftWidth;
            result.edgeDistance = Mathf.Abs(result.lateralOffset) - sideWidth;
            if (seg == 0 && rawT < 0f)
                result.edgeDistance = Mathf.Max(result.edgeDistance, -rawT * segVec.magnitude);
            if (seg == count - 2 && rawT > 1f)
                result.edgeDistance = Mathf.Max(result.edgeDistance, (rawT - 1f) * segVec.magnitude);
            return result;
        }

        /// <summary>The flow field rules 1-6 and 8 (see the class summary).</summary>
        Vector3 FlowAt(Vector3 worldPos, ref RiverSample s, bool withModifiers)
        {
            if (s.edgeDistance > 0f) { s.zoneMultiplier = CurrentZone.Evaluate(worldPos, out s.zone); return Vector3.zero; }

            // 1. slope, 2. width. The "main channel" is the narrower side, so a side pocket (backwater) counts as edge water.
            float channelHalf = Mathf.Max(1f, Mathf.Min(s.leftWidth, s.rightWidth));
            float widthFactor = Mathf.Clamp(referenceHalfWidth / channelHalf, widthFactorRange.x, widthFactorRange.y);
            float lineSpeed = Mathf.Clamp((baseSpeed + slopeGain * Mathf.Max(0f, s.slope)) * widthFactor, minSpeed, maxSpeed);

            // 3. bends: the fast line moves to the outside (bending right = outside is left)
            float shift = Mathf.Clamp(-s.curvature * bendShift, -maxBendShift, maxBendShift);
            s.thalweg = shift * channelHalf;

            // 4. banks: profile around the fast line, extra slow on the inside of the bend
            float u = s.lateralOffset, T = s.thalweg;
            float across = u >= T ? (u - T) / Mathf.Max(channelHalf - T, 0.5f) : (T - u) / Mathf.Max(channelHalf + T, 0.5f);
            across = Mathf.Clamp01(across);
            float speed = lineSpeed * crossChannelProfile.Evaluate(across);
            bool insideSide = Mathf.Abs(shift) > 1e-3f && Mathf.Sign(u - T) != Mathf.Sign(shift);
            if (insideSide) speed *= 1f - insideSlow * (Mathf.Abs(shift) / Mathf.Max(maxBendShift, 1e-3f)) * across;

            Vector3 velocity = s.direction * speed;

            // 5-6. obstacles (eddies), cross currents and features
            if (withModifiers)
            {
                var all = RiverModifiers.All;
                for (int i = 0; i < all.Count; i++)
                    if (RiverModifiers.Touches(all[i], worldPos)) all[i].ModifyFlow(worldPos, s, ref velocity);
            }

            // 8. artist overrides
            s.zoneMultiplier = CurrentZone.Evaluate(worldPos, out s.zone);
            return Vector3.ClampMagnitude(velocity * s.zoneMultiplier, maxCurrentFinal);
        }

        /// <summary>Base flow without modifiers (what an obstacle "sees" coming at it).</summary>
        public Vector3 BaseFlow(Vector3 worldPos)
        {
            int hint = -1;
            var s = SampleShape(worldPos, ref hint);
            return FlowAt(worldPos, ref s, false);
        }

        /// <summary>Full current at a position including boils (time-varying). For the kayak and anything that floats.</summary>
        public Vector3 GetCurrent(Vector3 worldPos, ref int hint)
        {
            var s = Sample(worldPos, ref hint);
            return Vector3.ClampMagnitude(s.waterVelocity + GetBoil(worldPos, s.waterVelocity.magnitude, Application.isPlaying ? Time.time : 0f), maxCurrentFinal);
        }

        /// <summary>7. boils: a slowly moving noise field that nudges sideways in strong water.</summary>
        public Vector3 GetBoil(Vector3 worldPos, float speed, float time)
        {
            if (boilStrength <= 0f || speed < boilMinSpeed) return Vector3.zero;
            float x = worldPos.x / boilScale, z = worldPos.z / boilScale, t = time * boilDrift;
            const float e = 0.35f;
            float c = Mathf.PerlinNoise(x + t, z - t * 0.7f);
            float gx = Mathf.PerlinNoise(x + e + t, z - t * 0.7f) - c;
            float gz = Mathf.PerlinNoise(x + t, z + e - t * 0.7f) - c;
            float strength = boilStrength * Mathf.Clamp01((speed - boilMinSpeed) / 2f) / e;
            return new Vector3(gx, 0f, gz) * strength;
        }

        /// <summary>The modifier dominating at a position (for debug/logs), or null.</summary>
        public IRiverModifier FeatureAt(Vector3 worldPos) => FeatureAt(worldPos, out _);

        public IRiverModifier FeatureAt(Vector3 worldPos, out float weight)
        {
            IRiverModifier best = null;
            float bestWeight = 0.05f;
            weight = 0f;
            foreach (var m in RiverModifiers.All)
            {
                if (!RiverModifiers.Touches(m, worldPos)) continue;
                float w = m.Weight(worldPos);
                if (w > bestWeight) { bestWeight = w; best = m; weight = w; }
            }
            return best;
        }

        // ------------------------------------------------------------------ lookup table

        int FindNearestSample(Vector3 pos, int hint)
        {
            int count = samplePos.Count;
            const int window = 40;
            if (hint >= 0 && hint < count)
            {
                int from = Mathf.Max(0, hint - window), to = Mathf.Min(count - 1, hint + window);
                int best = NearestInRange(pos, from, to);
                bool atWindowEdge = (best == from && from > 0) || (best == to && to < count - 1);
                if (!atWindowEdge) return best;
            }
            return NearestInRange(pos, 0, count - 1);
        }

        int NearestInRange(Vector3 pos, int from, int to)
        {
            int best = from;
            float bestSqr = float.MaxValue;
            for (int i = from; i <= to; i++)
            {
                float sqr = (samplePos[i] - pos).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = i; }
            }
            return best;
        }

        void EnsureSamples()
        {
            if (builtVersion == Version && samplePos.Count > 1) return;
            builtVersion = Version;
            samplePos.Clear(); sampleDir.Clear(); sampleDist.Clear(); sampleT.Clear();
            sampleLeft.Clear(); sampleRight.Clear(); sampleHeight.Clear(); sampleSlope.Clear(); sampleCurve.Clear();

            var spline = Container ? Container.Spline : null;
            if (spline == null || spline.Count < 2) return;

            float length = Container.CalculateLength();
            int steps = Mathf.Max(2, Mathf.CeilToInt(length / sampleSpacing));
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float3 p = Container.EvaluatePosition(t);
                var pos = new Vector3(p.x, 0f, p.z);
                float dist = samplePos.Count == 0 ? 0f : sampleDist[sampleDist.Count - 1] + Vector3.Distance(samplePos[samplePos.Count - 1], pos);
                samplePos.Add(pos);
                sampleHeight.Add(p.y);
                sampleDist.Add(dist);
                sampleT.Add(t);
                sampleLeft.Add(EvaluateWidth(leftWidth, spline, t));
                sampleRight.Add(EvaluateWidth(rightWidth, spline, t));
            }

            int n = samplePos.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = samplePos[Mathf.Max(i - 1, 0)];
                Vector3 b = samplePos[Mathf.Min(i + 1, n - 1)];
                sampleDir.Add((b - a).normalized);
            }

            // Smoothed slope (downhill = positive) and signed curvature (+ = bending right)
            float spacing = Mathf.Max(sampleDist[n - 1] / (n - 1), 0.01f);
            int slopeHalf = Mathf.Max(1, Mathf.RoundToInt(slopeSmoothing * 0.5f / spacing));
            int curveHalf = Mathf.Max(1, Mathf.RoundToInt(curvatureSmoothing * 0.5f / spacing));
            for (int i = 0; i < n; i++)
            {
                int a = Mathf.Max(0, i - slopeHalf), b = Mathf.Min(n - 1, i + slopeHalf);
                float run = Mathf.Max(sampleDist[b] - sampleDist[a], 0.01f);
                sampleSlope.Add((sampleHeight[a] - sampleHeight[b]) / run);

                a = Mathf.Max(0, i - curveHalf); b = Mathf.Min(n - 1, i + curveHalf);
                run = Mathf.Max(sampleDist[b] - sampleDist[a], 0.01f);
                float turn = Vector3.SignedAngle(sampleDir[a], sampleDir[b], Vector3.up) * Mathf.Deg2Rad;
                sampleCurve.Add(turn / run);
            }
        }

        float EvaluateWidth(SplineData<float> data, Spline spline, float t)
        {
            if (data == null || data.Count == 0) return defaultWidth;
            return Mathf.Max(1f, data.Evaluate(spline, t, PathIndexUnit.Normalized, new SmoothStepFloat()));
        }

        void OnDrawGizmos()
        {
            if (!drawCurrentGizmos) return;
            EnsureSamples();
            if (samplePos.Count < 2) return;

            for (int i = 0; i < samplePos.Count - 1; i++)
            {
                Vector3 up = Vector3.up * sampleHeight[i], upNext = Vector3.up * sampleHeight[i + 1];
                Vector3 r = new Vector3(sampleDir[i].z, 0f, -sampleDir[i].x);
                Vector3 rNext = new Vector3(sampleDir[i + 1].z, 0f, -sampleDir[i + 1].x);
                Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
                Gizmos.DrawLine(samplePos[i] + up, samplePos[i + 1] + upNext);
                Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.8f);
                Gizmos.DrawLine(samplePos[i] - r * sampleLeft[i] + up, samplePos[i + 1] - rNext * sampleLeft[i + 1] + upNext);
                Gizmos.DrawLine(samplePos[i] + r * sampleRight[i] + up, samplePos[i + 1] + rNext * sampleRight[i + 1] + upNext);
            }

            // Current arrows: green = calm, red = fast; the thalweg in yellow
            for (float d = 0f; d < Length; d += gizmoSpacing)
            {
                var c = GetPointAtDistance(d);
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(c.point + c.right * c.thalweg + Vector3.up * 0.3f, 0.25f);
                for (int k = -3; k <= 3; k++)
                {
                    float lateral = k / 3f * 0.9f * (k < 0 ? c.leftWidth : c.rightWidth);
                    Vector3 p = c.point + c.right * lateral;
                    var s = Sample(p);
                    float speed01 = s.waterVelocity.magnitude / Mathf.Max(maxSpeed, 0.01f);
                    DrawArrow(p + Vector3.up * 0.3f, s.waterVelocity * 0.8f, Color.Lerp(Color.green, Color.red, speed01));
                }
            }
        }

        public static void DrawArrow(Vector3 from, Vector3 vector, Color color)
        {
            Gizmos.color = color;
            if (vector.sqrMagnitude < 0.01f) { Gizmos.DrawWireSphere(from, 0.2f); return; }
            Vector3 to = from + vector;
            Vector3 back = -vector.normalized * 0.8f;
            Vector3 side = new Vector3(back.z, 0f, -back.x) * 0.5f;
            Gizmos.DrawLine(from, to);
            Gizmos.DrawLine(to, to + back + side);
            Gizmos.DrawLine(to, to + back - side);
        }
    }
}
