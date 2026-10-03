using System.Collections.Generic;
using UnityEngine;

namespace CampanhaRio.River
{
    /// <summary>
    /// GENERATED WATER SURFACE: builds the water mesh from the RiverPath spline and widths, and
    /// rebuilds automatically whenever the spline, the widths, a CurrentZone, a river modifier or (in the editor)
    /// an obstacle changes. The mesh is never saved (it is regenerated on load), so never hand-edit it.
    ///   UV0: U across the river (0 = left bank, 1 = right bank), V = distance along the river in meters.
    ///   UV1: (meters from the centerline, meters along), world-scale coordinates for the water textures.
    ///   UV3: river-feature surface detail (standing-wave pulse amplitude, phase, chop, glassy tongue).
    ///   UV2: flow DIRECTION in the river frame (across, along), unit length: the shader scrolls along it, so eddies
    ///        swirl, cross currents angle the glints and bends show the fast line.
    ///   Vertex color R: current speed (0..1 of the fastest water; the same flow field the kayak feels).
    ///   Vertex color G: foam (obstacle cushion + V tail, eddy-line seams and river-feature foam).
    ///   Vertex color B: obstacle proximity (1 at the rock, 0 at Proximity Radius), for calm-water rings.
    ///   Vertex height: RiverPath.GetWaterHeight (spline height + river features), so what you see is what the kayak rides.
    ///   Rows are dense where the water changes (features, obstacles, fast flow changes) and sparse in calm stretches.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RiverMeshBuilder : MonoBehaviour
    {
        static readonly int FlowMaxSpeedId = Shader.PropertyToID("_FlowMaxSpeed");

        public RiverPath river;
        [Tooltip("Row spacing along the river where the water is busy (features, obstacles, flow changes) (m).")]
        [Min(0.2f)] public float minRowSpacing = 0.45f;
        [Tooltip("Row spacing in calm, uniform stretches (m).")]
        [Min(0.25f)] public float maxRowSpacing = 1.4f;
        [Tooltip("Within this distance of a feature or obstacle the rows are densest (m).")]
        public float detailRadius = 10f;
        [Tooltip("Target distance between vertices across the river (m).")]
        [Min(0.25f)] public float columnSpacing = 1f;
        [Tooltip("How far the water extends under the banks (m), so no gap shows at the shoreline.")]
        public float bankOverlap = 5f;
        [Tooltip("Extra water before the start and after the end of the spline (m).")]
        public float endExtension = 6f;
        [Tooltip("Stage 12, a channel joining a lake: its water fades in over the first fadeIn m and out over the last fadeOut m (0 = off), so where it overlaps the lake's own water there is no seam (the water is transparent: two full layers would show).")]
        public float fadeIn = 0f, fadeOut = 0f;
        [Tooltip("Stage 8 fast line: how glassy the tongue along the thalweg is (0 = off), its half-width (m), and the current range (m/s) where it shows.")]
        [Range(0f, 1f)] public float fastLineGlassy = 0.75f;
        public float fastLineWidth = 1.3f;
        public Vector2 fastLineSpeed = new Vector2(4f, 8f);

        [Header("Obstacle foam (vertex color G / B)")]
        [Tooltip("Colliders on these layers that touch the water get foam.")]
        public LayerMask obstacleLayers;
        [Tooltip("Foam cushion distance on the upstream side (m).")]
        public float cushionWidth = 0.9f;
        [Tooltip("Foam tail length in still water (m).")]
        public float tailBaseLength = 1.5f;
        [Tooltip("Extra tail length per m/s of current (m).")]
        public float tailLengthPerSpeed = 1.1f;
        [Tooltip("How fast the V tail widens with distance (m per m).")]
        public float tailSpread = 0.3f;
        [Tooltip("Distance over which calm-water rings show around obstacles (m).")]
        public float proximityRadius = 3f;

        Mesh mesh;
        int builtRiverVersion = -1;
        int builtZoneVersion = -1;
        int builtObstacleHash;
        static int ZoneVersion => CurrentZone.Version * 31 + RiverModifiers.Version;
        float nextObstacleCheck;
        MaterialPropertyBlock block;

        struct Obstacle { public Collider collider; public Vector3 center; public float radius; public Vector3 flowDir, flowRight; public float speed; }

        void OnEnable()
        {
            if (!river) river = GetComponentInParent<RiverPath>();
            if (obstacleLayers.value == 0) obstacleLayers = LayerMask.GetMask("Obstacle");
            builtRiverVersion = -1;
        }

        void OnDisable()
        {
            if (mesh) DestroyImmediate(mesh);
            mesh = null;
        }

        void Update()
        {
            if (!river) return;
            bool dirty = river.Version != builtRiverVersion || ZoneVersion != builtZoneVersion;
            // In the editor, also rebuild when an obstacle is moved (checked twice a second)
            if (!dirty && !Application.isPlaying && Time.realtimeSinceStartup > nextObstacleCheck)
            {
                nextObstacleCheck = Time.realtimeSinceStartup + 0.5f;
                dirty = ObstacleHash() != builtObstacleHash;
            }
            if (dirty) Rebuild();
        }

        [ContextMenu("Rebuild Now")]
        public void Rebuild()
        {
            if (!river) return;
            builtRiverVersion = river.Version;
            builtZoneVersion = ZoneVersion;
            builtObstacleHash = ObstacleHash();

            if (!mesh)
            {
                mesh = new Mesh { name = "RiverWater (generated)", hideFlags = HideFlags.DontSave };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.Clear();
            float length = river.Length;
            if (length <= 0f) { GetComponent<MeshFilter>().sharedMesh = mesh; return; }

            var obstacles = CollectObstacles();

            // Rows: dense where the water is busy, sparse in calm stretches
            var rowDistances = RowDistances(length, obstacles);
            int rows = rowDistances.Count;
            // Width varies along the river, so columns are a fixed count per mesh sized for the widest row.
            float widest = 0f;
            var samples = new RiverPath.RiverSample[rows];
            for (int r = 0; r < rows; r++)
            {
                samples[r] = river.GetPointAtDistance(rowDistances[r]);
                widest = Mathf.Max(widest, samples[r].leftWidth + samples[r].rightWidth + bankOverlap * 2f);
            }
            int columns = Mathf.Max(2, Mathf.CeilToInt(widest / columnSpacing) + 1);

            int count = rows * columns;
            var vertices = new List<Vector3>(count);
            var uv0 = new List<Vector2>(count);
            var uv1 = new List<Vector2>(count);
            var uv2 = new List<Vector2>(count);
            var uv3 = new List<Vector4>(count);
            var colors = new List<Color>(count);
            var speeds = new List<float>(count);
            float maxSpeed = 0.01f;
            int hint = -1;

            for (int r = 0; r < rows; r++)
            {
                var s = samples[r];
                float along = rowDistances[r];
                Vector3 center = s.point;
                if (r == 0) center -= s.direction * endExtension;
                if (r == rows - 1) center += s.direction * endExtension;
                float left = s.leftWidth + bankOverlap, right = s.rightWidth + bankOverlap;

                for (int c = 0; c < columns; c++)
                {
                    float u = c / (float)(columns - 1);
                    float lateral = Mathf.Lerp(-left, right, u);
                    Vector3 world = center + s.right * lateral;
                    world.y = river.GetWaterHeight(world, ref hint);
                    var flow = river.Sample(world, ref hint);
                    float speed = flow.waterVelocity.magnitude;
                    maxSpeed = Mathf.Max(maxSpeed, speed);
                    ObstacleFoam(world, obstacles, out float foam, out float proximity);
                    foam = Mathf.Max(foam, ModifierFoam(world));
                    // Flow direction in the river frame (across, along) = the UV1 axes the shader scrolls
                    Vector2 dir = speed > 0.02f ? new Vector2(Vector3.Dot(flow.waterVelocity, s.right), Vector3.Dot(flow.waterVelocity, s.direction)) / speed : new Vector2(0f, 1f);

                    vertices.Add(transform.InverseTransformPoint(world));
                    uv0.Add(new Vector2(u, along));
                    uv1.Add(new Vector2(lateral, along));
                    uv2.Add(dir);
                    var detail = SurfaceDetail(world);
                    // The fast line (Stage 8): a glassy, brighter tongue along the thalweg where the water runs fast, so it can be read
                    if (fastLineGlassy > 0f)
                    {
                        float offLine = (flow.lateralOffset - flow.thalweg) / Mathf.Max(fastLineWidth, 0.1f);
                        detail.w = Mathf.Max(detail.w, fastLineGlassy * Mathf.Exp(-offLine * offLine) * Mathf.InverseLerp(fastLineSpeed.x, fastLineSpeed.y, speed));
                    }
                    uv3.Add(detail);
                    speeds.Add(speed);
                    float fade = 1f;
                    if (fadeIn > 0f) fade *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(along / fadeIn));
                    if (fadeOut > 0f) fade *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((river.Length - along) / fadeOut));
                    if (r == 0 && fadeIn > 0f) fade = 0f;
                    if (r == rows - 1 && fadeOut > 0f) fade = 0f;
                    colors.Add(new Color(0f, foam, proximity, fade));
                }
            }
            for (int i = 0; i < count; i++)
            {
                var color = colors[i];
                color.r = speeds[i] / maxSpeed;
                colors[i] = color;
            }

            var triangles = new List<int>((rows - 1) * (columns - 1) * 6);
            for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < columns - 1; c++)
            {
                int a = r * columns + c, b = a + 1, d = a + columns, e = d + 1;
                triangles.Add(a); triangles.Add(d); triangles.Add(e);
                triangles.Add(a); triangles.Add(e); triangles.Add(b);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetUVs(2, uv2);
            mesh.SetUVs(3, uv3);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;

            // The shader turns R back into m/s with this
            block ??= new MaterialPropertyBlock();
            var meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.GetPropertyBlock(block);
            block.SetFloat(FlowMaxSpeedId, maxSpeed);
            meshRenderer.SetPropertyBlock(block);
        }

