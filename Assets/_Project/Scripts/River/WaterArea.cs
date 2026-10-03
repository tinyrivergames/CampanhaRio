using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace CampanhaRio.River
{
    /// <summary>
    /// An isolated body of still water (pond, lake not connected to the river): a closed spline on this
    /// object becomes a flat water surface at this object's height, using the river water material.
    /// Current strength is 0 everywhere. The outline should be roughly star-shaped around its center (like a pond).
    /// Water connected to the river should instead widen the river spline.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaterArea : MonoBehaviour
    {
        [Tooltip("Outline sampling step (m).")]
        [Min(0.25f)] public float outlineStep = 1f;
        [Tooltip("How far the surface extends outside the outline, under the banks (m).")]
        public float edgeOverlap = 1.5f;

        Mesh mesh;
        bool dirty = true;

        void OnEnable() { Spline.Changed += OnSplineChanged; dirty = true; }
        void OnDisable() { Spline.Changed -= OnSplineChanged; if (mesh) DestroyImmediate(mesh); mesh = null; }
        void OnValidate() => dirty = true;

        void OnSplineChanged(Spline spline, int knot, SplineModification modification)
        {
            var container = GetComponent<SplineContainer>();
            if (container && spline == container.Spline) dirty = true;
        }

        void Update()
        {
            if (transform.hasChanged) { transform.hasChanged = false; dirty = true; }
            if (dirty) Rebuild();
        }

        [ContextMenu("Rebuild Now")]
        public void Rebuild()
        {
            dirty = false;
            var container = GetComponent<SplineContainer>();
            if (!mesh) mesh = new Mesh { name = "WaterArea (generated)", hideFlags = HideFlags.DontSave };
            mesh.Clear();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            if (!container || container.Spline == null || container.Spline.Count < 3) return;

            // Sample the outline in local space (flat), pushed outward a little
            float length = container.CalculateLength();
            int steps = Mathf.Max(8, Mathf.CeilToInt(length / outlineStep));
            var outline = new List<Vector2>(steps);
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < steps; i++)
            {
                float3 world = container.EvaluatePosition(i / (float)steps);
                Vector3 local = transform.InverseTransformPoint(world);
                outline.Add(new Vector2(local.x, local.z));
                centroid += outline[i];
            }
            centroid /= steps;
            for (int i = 0; i < steps; i++)
                outline[i] += (outline[i] - centroid).normalized * edgeOverlap;
            if (SignedArea(outline) < 0f) outline.Reverse();

            var vertices = new List<Vector3>();
            var uv0 = new List<Vector2>();
            var uv1 = new List<Vector2>();
            var colors = new List<Color>();
            outline.Insert(0, centroid); // fan center
            foreach (var p in outline)
            {
                vertices.Add(new Vector3(p.x, 0f, p.y));
                uv0.Add(new Vector2(0.5f, p.y));   // 0.5 across = no bank fade
                uv1.Add(p);                        // world-scale texture coordinates
                colors.Add(new Color(0f, 0f, 0f, 1f)); // still water, no obstacle foam
            }
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetColors(colors);
            mesh.SetTriangles(Triangulate(outline.Count - 1), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        static float SignedArea(List<Vector2> poly)
        {
            float area = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        /// <summary>
        /// Fan from the center: robust for pond-like (star-shaped) outlines, which is what WaterArea is for.
        /// Vertex 0 is the center; the outline follows. Clockwise seen from above = faces up in Unity.
        /// </summary>
        static List<int> Triangulate(int outlineCount)
        {
            var triangles = new List<int>(outlineCount * 3);
            for (int i = 0; i < outlineCount; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % outlineCount;
                triangles.Add(0); triangles.Add(b); triangles.Add(a);
            }
            return triangles;
        }
    }
}
