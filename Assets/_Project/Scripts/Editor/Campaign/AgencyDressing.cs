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
        static readonly Rect Yard = new Rect(-42f, -368f, 64f, 108f);

        [MenuItem("CampanhaRio/Setup/Dress the Agency")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var root = GameObject.Find("Agencia").transform;
            foreach (string old in new[] { "Ground", "Trees", "Agency Terrain", "Forest", "Grass", "Paths" })
            {
                var t = root.Find(old);
                if (t) Object.DestroyImmediate(t.gameObject);
            }

            var paths = new GameObject("Paths").AddComponent<GroundPaths>();
            paths.transform.SetParent(root, false);
            P(paths, 2.4f, new Vector3(-6, 0, -326), new Vector3(6, 0, -330.5f));                       // to the orders board
            P(paths, 2.4f, new Vector3(-6, 0, -326), new Vector3(-12, 0, -334), new Vector3(-18, 0, -339)); // to the shed's door
            P(paths, 2.0f, new Vector3(-12, 0, -334), new Vector3(-8.5f, 0, -335.5f));                  // to the upgrades board
            P(paths, 2.8f, new Vector3(-6, 0, -326), new Vector3(-2, 0, -305), new Vector3(0, 0, -290)); // to the van
            P(paths, 1.8f, new Vector3(6, 0, -330.5f), new Vector3(26, 0, -340), new Vector3(48, 0, -352), new Vector3(52, 0, -378),
              new Vector3(20, 0, -390), new Vector3(-20, 0, -386), new Vector3(-56, 0, -372), new Vector3(-66, 0, -342),
              new Vector3(-48, 0, -318), new Vector3(-6, 0, -326));                                     // the loop through the woods
            paths.keepClear.Add(new Rect(-27f, -353f, 18f, 15f));   // the shed
            paths.keepClear.Add(new Rect(3.5f, -331f, 5f, 1.6f));   // the orders board
            paths.keepClear.Add(new Rect(-6f, -300f, 12f, 40f));    // the van's gravel and the road out

            var terrain = BuildTerrain(root, paths);
            Forest(root, terrain, paths);

            var grass = new GameObject("Grass").AddComponent<GrassField>();
            grass.transform.SetParent(root, false);
            grass.terrain = terrain;
            grass.noRiver = true;
            grass.densityNear = 3.2f;
            grass.densityFar = 3.2f;
            grass.drawDistance = 80f;
            grass.types = GrassTypes();

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
                float hills = Mathf.SmoothStep(0f, 1f, out_ / 45f) * 5f + (Mathf.PerlinNoise(wx * 0.025f + 5f, wz * 0.025f + 2f) - 0.5f) * 3f * Mathf.SmoothStep(0f, 1f, out_ / 20f);
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
            };
            int ar = data.alphamapResolution;
            var maps = new float[ar, ar, 3];
            for (int y = 0; y < ar; y++)
            for (int x = 0; x < ar; x++)
            {
                float wx = Origin.x + x / (float)(ar - 1) * Size.x, wz = Origin.z + y / (float)(ar - 1) * Size.z;
                var p = new Vector3(wx, 0f, wz);
                float earth = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.2f, 0.9f, paths.Distance(p)));   // the trails, soft edges
                earth = Mathf.Max(earth, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.8f, Mathf.PerlinNoise(wx * 0.05f + 3f, wz * 0.05f + 1f))) * 0.5f);
                float gravel = wx > -6f && wx < 6f && wz > -300f ? 1f : 0f;                                     // the van's gravel and the road out
                earth *= 1f - gravel;
                float grass = Mathf.Max(0f, 1f - earth - gravel);
                float sum = grass + earth + gravel;
                maps[y, x, 0] = grass / sum; maps[y, x, 1] = earth / sum; maps[y, x, 2] = gravel / sum;
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

        static void Forest(Transform root, Terrain terrain, GroundPaths paths)
        {
            var forest = new GameObject("Forest").transform;
            forest.SetParent(root, false);
            string[] pines = { "Pine/PinheiroA", "Pine/PinheiroB", "Pine/PinheiroC" };
            string[] leafy = { "Broadleaf/ArvoreFolhosaA", "Broadleaf/ArvoreFolhosaB", "Broadleaf/ArvoreFolhosaC" };
            string[] rocks = { "Rock/RochaA", "Rock/RochaB", "Rock/RochaC", "Rock/RochaD" };
            GameObject L(string p) => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Art/Models/{p}.fbx");
            var rng = new System.Random(77);
            float R() => (float)rng.NextDouble();
            var placed = new List<Vector3>();
            bool Free(Vector3 q, float d) { foreach (var o in placed) if ((o - q).sqrMagnitude < d * d) return false; return true; }
            int trees = 0, stones = 0;
            for (int i = 0; i < 40000 && trees < 1100; i++)
            {
                var p = new Vector3(Origin.x + R() * Size.x, 0f, Origin.z + R() * Size.z);
                if (InYard(p.x, p.z, 4f) || p.z > -268f) continue;                     // the yard, and the road segment's edge
                bool leaf = R() < 0.45f;
                float size = leaf ? Mathf.Lerp(1.2f, 1.9f, R()) : Mathf.Lerp(1.4f, 2.4f, R());
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
            Debug.Log($"[Campanha] Agency forest: {trees} trees, {stones} rocks");
        }

        static GrassField.TuftType[] GrassTypes()
        {
            var kinds = new[] { ("A", GrassField.Tuft.Green), ("B", GrassField.Tuft.DarkLush), ("C", GrassField.Tuft.TallYellow), ("D", GrassField.Tuft.DryTipped), ("E", GrassField.Tuft.CoolWater) };
            var list = new List<GrassField.TuftType>();
            foreach (var (letter, kind) in kinds)
            {
                string fbx = $"Assets/_Project/Art/Models/Grass/TufoGrama{letter}.fbx";
                Mesh near = null, far = null;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
                    if (a is Mesh m) { if (m.name.EndsWith("_LOD0")) near = m; else if (m.name.EndsWith("_LOD1")) far = m; }
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Art/Materials/Grass/M_TufoGrama{letter}.mat");
                if (near && mat) list.Add(new GrassField.TuftType { kind = kind, near = near, far = far ? far : near, material = mat });
            }
            return list.ToArray();
        }
    }
}
