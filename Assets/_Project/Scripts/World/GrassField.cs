using System.Collections.Generic;
using CampanhaRio.River;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampanhaRio.World
{
    /// <summary>
    /// The forest floor's grass (Phase 3): tens of thousands of tufts drawn with GPU instancing, never as GameObjects.
    /// Tufts are generated where they are needed, one cell (48 m) per frame around the camera, the same everywhere (a
    /// fixed seed from the cell), and dropped when far. Which tuft grows where follows the place: the cool blue-green one
    /// at the water, the dark lush one under the woods, the green and dry-tipped ones in the clearings with patches of the
    /// tall yellow grass. Near: the full tuft; past <see cref="lodDistance"/>: the light one; past <see cref="thinDistance"/>
    /// half of them; past <see cref="drawDistance"/>: none (the ground's grass texture carries on).
    /// </summary>
    public class GrassField : MonoBehaviour
    {
        public enum Tuft { Green, DarkLush, TallYellow, DryTipped, CoolWater }

        [System.Serializable]
        public class TuftType
        {
            public Tuft kind;
            public Mesh near, far;
            public Material material;
        }

        public Terrain terrain;
        public RiverPath river;
        public TuftType[] types = new TuftType[0];
        [Tooltip("Tufts per square metre near the river, and in the woods further out.")]
        public float densityNear = 2.6f, densityFar = 1.4f;
        [Tooltip("Grass grows from this far into the water (negative = in the shallows) up to this far from it (m).")]
        public float fromEdge = -0.3f, toEdge = 75f;
        public float maxSteepness = 48f;
        public float drawDistance = 85f, lodDistance = 24f, thinDistance = 45f;
        public float cellSize = 48f;

        class Cell
        {
            public Bounds bounds;
            public readonly List<Matrix4x4>[] matrices = new List<Matrix4x4>[5];
        }

        readonly Dictionary<Vector2Int, Cell> cells = new Dictionary<Vector2Int, Cell>();
        readonly Plane[] planes = new Plane[6];
        RenderParams[] paramsNear, paramsFar;
        int riverHint = -1;

        public int Generated { get; private set; }
        public int DrawnLastFrame { get; private set; }

        void OnEnable()
        {
            if (!terrain) terrain = FindAnyObjectByType<Terrain>();
            if (!river) river = FindAnyObjectByType<RiverPath>();
            paramsNear = new RenderParams[types.Length];
            paramsFar = new RenderParams[types.Length];
            for (int i = 0; i < types.Length; i++)
            {
                paramsNear[i] = new RenderParams(types[i].material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = gameObject.layer };
                paramsFar[i] = paramsNear[i];
            }
        }

        void Update()
        {
            var cam = Camera.main;
            if (!cam || !terrain || !river || types.Length == 0) return;
            Vector3 eye = cam.transform.position;

            // Make the nearest missing cell (one per frame)
            int reach = Mathf.CeilToInt(drawDistance / cellSize);
            var c0 = new Vector2Int(Mathf.FloorToInt(eye.x / cellSize), Mathf.FloorToInt(eye.z / cellSize));
            Vector2Int best = default; float bestD = float.MaxValue; bool found = false;
            for (int x = -reach; x <= reach; x++)
            for (int z = -reach; z <= reach; z++)
            {
                var k = new Vector2Int(c0.x + x, c0.y + z);
                if (cells.ContainsKey(k)) continue;
                float d = new Vector2((k.x + 0.5f) * cellSize - eye.x, (k.y + 0.5f) * cellSize - eye.z).sqrMagnitude;
                if (d > (drawDistance + cellSize) * (drawDistance + cellSize) || d >= bestD) continue;
                bestD = d; best = k; found = true;
            }
            if (found) cells[best] = Build(best);

            // Drop far cells (they come back the same when needed)
            if (cells.Count > (reach * 2 + 3) * (reach * 2 + 3))
            {
                var drop = new List<Vector2Int>();
                foreach (var kv in cells)
                    if (Mathf.Abs(kv.Key.x - c0.x) > reach + 1 || Mathf.Abs(kv.Key.y - c0.y) > reach + 1) drop.Add(kv.Key);
                foreach (var k in drop) cells.Remove(k);
            }

            // Draw what the camera sees
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            int drawn = 0;
            foreach (var cell in cells.Values)
            {
                float d = Mathf.Sqrt(cell.bounds.SqrDistance(eye));
                if (d > drawDistance || !GeometryUtility.TestPlanesAABB(planes, cell.bounds)) continue;
                bool far = d > lodDistance;
                bool thin = d > thinDistance;
                for (int t = 0; t < types.Length; t++)
                {
                    var list = cell.matrices[(int)types[t].kind];
                    if (list == null || list.Count == 0) continue;
                    int count = thin ? list.Count / 2 : list.Count; // (the list is in random order: half is an even thinning)
                    var mesh = far && types[t].far ? types[t].far : types[t].near;
                    for (int start = 0; start < count; start += 1023)
                    {
                        int n = Mathf.Min(1023, count - start);
                        Graphics.RenderMeshInstanced(far ? paramsFar[t] : paramsNear[t], mesh, 0, list, n, start);
                        drawn += n;
                    }
                }
            }
            DrawnLastFrame = drawn;
        }

        Cell Build(Vector2Int key)
        {
            var cell = new Cell();
            for (int i = 0; i < 5; i++) cell.matrices[i] = new List<Matrix4x4>();
            var data = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            var rng = new System.Random(key.x * 73856093 ^ key.y * 19349663);
            float R() => (float)rng.NextDouble();
            float x0 = key.x * cellSize, z0 = key.y * cellSize;
            int tries = Mathf.RoundToInt(cellSize * cellSize * densityNear);
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < tries; i++)
            {
                var p = new Vector3(x0 + R() * cellSize, 0f, z0 + R() * cellSize);
                float nx = (p.x - origin.x) / data.size.x, nz = (p.z - origin.z) / data.size.z;
                if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;
                float edge = river.SampleShape(p, ref riverHint).edgeDistance;
                if (edge < fromEdge || edge > toEdge) continue;
                if (edge > 25f && R() > densityFar / densityNear) continue; // lighter in the deep woods
                if (data.GetSteepness(nx, nz) > maxSteepness) continue;
                p.y = terrain.SampleHeight(p) + origin.y - 0.03f;
                if (edge < 1.5f && p.y < river.GetWaterHeight(p, ref riverHint) - 0.05f) continue; // not under the water

                float woods = Mathf.PerlinNoise(p.x * 0.012f + 3f, p.z * 0.012f + 9f);   // the pines' own density noise
                float patch = Mathf.PerlinNoise(p.x * 0.06f + 41f, p.z * 0.06f + 17f);   // patches of tall grass
                Tuft kind;
                if (edge < 3f) kind = R() < 0.7f ? Tuft.CoolWater : Tuft.Green;
                else if (woods > 0.5f && edge > 6f) kind = R() < 0.75f ? Tuft.DarkLush : Tuft.Green;
                else if (patch > 0.7f) kind = R() < 0.6f ? Tuft.TallYellow : Tuft.DryTipped;
                else kind = R() < 0.6f ? Tuft.Green : R() < 0.75f ? Tuft.DryTipped : R() < 0.5f ? Tuft.DarkLush : Tuft.TallYellow;

                float scale = Mathf.Lerp(0.85f, 1.5f, R()) * (kind == Tuft.DarkLush ? 1.1f : 1f);
                var rot = Quaternion.Euler(R() * 10f - 5f, R() * 360f, R() * 10f - 5f);
                cell.matrices[(int)kind].Add(Matrix4x4.TRS(p, rot, Vector3.one * scale));
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            // Random order already (random points), so drawing half thins evenly
            if (minY > maxY) { minY = 0f; maxY = 1f; }
            cell.bounds = new Bounds(new Vector3(x0 + cellSize / 2f, (minY + maxY) / 2f + 0.5f, z0 + cellSize / 2f), new Vector3(cellSize, maxY - minY + 2f, cellSize));
            foreach (var l in cell.matrices) Generated += l.Count;
            return cell;
        }
    }
}
