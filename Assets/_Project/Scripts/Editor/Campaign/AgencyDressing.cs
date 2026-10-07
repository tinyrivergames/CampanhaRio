using System.Collections.Generic;
using System.IO;
using CampanhaRio.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Phase 3 shown at the agency (where everyone starts, so it can be seen without a van ride): a real ground (a
    /// terrain: the yard flat, gentle hills around), brown earth trails (to the board, the shed, the van, and a loop
    /// through the woods), a full forest around the yard (the three pines and the three leafy trees, bigger than on the
    /// river), rocks along the trails and in the woods, and dense grass (the five tufts) everywhere off the trails.
    /// Runs on the Agencia segment WorldBuilder made (the shed, the boards and the markers stay where they are).
    /// Menu: CampanhaRio > Setup > Dress the Agency. Batch: CampanhaRio.Editor.AgencyDressing.Build
    /// </summary>
    public static class AgencyDressing
    {
        const string ScenePath = "Assets/_Project/Scenes/Segments/Agencia.unity";
        const string TerrainPath = "Assets/_Project/Art/Terrain/Agencia_Terrain.asset";
        const float Ground = 37f;
        static readonly Vector3 Origin = new Vector3(-100f, 30f, -400f);
        static readonly Vector3 Size = new Vector3(200f, 30f, 140f);
        // The yard (flat, no trees): shed, boards, the van's gravel and the road out
        static readonly Rect Yard = new Rect(-34f, -352f, 58f, 64f); // (tighter: the woods close in, as in the reference)

        [MenuItem("CampanhaRio/Setup/Dress the Agency")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var root = GameObject.Find("Agencia").transform;
            foreach (string old in new[] { "Ground", "Gravel", "Trees", "Agency Terrain", "Forest", "Grass", "Grass Carpet", "Paths", "Props" })
            {
                var t = root.Find(old);
                if (t) Object.DestroyImmediate(t.gameObject);
            }

            var paths = new GameObject("Paths").AddComponent<GroundPaths>();
            paths.transform.SetParent(root, false);
            // The reference's trails: from the van a wide trail to a junction, then on into the woods (centre), to the cabin
            // (right) and into the woods on the left
            P(paths, 3.6f, new Vector3(0, 0, -292), new Vector3(1.5f, 0, -306), new Vector3(0, 0, -318));
            P(paths, 3.2f, new Vector3(0, 0, -318), new Vector3(-5, 0, -323), new Vector3(-9, 0, -327.8f));
            P(paths, 3.2f, new Vector3(0, 0, -318), new Vector3(2.5f, 0, -330), new Vector3(-1.5f, 0, -342), new Vector3(1.5f, 0, -356),
              new Vector3(-2, 0, -372), new Vector3(2.5f, 0, -396));
            P(paths, 2.6f, new Vector3(0, 0, -318), new Vector3(9, 0, -321.5f), new Vector3(20, 0, -318), new Vector3(34, 0, -326), new Vector3(52, 0, -321));
            paths.keepClear.Add(new Rect(-22f, -342f, 18f, 18f));  // the cabin
            paths.keepClear.Add(new Rect(-5.6f, -335f, 4.4f, 3f));  // the map board
            paths.keepClear.Add(new Rect(-6f, -300f, 12f, 40f));    // the van's gravel and the road out

            var terrain = BuildTerrain(root, paths);
            AgencySet.Build(root, terrain, paths);
            Forest(root, terrain, paths);

            var grass = new GameObject("Grass").AddComponent<GrassField>();
            grass.transform.SetParent(root, false);
            grass.terrain = terrain;
            grass.noRiver = true;
            grass.densityNear = 3.2f;
            grass.densityFar = 3.2f;
            grass.drawDistance = 80f;
            grass.types = GrassTypes();
            grass.densityNear = grass.densityFar = 0.7f; // the tall ribbons: few in the yard, thick at the woods' edge
            grass.clearing = Yard;
            grass.clearingShare = 0.25f;
            grass.forestTallness = 1.8f;
            grass.trailHug = 0f; // (the reference keeps the grass low at the trails)

            var carpet = new GameObject("Grass Carpet").AddComponent<GrassField>();
            carpet.transform.SetParent(root, false);
            carpet.terrain = terrain;
            carpet.noRiver = true;
            carpet.carpet = true;
            carpet.densityNear = carpet.densityFar = 5.0f; // (fewer: clumps with the ground between, as in the reference) // (each ribbon clump is fuller than the old carpet patch)
            carpet.drawDistance = 55f; carpet.lodDistance = 16f; carpet.thinDistance = 34f;
            carpet.types = CarpetTypes();
            carpet.clearing = Yard;
            carpet.forestTallness = 1.5f;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Campanha] Agency dressed: terrain, trails, forest, rocks, grass.");
        }

        static void P(GroundPaths g, float width, params Vector3[] pts) => g.paths.Add(new GroundPaths.Path { width = width, points = pts });

        static bool InYard(float x, float z, float margin = 0f) =>
            x > Yard.xMin - margin && x < Yard.xMax + margin && z > Yard.yMin - margin && z < Yard.yMax + margin;

        static Terrain BuildTerrain(Transform root, GroundPaths paths)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TerrainPath));
            AssetDatabase.DeleteAsset(TerrainPath);
            var data = new TerrainData { heightmapResolution = 257, alphamapResolution = 512 };
            data.size = Size;
            AssetDatabase.CreateAsset(data, TerrainPath); // first: the splat maps made below are kept as its sub-assets
            int hr = data.heightmapResolution;
            var h = new float[hr, hr];
            for (int y = 0; y < hr; y++)
            for (int x = 0; x < hr; x++)
            {
                float wx = Origin.x + x / (float)(hr - 1) * Size.x, wz = Origin.z + y / (float)(hr - 1) * Size.z;
                // Distance out of the yard, and the band next to the road segment kept flat (it meets the valley road at 37 m)
                float dx = Mathf.Max(0f, Mathf.Max(Yard.xMin - wx, wx - Yard.xMax));
                float dz = Mathf.Max(0f, Mathf.Max(Yard.yMin - wz, wz - Yard.yMax));
                float out_ = Mathf.Sqrt(dx * dx + dz * dz);
                float hills0 = Mathf.SmoothStep(0f, 1f, out_ / 45f) * 5f + (Mathf.PerlinNoise(wx * 0.025f + 5f, wz * 0.025f + 2f) - 0.5f) * 3f * Mathf.SmoothStep(0f, 1f, out_ / 20f);
                float hills = hills0;
                var here = new Vector3(wx, 0f, wz);
                float nearBuilding = 99f;
                foreach (var r in paths.keepClear)
                {
                    float bx = Mathf.Max(0f, Mathf.Max(r.xMin - wx, wx - r.xMax)), bz = Mathf.Max(0f, Mathf.Max(r.yMin - wz, wz - r.yMax));
                    nearBuilding = Mathf.Min(nearBuilding, Mathf.Sqrt(bx * bx + bz * bz));
                }
                float swell = (Mathf.PerlinNoise(wx * 0.06f + 21f, wz * 0.06f + 8f) - 0.5f) * 1.6f + (Mathf.PerlinNoise(wx * 0.17f + 4f, wz * 0.17f + 2f) - 0.5f) * 0.5f;
                hills += swell * Mathf.SmoothStep(0f, 1f, nearBuilding / 6f);
                float edgeFlat = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-262f, -285f, wz)); // 0 at the road segment's edge
                float trail = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 6f, paths.Distance(new Vector3(wx, 0f, wz))));
                h[y, x] = (Ground + hills * edgeFlat * Mathf.Lerp(0.55f, 1f, trail) - Origin.y) / Size.y; // trails sit a little lower
            }
            data.SetHeights(0, 0, h);
            data.terrainLayers = new[]
            {
                AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/_Project/Art/Textures/Ground/TL_Grass.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/_Project/Art/Textures/Ground/TL_Earth.terrainlayer"),
                AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/_Project/Art/Textures/Ground/TL_Gravel.terrainlayer"),
                TrailLayer(),
            };
            int ar = data.alphamapResolution;
            var maps = new float[ar, ar, 4];
            for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float wx = Origin.x + x / (float)(ar - 1) * Size.x, wz = Origin.z + y / (float)(ar - 1) * Size.z;
                var p = new Vector3(wx, 0f, wz);
                float trail = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, 0.9f, paths.Distance(p)));   // the trails (earth with pebbles), soft edges
                float earth = Mathf.Max(0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.8f, Mathf.PerlinNoise(wx * 0.05f + 3f, wz * 0.05f + 1f))) * 0.5f);
                float gravel = wx > -6f && wx < 6f && wz > -294f ? 1f : 0f;                                     // the van's gravel and the road out
                trail *= 1f - gravel;
                earth *= (1f - gravel) * (1f - trail);
                float grass = Mathf.Max(0f, 1f - earth - gravel - trail);
                float sum = grass + earth + gravel + trail;
                maps[y, x, 0] = grass / sum; maps[y, x, 1] = earth / sum; maps[y, x, 2] = gravel / sum; maps[y, x, 3] = trail / sum;
            }
            data.SetAlphamaps(0, 0, maps);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Agency Terrain";
            go.transform.SetParent(root, false);
            go.transform.position = Origin;
            var t = go.GetComponent<Terrain>();
            t.drawInstanced = true;
            return t;
        }

        /// <summary>The trails' ground: brown earth with pebbles mixed in (light and dark stones, a shade under each), tileable.</summary>
        static TerrainLayer TrailLayer()
        {
            const string dir = "Assets/_Project/Art/Textures/Ground";
            string texPath = $"{dir}/T_Ground_Trail.png";
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var earthDark = new Color(0.55f, 0.31f, 0.17f); var earthLight = new Color(0.68f, 0.41f, 0.24f); // the reference's orange-red earth
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                float n = Mathf.PerlinNoise(3f + Mathf.Cos(u) * 2f + Mathf.Sin(v) * 0.7f, 7f + Mathf.Sin(u) * 2f + Mathf.Cos(v) * 0.7f);
                tex.SetPixel(x, y, Color.Lerp(earthDark, earthLight, n));
            }
            var rng = new System.Random(5);
            for (int s = 0; s < 70; s++) // (a few: the reference's earth is smooth, with loose pebbles on top)
            {
                float cx = (float)rng.NextDouble() * size, cy = (float)rng.NextDouble() * size;
                float rx = 2.5f + (float)rng.NextDouble() * (s < 10 ? 8f : 4f), ry = rx * (0.6f + (float)rng.NextDouble() * 0.4f);
                float ang = (float)rng.NextDouble() * Mathf.PI;
                float g = 0.42f + (float)rng.NextDouble() * 0.28f;
                var stone = new Color(g, g * 0.97f, g * 0.92f);
                for (int dy = -(int)rx - 3; dy <= (int)rx + 3; dy++)
                for (int dx = -(int)rx - 3; dx <= (int)rx + 3; dx++)
                {
                    float lx = dx * Mathf.Cos(ang) + dy * Mathf.Sin(ang), ly = -dx * Mathf.Sin(ang) + dy * Mathf.Cos(ang);
                    float e = (lx / rx) * (lx / rx) + (ly / ry) * (ly / ry);
                    int px = ((int)cx + dx + size) % size, py = ((int)cy + dy + size) % size;
                    if (e <= 1f) tex.SetPixel(px, py, stone * Mathf.Lerp(1.1f, 0.85f, Mathf.Clamp01((ly / ry + 1f) * 0.5f)));
                    else if (e <= 1.5f && dy < 0) tex.SetPixel(px, py, tex.GetPixel(px, py) * 0.8f); // a little shade
                }
            }
            var pixels = tex.GetPixels();
            for (int pi = 0; pi < pixels.Length; pi++) pixels[pi].a = 0f; // URP's terrain reads the alpha as smoothness: 0 = matte
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.alphaIsTransparency = false; // (the alpha is the terrain's smoothness, not a cut-out: never bleed the colours)
            imp.anisoLevel = 4;
            imp.SaveAndReimport();
            string layerPath = $"{dir}/TL_Trail.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layerPath); }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            layer.tileSize = new Vector2(3.5f, 3.5f);
            layer.smoothness = 0f; // matte (a default smoothness made the trails shine like water)
            layer.metallic = 0f;
            layer.specular = Color.black;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static void Forest(Transform root, Terrain terrain, GroundPaths paths)
        {
            var forest = new GameObject("Forest").transform;
            forest.SetParent(root, false);
            string[] pines = { "Pine/PinheiroA", "Pine/PinheiroB", "Pine/PinheiroC" };
            string[] leafy = { "Broadleaf/ArvoreFolhosaA", "Broadleaf/ArvoreFolhosaB", "Broadleaf/ArvoreFolhosaC" };
            string[] rocks = { "Rock/PedraCinzaA", "Rock/PedraCinzaB", "Rock/PedraCinzaB", "Rock/PedraCinzaA" }; // the reference picture's grey rocks
            GameObject L(string p) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Art/Models/{p}.fbx");
            var rng = new System.Random(77);
            float R() => (float)rng.NextDouble();
            var placed = new List<Vector3>();
            bool Free(Vector3 q, float d) { foreach (var o in placed) if ((o - q).sqrMagnitude < d * d) return false; return true; }
            int trees = 0, stones = 0;
            for (int i = 0; i < 60000 && trees < 1500; i++)
            {
                var p = new Vector3(Origin.x + R() * Size.x, 0f, Origin.z + R() * Size.z);
                if (InYard(p.x, p.z, 4f) || p.z > -268f) continue;                     // the yard, and the road segment's edge
                float edgeDist = Mathf.Sqrt(Mathf.Pow(Mathf.Max(0f, Mathf.Max(Yard.xMin - p.x, p.x - Yard.xMax)), 2) + Mathf.Pow(Mathf.Max(0f, Mathf.Max(Yard.yMin - p.z, p.z - Yard.yMax)), 2));
                bool leaf = R() < (edgeDist < 12f ? 0.55f : 0.25f); // leafy trees in front, a wall of pines behind
                float size = leaf ? Mathf.Lerp(1.4f, 2.1f, R()) : Mathf.Lerp(2.0f, 3.1f, R());
                // The trails stay open: a pine's low branches reach about 2 m per unit of scale, a leafy tree's crown is overhead
                if (paths.Distance(p) < (leaf ? 1.2f + 0.8f * size : 0.8f + 2f * size)) continue;
                if (!Free(p, 3.4f)) continue;
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y - 0.15f;
                var prefab = L(leaf ? leafy[rng.Next(3)] : pines[rng.Next(3)]);
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(prefab, forest);
                tree.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, R() * 360f, 0f));
                tree.transform.localScale = Vector3.one * size;
                placed.Add(p);
                trees++;
            }
            for (int i = 0; i < 20000 && stones < 140; i++)
            {
                var p = new Vector3(Origin.x + R() * Size.x, 0f, Origin.z + R() * Size.z);
                float d = paths.Distance(p);
                bool trailSide = d > 0.6f && d < 3f;
                bool woods = !InYard(p.x, p.z, 2f) && R() < 0.15f;
                if ((!trailSide && !woods) || InYard(p.x, p.z) && !trailSide || GroundPaths.Cleared(p)) continue;
                if (!Free(p, 1.8f)) continue;
                int kind = trailSide ? (R() < 0.6f ? 1 : 2) : rng.Next(4);
                float scale = trailSide ? Mathf.Lerp(0.35f, 0.75f, R()) : Mathf.Lerp(0.6f, 1.3f, R());
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y - 0.15f * scale;
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(L(rocks[kind]), forest);
                rock.transform.SetPositionAndRotation(p, Quaternion.Euler(R() * 8f - 4f, R() * 360f, R() * 8f - 4f));
                rock.transform.localScale = Vector3.one * scale;
                placed.Add(p);
                stones++;
            }
            int boulders = 0;
            for (int i = 0; i < 20000 && boulders < 45; i++) // big mossy boulders in the woods and at its edge
            {
                var p = new Vector3(Origin.x + R() * Size.x, 0f, Origin.z + R() * Size.z);
                if (InYard(p.x, p.z, 1f) || p.z > -268f || paths.Distance(p) < 3.5f || !Free(p, 4.5f)) continue;
                float scale = Mathf.Lerp(2.2f, 4f, R());
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y - 0.25f * scale;
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(L(rocks[R() < 0.5f ? 0 : 3]), forest);
                rock.transform.SetPositionAndRotation(p, Quaternion.Euler(R() * 10f - 5f, R() * 360f, R() * 10f - 5f));
                rock.transform.localScale = Vector3.one * scale;
                placed.Add(p);
                boulders++;
            }
            // Round bushes: along the trails and in clumps at the woods' edge
            int bushes = 0;
            var bush = L("Broadleaf/ArbustoA");
            for (int i = 0; i < 20000 && bushes < 260; i++)
            {
                var p = new Vector3(Origin.x + R() * Size.x, 0f, Origin.z + R() * Size.z);
                float d = paths.Distance(p);
                bool trailSide = d > 1.2f && d < 4f && R() < 0.5f && p.z < -345f; // (none along the yard's trails: the reference keeps them in the woods)
                float ox = Mathf.Max(0f, Mathf.Max(Yard.xMin - p.x, p.x - Yard.xMax)), oz = Mathf.Max(0f, Mathf.Max(Yard.yMin - p.z, p.z - Yard.yMax));
                bool edge = !InYard(p.x, p.z) && Mathf.Sqrt(ox * ox + oz * oz) < 14f;
                if ((!trailSide && !edge) || GroundPaths.Cleared(p) || p.z > -268f || d < 1.2f || !Free(p, 1.4f)) continue;
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y - 0.1f;
                var b = (GameObject)PrefabUtility.InstantiatePrefab(bush, forest);
                b.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, R() * 360f, 0f));
                b.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 2.2f, R());
                placed.Add(p);
                bushes++;
            }
            Debug.Log($"[Campanha] Agency forest: {trees} trees, {stones} rocks, {boulders} big boulders, {bushes} bushes");
        }

        /// <summary>The reference's wooden props (graybox wood): a rail fence along the trails near the yard, a trail sign at
        /// the woods' entrance, and lantern posts (lit, warm) along the way to the van and the woods.</summary>
        static void Props(Transform root, Terrain terrain, GroundPaths paths)
        {
            var props = new GameObject("Props").transform;
            props.SetParent(root, false);
            var wood = GrayboxMaterials.Get("WoodWarm", new Color(0.43f, 0.27f, 0.16f));
            var woodLight = GrayboxMaterials.Get("WoodLight", new Color(0.6f, 0.42f, 0.27f));
            float G(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m, float yaw = 0f)
            {
                var t = KayakPrefabBuilder.Primitive(PrimitiveType.Cube, name, parent, pos, size, m);
                t.rotation = Quaternion.Euler(0f, yaw, 0f);
                return t;
            }
            // Fences: posts every 2.4 m with two rails, beside the trail to the van and along the start of the woods' loop
            void Fence(Vector3 a, Vector3 b)
            {
                var dir = b - a; dir.y = 0f;
                int n = Mathf.Max(1, Mathf.RoundToInt(dir.magnitude / 2.4f));
                float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
                for (int k = 0; k <= n; k++)
                {
                    var p = Vector3.Lerp(a, b, k / (float)n); p.y = G(p.x, p.z);
                    Box(props, "FencePost", p + Vector3.up * 0.55f, new Vector3(0.16f, 1.1f, 0.16f), wood, yaw);
                    if (k == n) continue;
                    var q = Vector3.Lerp(a, b, (k + 0.5f) / n); q.y = G(q.x, q.z);
                    float len = dir.magnitude / n;
                    Box(props, "FenceRail", q + Vector3.up * 0.85f, new Vector3(0.08f, 0.1f, len), woodLight, yaw);
                    Box(props, "FenceRail", q + Vector3.up * 0.45f, new Vector3(0.08f, 0.1f, len), woodLight, yaw);
                }
            }
            Fence(new Vector3(-4.2f, 0, -322f), new Vector3(-3.8f, 0, -304f));
            Fence(new Vector3(9f, 0, -333f), new Vector3(24f, 0, -342f));
            Fence(new Vector3(-50f, 0, -315f), new Vector3(-62f, 0, -326f));
            // A trail sign at the woods' entrance: a post and three arrow boards
            var sx = 9.5f; var sz = -328.5f; var sy = G(sx, sz);
            Box(props, "SignPost", new Vector3(sx, sy + 1.2f, sz), new Vector3(0.18f, 2.4f, 0.18f), wood);
            for (int k = 0; k < 3; k++)
                Box(props, "SignBoard", new Vector3(sx + 0.35f, sy + 1.9f - k * 0.42f, sz), new Vector3(0.9f, 0.3f, 0.06f), woodLight, k == 1 ? 180f : 0f).position += Vector3.right * (k == 1 ? -0.7f : 0f);
            // Lantern posts: an L post, a small glowing box and a warm light
            var lamp = GrayboxMaterials.Get("LanternGlow", new Color(1f, 0.82f, 0.45f));
            void Lantern(float x, float z)
            {
                float y = G(x, z);
                Box(props, "LanternPost", new Vector3(x, y + 1.3f, z), new Vector3(0.14f, 2.6f, 0.14f), wood);
                Box(props, "LanternArm", new Vector3(x + 0.35f, y + 2.5f, z), new Vector3(0.7f, 0.1f, 0.1f), wood);
                Box(props, "Lantern", new Vector3(x + 0.62f, y + 2.2f, z), new Vector3(0.24f, 0.32f, 0.24f), lamp);
                var light = new GameObject("LanternLight").AddComponent<Light>();
                light.transform.SetParent(props, false);
                light.transform.position = new Vector3(x + 0.62f, y + 2.1f, z);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.75f, 0.45f);
                light.range = 7f;
                light.intensity = 1.6f;
                light.shadows = LightShadows.None;
            }
            Lantern(-4.8f, -318f); Lantern(-1.2f, -300f); Lantern(10.5f, -335f); Lantern(-44f, -318f); Lantern(30f, -343f);
        }

        static GrassField.TuftType[] CarpetTypes()
        {
            var list = new List<GrassField.TuftType>();
            foreach (var (letter, kind) in new[] { ("A", GrassField.Tuft.Green), ("B", GrassField.Tuft.DarkLush) })
            {
                string fbx = $"Assets/_Project/Art/Models/Grass/GramaFita{letter}.fbx"; // the reference's ribbon grass
                Mesh near = null, far = null;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
                    if (a is Mesh m) { if (m.name.EndsWith("_LOD0")) near = m; else if (m.name.EndsWith("_LOD1")) far = m; }
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Art/Materials/Grass/M_GramaFita{letter}.mat");
                if (near && mat) list.Add(new GrassField.TuftType { kind = kind, near = near, far = far ? far : near, material = mat });
            }
            return list.ToArray();
        }

        static GrassField.TuftType[] GrassTypes()
        {
            var kinds = new[] { ("A", GrassField.Tuft.Green), ("B", GrassField.Tuft.DarkLush), ("C", GrassField.Tuft.TallYellow), ("D", GrassField.Tuft.DryTipped), ("E", GrassField.Tuft.CoolWater) }; // (C is the daisies now, see below)
            var list = new List<GrassField.TuftType>();
            foreach (var (letter, kind) in kinds)
            {
                string asset = letter == "C" ? "FloresBrancas" : "GramaFitaB"; // the tall ribbon grass, and the daisies
                string fbx = $"Assets/_Project/Art/Models/Grass/{asset}.fbx";
                Mesh near = null, far = null;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
                    if (a is Mesh m) { if (m.name.EndsWith("_LOD0")) near = m; else if (m.name.EndsWith("_LOD1")) far = m; }
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Art/Materials/Grass/M_{asset}.mat");
                if (near && mat) list.Add(new GrassField.TuftType { kind = kind, near = near, far = far ? far : near, material = mat });
            }
            return list.ToArray();
        }
    }
}