        /// <summary>Row distances along the river: minRowSpacing near modifiers and obstacles, maxRowSpacing in calm water.</summary>
        List<float> RowDistances(float length, List<Obstacle> obstacles)
        {
            var rows = new List<float> { 0f };
            float d = 0f;
            while (d < length)
            {
                var s = river.GetPointAtDistance(d);
                float detail = 0f;
                foreach (var m in RiverModifiers.All)
                {
                    var b = m.Influence;
                    Vector3 closest = b.ClosestPoint(new Vector3(s.point.x, b.center.y, s.point.z));
                    float gap = Vector2.Distance(new Vector2(closest.x, closest.z), new Vector2(s.point.x, s.point.z));
                    detail = Mathf.Max(detail, 1f - gap / detailRadius);
                }
                foreach (var o in obstacles)
                    detail = Mathf.Max(detail, 1f - (Vector3.Distance(o.center, s.point) - o.radius - s.leftWidth - s.rightWidth) / detailRadius);
                // Fast or quickly changing water (slope, bends) also gets more rows
                detail = Mathf.Max(detail, Mathf.Clamp01(Mathf.Abs(s.curvature) * 20f), Mathf.Clamp01(s.slope * 40f));
                d += Mathf.Lerp(maxRowSpacing, minRowSpacing, Mathf.Clamp01(detail));
                rows.Add(Mathf.Min(d, length));
            }
            return rows;
        }

