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
        [Tooltip("A place without a river (the agency): grass everywhere on the terrain, off the paths.")]
        public bool noRiver;
        [Tooltip("The carpet layer: short dense grass patches that fill the ground (two colours mixed), under the tall tufts.")]
        public bool carpet;
        [Tooltip("Each blade takes the colour of the ground it grows from (the terrain's painted layers), times this (the grass is lit more evenly than the terrain, so it needs to be darker to read the same).")]
        public bool groundTint = true;
        public float tintScale = 0.73f;
        [Tooltip("Ground layers the grass never takes its colour from (the trails' earth: the tufts at their edges stay green).")]
        public string[] tintIgnore = { "Trail" };
        [Tooltip("A clearing (XZ rect: x, z, width, depth; empty = none): outside it, toward the woods, the grass grows taller (and, for the tall tufts, denser).")]
        public Rect clearing;
        public float forestTallness = 1.7f;
        [Tooltip("Inside the clearing, this share of the tall tufts stays (they gather at the woods' edge).")]
        [Range(0f, 1f)] public float clearingShare = 0.35f;
        [Tooltip("Height variation in patches (0 = even): low and high patches of grass.")]
        public float heightPatches = 0.45f;
        [Tooltip("Tall tufts gather along the trails, within this distance (m; 0 = off).")]
        public float trailHug;
        [Tooltip("Over this distance (m) from a trail's edge, and around objects, the grass fades in (lower and thinner): soft edges.")]
        public float edgeFade = 1.8f, objectFade = 1.2f;

        class Cell
        {
            public Bounds bounds;
            public readonly List<Matrix4x4>[] matrices = new List<Matrix4x4>[5];
            public readonly List<Vector4>[] tints = new List<Vector4>[5];
            public readonly List<MaterialPropertyBlock>[] blocks = new List<MaterialPropertyBlock>[5];
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
            if (!river && !noRiver) river = FindAnyObjectByType<RiverPath>();
            paramsNear = new RenderParams[types.Length];
            paramsFar = new RenderParams[types.Length];
            CacheGround();
            for (int i = 0; i < types.Length; i++)
            {
                paramsNear[i] = new RenderParams(types[i].material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true, layer = gameObject.layer };
                paramsFar[i] = paramsNear[i];
            }
        }

        void Update()
        {
            var cam = Camera.main;
            if (!cam || !terrain || types.Length == 0) return;
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
                    // Fades out with distance (the list is in random order: drawing a share of it thins evenly)
                    float keep = thin ? Mathf.Clamp01(1f - (d - thinDistance) / Mathf.Max(1f, drawDistance - thinDistance)) : 1f;
                    int count = Mathf.RoundToInt(list.Count * keep);
                    var mesh = far && types[t].far ? types[t].far : types[t].near;
                    for (int start = 0; start < count; start += 1023)
                    {
                        int n = Mathf.Min(1023, count - start);
                        var rp = far ? paramsFar[t] : paramsNear[t];
                        rp.matProps = Block(cell, (int)types[t].kind, start / 1023);
                        Graphics.RenderMeshInstanced(rp, mesh, 0, list, n, start);
                        drawn += n;
                    }
                }
            }
            DrawnLastFrame = drawn;
        }

        // ---------------------------------------------------------------- the ground's colour under each blade
        float[,,] alphamaps;
        Texture2D[] layerTex;
        Vector2[] layerTile;

        void CacheGround()
        {
            alphamaps = null;
            if (!groundTint || !terrain) return;
            var data = terrain.terrainData;
            var layers = data.terrainLayers;
            layerTex = new Texture2D[layers.Length];
            layerTile = new Vector2[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var t = layers[i] ? layers[i].diffuseTexture : null;
                layerTex[i] = t && t.isReadable ? t : null;
                if (layers[i] && System.Array.Exists(tintIgnore, n => layers[i].name.Contains(n))) layerTex[i] = null;
                layerTile[i] = layers[i] ? layers[i].tileSize : Vector2.one;
            }
            alphamaps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        }

        Vector4 GroundTint(Vector3 p)
        {
            if (alphamaps == null) return Vector4.one;
            var data = terrain.terrainData;
            Vector3 local = p - terrain.transform.position;
            int w = alphamaps.GetLength(1), h = alphamaps.GetLength(0);
            int x = Mathf.Clamp(Mathf.RoundToInt(local.x / data.size.x * (w - 1)), 0, w - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(local.z / data.size.z * (h - 1)), 0, h - 1);
            Color c = Color.black;
            float total = 0f;
            for (int i = 0; i < layerTex.Length; i++)
            {
                float wgt = alphamaps[z, x, i];
                if (wgt <= 0.001f || !layerTex[i]) continue;
                c += layerTex[i].GetPixelBilinear(local.x / layerTile[i].x, local.z / layerTile[i].y).linear * wgt;
                total += wgt;
            }
            if (total <= 0f) return Vector4.one;
            c /= total;
            return new Vector4(c.r * tintScale, c.g * tintScale, c.b * tintScale, 1f);
        }

        MaterialPropertyBlock Block(Cell cell, int kind, int batch)
        {
            var list = cell.blocks[kind];
            while (list.Count <= batch)
            {
                int start = list.Count * 1023;
                var tints = cell.tints[kind];
                var arr = new Vector4[1023];
                for (int i = 0; i < 1023; i++) arr[i] = start + i < tints.Count ? tints[start + i] : Vector4.one;
                var mpb = new MaterialPropertyBlock();
                mpb.SetVectorArray("_GroundTint", arr);
                list.Add(mpb);
            }
            return list[batch];
        }

        Cell Build(Vector2Int key)
        {
            var cell = new Cell();
            for (int i = 0; i < 5; i++) { cell.matrices[i] = new List<Matrix4x4>(); cell.tints[i] = new List<Vector4>(); cell.blocks[i] = new List<MaterialPropertyBlock>(); }
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
                float edge = river ? river.SampleShape(p, ref riverHint).edgeDistance : 30f;
                float path = GroundPaths.DistanceToPath(p);
                if (path < 0.2f || GroundPaths.Cleared(p)) continue; // the trails and the buildings stay clear
                if (edge < fromEdge || edge > toEdge) continue;
                if (edge > 25f && R() > densityFar / densityNear) continue; // lighter in the deep woods
                if (data.GetSteepness(nx, nz) > maxSteepness) continue;
                p.y = terrain.SampleHeight(p) + origin.y - 0.03f;
                if (river && edge < 1.5f && p.y < river.GetWaterHeight(p, ref riverHint) - 0.05f) continue; // not under the water

                // How far into the woods (0 in the clearing, 1 ten metres out), and the patchy height of the field
                float intoWoods = 0f;
                if (clearing.width > 0f)
                {
                    float ox = Mathf.Max(0f, Mathf.Max(clearing.xMin - p.x, p.x - clearing.xMax));
                    float oz = Mathf.Max(0f, Mathf.Max(clearing.yMin - p.z, p.z - clearing.yMax));
                    intoWoods = Mathf.Clamp01(Mathf.Sqrt(ox * ox + oz * oz) / 10f);
                    bool hug = !carpet && trailHug > 0f && path < trailHug;
                    if (!carpet && intoWoods <= 0f && !hug && R() > clearingShare) continue;
                    if (hug) intoWoods = Mathf.Max(intoWoods, 0.2f); // a little taller there
                }
                float tall = Mathf.Lerp(1f, forestTallness, intoWoods) * (1f + (Mathf.PerlinNoise(p.x * 0.09f + 13f, p.z * 0.09f + 5f) - 0.5f) * 2f * heightPatches);
                // Soft edges: the grass gets lower and thinner toward the trails and around objects (rocks, posts, the cabin)
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, edgeFade, path));
                foreach (var hit in Physics.OverlapSphere(p + Vector3.up * 0.3f, objectFade, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit is TerrainCollider || hit.attachedRigidbody) continue;
                    var cp = hit.ClosestPoint(p + Vector3.up * 0.3f);
                    float d = Vector2.Distance(new Vector2(cp.x, cp.z), new Vector2(p.x, p.z));
                    fade = Mathf.Min(fade, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, objectFade, d)));
                }
                if (fade < 0.12f || R() > Mathf.Lerp(0.35f, 1f, fade)) continue; // thinner there too
                tall *= Mathf.Lerp(0.35f, 1f, fade);
                if (carpet)
                {
                    // Clumps: dark ones gather in patches (the reference), lower near the trails, sizes varied
                    float darkPatch = Mathf.PerlinNoise(p.x * 0.11f + 7f, p.z * 0.11f + 3f);
                    var kc = R() < Mathf.Lerp(0.15f, 0.85f, Mathf.SmoothStep(0.35f, 0.65f, darkPatch)) ? Tuft.DarkLush : Tuft.Green;
                    float warmPatch = Mathf.PerlinNoise(p.x * 0.09f + 53f, p.z * 0.09f + 19f); // groups of the yellowish clumps
                    if (R() < Mathf.SmoothStep(0.5f, 0.75f, warmPatch) * 0.8f) kc = Tuft.DryTipped;
                    if (R() > Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0.25f, 0.6f, Mathf.PerlinNoise(p.x * 0.07f + 31f, p.z * 0.07f + 11f)))) continue; // gaps: the ground shows
                    tall *= Mathf.Lerp(0.45f, 1f, Mathf.Clamp01((path - 0.2f) / 2.5f)) * Mathf.Lerp(0.55f, 1.3f, R() * R());
                    cell.matrices[(int)kc].Add(Matrix4x4.TRS(p, Quaternion.Euler(0f, R() * 360f, 0f), new Vector3(Mathf.Lerp(1.1f, 1.45f, R()), Mathf.Lerp(0.6f, 0.95f, R()) * tall, Mathf.Lerp(1.1f, 1.45f, R()))));
                    cell.tints[(int)kc].Add(GroundTint(p));
                    minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
                    continue;
                }
                float woods = Mathf.PerlinNoise(p.x * 0.012f + 3f, p.z * 0.012f + 9f);   // the pines' own density noise
                float patch = Mathf.PerlinNoise(p.x * 0.06f + 41f, p.z * 0.06f + 17f);   // patches of tall grass
                Tuft kind;
                if (path < 1.2f) kind = R() < 0.6f ? Tuft.DryTipped : Tuft.Green; // trodden edges of the trails
                else if (edge < 3f) kind = R() < 0.7f ? Tuft.CoolWater : Tuft.Green;
                else if (woods > 0.5f && edge > 6f) kind = R() < 0.75f ? Tuft.DarkLush : Tuft.Green;
                else if (patch > 0.7f) kind = R() < 0.6f ? Tuft.TallYellow : Tuft.DryTipped;
                else kind = R() < 0.6f ? Tuft.Green : R() < 0.75f ? Tuft.DryTipped : R() < 0.5f ? Tuft.DarkLush : Tuft.TallYellow;

                float scale = Mathf.Lerp(0.85f, 1.5f, R()) * (kind == Tuft.DarkLush ? 1.1f : 1f);
                var rot = Quaternion.Euler(R() * 10f - 5f, R() * 360f, R() * 10f - 5f);
                cell.matrices[(int)kind].Add(Matrix4x4.TRS(p, rot, new Vector3(scale, scale * tall, scale)));
                cell.tints[(int)kind].Add(GroundTint(p));
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
