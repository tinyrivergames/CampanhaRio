using System.IO;
using CampanhaRio.River;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Phase 3, the forest's first pass on the Rio do Moinho (and the KayakTest terrain it shares):
    ///   - the ground's main textures (stylized, generated): grass with lighter and darker patches, earth, gravel;
    ///   - the terrain painted with them: gravel at the water's edge, earth on the banks and steep slopes, grass elsewhere
    ///     with a few bare patches;
    ///   - the forest: clusters of the three approved pines (A, B young, C old) with clearings between them, and the four
    ///     approved rocks (pebbles and slabs at the banks, tall stones up the slopes).
    /// Grass tufts, bushes and flowers come next (Phase 3 assets, each through the approval loop).
    /// Menu: CampanhaRio > Setup > Dress the Forest. Batch: CampanhaRio.Editor.ForestDressing.Build
    /// </summary>
    public static class ForestDressing
    {
        const string TexDir = "Assets/_Project/Art/Textures/Ground";
        const string ScenePath = "Assets/_Project/Scenes/Segments/Rio_Moinho.unity";

        [MenuItem("CampanhaRio/Setup/Dress the Forest")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var river = Object.FindAnyObjectByType<RiverPath>();
            var terrain = Object.FindAnyObjectByType<Terrain>();
            var layers = new[]
            {
                Layer("Grass", new Color(0.27f, 0.46f, 0.2f), new Color(0.32f, 0.52f, 0.23f), new Color(0.22f, 0.4f, 0.17f), 0.18f, 7f), // the ribbon grass' root green (seen between the clumps)
                Layer("Earth", new Color(0.47f, 0.37f, 0.27f), new Color(0.55f, 0.44f, 0.32f), new Color(0.38f, 0.29f, 0.21f), 0.25f, 5f),
                Layer("Gravel", new Color(0.56f, 0.54f, 0.5f), new Color(0.68f, 0.66f, 0.6f), new Color(0.42f, 0.4f, 0.37f), 0.45f, 3f),
            };
            Paint(terrain, river, layers);
            Populate(terrain, river);
            AddGrass(terrain, river);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Campanha] Forest dressed: ground textures, painted terrain, pines and rocks.");
        }

        // ---------------------------------------------------------------- ground
        static TerrainLayer Layer(string name, Color baseColor, Color light, Color dark, float speckle, float tile)
        {
            Directory.CreateDirectory(TexDir);
            string texPath = $"{TexDir}/T_Ground_{name}.png";
            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var rng = new System.Random(name.GetHashCode());
            float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Tileable patches: noise on a torus (sin/cos of the coordinates)
                float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                float big = Mathf.PerlinNoise(ox + Mathf.Cos(u) * 1.2f + Mathf.Sin(v) * 0.6f, oy + Mathf.Sin(u) * 1.2f + Mathf.Cos(v) * 0.6f);
                float small = Mathf.PerlinNoise(ox * 2f + Mathf.Cos(u) * 5f + Mathf.Sin(v) * 3f, oy * 2f + Mathf.Sin(u) * 5f + Mathf.Cos(v) * 3f);
                var c = Color.Lerp(dark, light, Mathf.SmoothStep(0f, 1f, big * 0.8f + small * 0.4f - 0.1f));
                c = Color.Lerp(c, baseColor, 0.35f);
                if (rng.NextDouble() < speckle * 0.08) c = Color.Lerp(c, rng.NextDouble() < 0.5 ? light : dark, 0.6f); // grains, blades
                tex.SetPixel(x, y, c);
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

            string layerPath = $"{TexDir}/TL_{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layerPath); }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = 0f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        static void Paint(Terrain terrain, RiverPath river, TerrainLayer[] layers)
        {
            var data = terrain.terrainData;
            data.terrainLayers = layers;
            int res = data.alphamapResolution;
            var maps = new float[res, res, layers.Length];
            var origin = terrain.transform.position;
            int hint = -1;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float nx = x / (float)(res - 1), nz = y / (float)(res - 1);
                var p = origin + new Vector3(nx * data.size.x, 0f, nz * data.size.z);
                float edge = river.SampleShape(p, ref hint).edgeDistance;
                float steep = data.GetSteepness(nx, nz);
                float gravel = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1.8f, edge));
                float earth = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 4.5f, edge));
                earth = Mathf.Max(earth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, 55f, steep)));
                float patch = Mathf.PerlinNoise(p.x * 0.045f + 11f, p.z * 0.045f + 7f);
                earth = Mathf.Max(earth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 0.82f, patch)) * 0.6f); // bare patches
                earth *= 1f - gravel;
                float grass = Mathf.Max(0f, 1f - earth - gravel);
                float sum = grass + earth + gravel;
                maps[y, x, 0] = grass / sum; maps[y, x, 1] = earth / sum; maps[y, x, 2] = gravel / sum;
            }
            data.SetAlphamaps(0, 0, maps);
            EditorUtility.SetDirty(data);
        }

        // ---------------------------------------------------------------- the forest
        static void Populate(Terrain terrain, RiverPath river)
        {
            foreach (string old in new[] { "Forest (pine test)", "Rocks (rock test)", "Forest" })
            {
                var go = GameObject.Find(old);
                if (go) Object.DestroyImmediate(go);
            }
            var forest = new GameObject("Forest").transform;
            var pines = new[] { Load("Pine/PinheiroA"), Load("Pine/PinheiroB"), Load("Pine/PinheiroC") };
            var rocks = new[] { Load("Rock/RochaA"), Load("Rock/RochaB"), Load("Rock/RochaC"), Load("Rock/RochaD") };
            var data = terrain.terrainData;
            var origin = terrain.transform.position;
            var rng = new System.Random(2031);
            float R() => (float)rng.NextDouble();
            int hint = -1, trees = 0, stones = 0;
            var placed = new System.Collections.Generic.List<Vector3>();
            bool Free(Vector3 p, float d) { foreach (var q in placed) if ((q - p).sqrMagnitude < d * d) return false; return true; }

            // Trees: dense where the "forest" noise is high, clearings where it is low, never at the water
            for (int i = 0; i < 120000 && trees < 3000; i++)
            {
                var p = origin + new Vector3(R() * data.size.x, 0f, R() * data.size.z);
                float edge = river.SampleShape(p, ref hint).edgeDistance;
                if (edge < 3.5f || edge > 70f) continue; // the forest the kayaker sees
                float density = Mathf.PerlinNoise(p.x * 0.012f + 3f, p.z * 0.012f + 9f);
                float want = edge < 15f ? 0.2f : 0.16f; // a lighter fringe along the banks, then the woods
                if (density < want + R() * 0.25f) continue;
                if (data.GetSteepness((p.x - origin.x) / data.size.x, (p.z - origin.z) / data.size.z) > 52f) continue;
                if (!Free(p, 2.8f)) continue;
                p.y = terrain.SampleHeight(p) + origin.y - 0.15f;
                int kind = R() < 0.5f ? 0 : R() < 0.6f ? 1 : 2;
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(pines[kind], forest);
                tree.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, R() * 360f, 0f));
                tree.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.25f, R());
                placed.Add(p);
                trees++;
            }

            // Rocks: pebbles and slabs along the banks (some in the shallows), the tall ones and big boulders up the slopes
            var stoneRoot = new GameObject("Rocks").transform;
            stoneRoot.SetParent(forest, false);
            for (int i = 0; i < 40000 && stones < 700; i++)
            {
                var p = origin + new Vector3(R() * data.size.x, 0f, R() * data.size.z);
                var s = river.SampleShape(p, ref hint);
                bool bank = s.edgeDistance > -1.4f && s.edgeDistance < 4f;
                bool woods = s.edgeDistance > 8f && R() < 0.25f;
                if (!bank && !woods) continue;
                if (!Free(p, 1.6f)) continue;
                int kind = bank ? (R() < 0.45f ? 1 : R() < 0.6f ? 2 : 0) : (R() < 0.5f ? 3 : 0);
                float scale = kind == 1 ? Mathf.Lerp(0.5f, 1.2f, R()) : Mathf.Lerp(0.5f, 1.15f, R());
                p.y = terrain.SampleHeight(p) + origin.y - 0.18f * scale;
                var rock = (GameObject)PrefabUtility.InstantiatePrefab(rocks[kind], stoneRoot);
                rock.transform.SetPositionAndRotation(p, Quaternion.Euler(R() * 8f - 4f, R() * 360f, R() * 8f - 4f));
                rock.transform.localScale = Vector3.one * scale;
                placed.Add(p);
                stones++;
            }
            Debug.Log($"[Campanha] Forest: {trees} pines, {stones} rocks");
        }

        /// <summary>The grass: a GrassField (instanced, generated around the camera) with the five approved tufts.</summary>
        static void AddGrass(Terrain terrain, RiverPath river)
        {
            var old = Object.FindAnyObjectByType<World.GrassField>();
            if (old) Object.DestroyImmediate(old.gameObject);
            var field = new GameObject("Grass").AddComponent<World.GrassField>();
            field.terrain = terrain;
            field.river = river;
            var kinds = new[] { ("A", World.GrassField.Tuft.Green), ("B", World.GrassField.Tuft.DarkLush), ("C", World.GrassField.Tuft.TallYellow), ("D", World.GrassField.Tuft.DryTipped), ("E", World.GrassField.Tuft.CoolWater) };
            var types = new System.Collections.Generic.List<World.GrassField.TuftType>();
            foreach (var (letter, kind) in kinds)
            {
                string fbx = $"Assets/_Project/Art/Models/Grass/TufoGrama{letter}.fbx";
                Mesh near = null, far = null;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
                    if (a is Mesh m) { if (m.name.EndsWith("_LOD0")) near = m; else if (m.name.EndsWith("_LOD1")) far = m; }
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Art/Materials/Grass/M_TufoGrama{letter}.mat");
                if (!near || !mat) { Debug.LogWarning($"[Campanha] Grass: TufoGrama{letter} not found ({fbx})"); continue; }
                mat.enableInstancing = true;
                types.Add(new World.GrassField.TuftType { kind = kind, near = near, far = far ? far : near, material = mat });
            }
            field.types = types.ToArray();
            Debug.Log($"[Campanha] Grass: {field.types.Length} tuft types");
        }

        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Art/Models/{path}.fbx");
    }
}