        /// <summary>UV3: (standing-wave pulse amplitude, pulse phase, chop, glassy) from river features.</summary>
        Vector4 SurfaceDetail(Vector3 p)
        {
            float pulse = 0f, phase = 0f, chop = 0f, glassy = 0f;
            foreach (var m in RiverModifiers.All)
                if (m is IRiverSurfaceDetail detail && RiverModifiers.Touches(m, p)) detail.SurfaceDetail(p, ref pulse, ref phase, ref chop, ref glassy);
            return new Vector4(pulse, phase, chop, glassy);
        }

        float ModifierFoam(Vector3 p)
        {
            float foam = 0f;
            foreach (var m in RiverModifiers.All)
                if (RiverModifiers.Touches(m, p)) foam = Mathf.Max(foam, m.Foam(p, river));
            return foam;
        }

        List<Obstacle> CollectObstacles()
        {
            var list = new List<Obstacle>();
            foreach (var col in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (!col.enabled || col.isTrigger || (obstacleLayers.value & (1 << col.gameObject.layer)) == 0) continue;
                Bounds b = col.bounds;
                float waterHeight = river.GetWaterHeight(b.center);
                if (b.min.y > waterHeight + 0.1f || b.max.y < waterHeight - 0.1f) continue; // not touching the surface
                var s = river.Sample(b.center);
                float radius = Mathf.Max(b.extents.x, b.extents.z);
                if (s.edgeDistance > radius) continue; // on land
                float speed = s.waterVelocity.magnitude;
                list.Add(new Obstacle
                {
                    collider = col, center = new Vector3(b.center.x, waterHeight, b.center.z), radius = radius,
                    flowDir = speed > 0.05f ? s.waterVelocity / speed : s.direction, speed = speed,
                });
            }
            for (int i = 0; i < list.Count; i++)
            {
                var o = list[i];
                o.flowRight = new Vector3(o.flowDir.z, 0f, -o.flowDir.x);
                list[i] = o;
            }
            return list;
        }

        void ObstacleFoam(Vector3 p, List<Obstacle> obstacles, out float foam, out float proximity)
        {
            foam = 0f;
            proximity = 0f;
            foreach (var o in obstacles)
            {
                float tailLength = tailBaseLength + tailLengthPerSpeed * o.speed;
                Vector3 d = p - o.center; d.y = 0f;
                float reach = o.radius + Mathf.Max(tailLength, proximityRadius) + 1f;
                if (d.sqrMagnitude > reach * reach) continue; // cheap reject before the collider query

                Vector3 closest = o.collider.ClosestPoint(p);
                closest.y = p.y;
                float surfaceDistance = Vector3.Distance(p, closest);
                proximity = Mathf.Max(proximity, 1f - surfaceDistance / proximityRadius);

                float along = Vector3.Dot(d, o.flowDir);   // + downstream
                float across = Vector3.Dot(d, o.flowRight);
                float currentFactor = Mathf.Clamp01(o.speed / 3f);

                // Contact ring + cushion on the upstream face
                float contact = (1f - Mathf.Clamp01(surfaceDistance / 0.45f)) * Mathf.Lerp(0.05f, 1f, currentFactor); // still water: rings only
                float cushion = along < 0f ? (1f - Mathf.Clamp01(surfaceDistance / cushionWidth)) * Mathf.Lerp(0.2f, 1f, currentFactor) : 0f;

                // V-shaped tail downstream: two foam lines diverging, plus a softer turbulent center
                float tail = 0f;
                if (along > 0f && along < tailLength)
                {
                    float fade = 1f - along / tailLength;
                    float halfWidth = o.radius * 0.8f + along * tailSpread;
                    float edge = Mathf.Exp(-Sq(Mathf.Abs(across) - halfWidth) / (2f * 0.35f * 0.35f));
                    float middle = 0.55f * Mathf.Exp(-Sq(across) / (2f * Sq(o.radius * 0.6f + 0.2f)));
                    tail = Mathf.Max(edge, middle) * fade * Mathf.Clamp01(o.speed / 2.5f);
                }
                foam = Mathf.Max(foam, Mathf.Max(contact, Mathf.Max(cushion, tail)));
            }
        }

        static float Sq(float x) => x * x;

        int ObstacleHash()
        {
            unchecked
            {
                int hash = 17;
                foreach (var col in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
                {
                    if (col.isTrigger || (obstacleLayers.value & (1 << col.gameObject.layer)) == 0) continue;
                    var p = col.transform.position;
                    hash = hash * 31 + p.GetHashCode();
                    hash = hash * 31 + col.transform.lossyScale.GetHashCode();
                }
                return hash;
            }
        }
    }
}
