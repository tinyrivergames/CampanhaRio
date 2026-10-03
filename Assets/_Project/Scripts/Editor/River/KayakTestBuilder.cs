using System.IO;
using CampanhaRio.CameraSystem;
using CampanhaRio.Dev;
using CampanhaRio.Kayak;
using CampanhaRio.River;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Scenes/Dev/KayakTest.unity: the test river for the kayak, a GRAYBOX of the old project's River_01 (~1.1 km, seven
    /// sections). Only what the kayak feels is rebuilt, with the old builder's exact numbers and random seeds: the water
    /// (knots, widths, flow), the bed and banks (the same sculpted terrain), the rocks, eddies, the backwater, the creek
    /// inflow, the river features (riffles, chute + wave train, ledge, pour-overs, log ramp), the fallen trees, the logjam,
    /// the shallow rocks and the pier. No art: plain shapes and flat colours. The same river means the FeelBenchmark numbers
    /// can be compared one to one with the old project (Docs/PORTED.md).
    /// Menu: CampanhaRio > Setup > Build KayakTest Scene. Batch: CampanhaRio.Editor.KayakTestBuilder.Build
    /// </summary>
    public static class KayakTestBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dev/KayakTest.unity";
        const string DataDir = "Assets/_Project/Scenes/Dev/KayakTest";
        const string TerrainDataPath = DataDir + "/KayakTest_TerrainData.asset";

        // River_01 knots: x, z, water height, left half-width, right half-width
        static readonly float[,] Knots =
        {
            { 0f, 0f, 36.00f, 14f, 14f },      // 0  forest pool
            { 4f, 48f, 35.95f, 13f, 12f },     // 1
            { -4f, 95f, 35.80f, 8.5f, 8.5f },  // 2  corridor
            { 8f, 148f, 35.35f, 7f, 7f },      // 3
            { -2f, 200f, 34.85f, 7.5f, 7f },   // 4
            { -16f, 250f, 34.35f, 7f, 7.5f },  // 5
            { -8f, 300f, 33.80f, 7f, 7f },     // 6
            { 8f, 345f, 33.05f, 6f, 6f },      // 7  gorge
            { 16f, 395f, 31.80f, 4.8f, 4.8f }, // 8  chute
            { 10f, 445f, 30.60f, 5.5f, 5.5f }, // 9
            { 0f, 492f, 29.40f, 6f, 6f },      // 10
            { -20f, 537f, 28.95f, 10f, 10f },  // 11 recovery bend
            { -42f, 585f, 28.75f, 13f, 12f },  // 12
            { -38f, 636f, 28.55f, 10f, 9f },   // 13
            { -22f, 686f, 28.45f, 10f, 24f },  // 14 backwater pocket (right)
            { -14f, 736f, 28.35f, 10f, 24f },  // 15
            { -16f, 786f, 28.15f, 8.5f, 8.5f },// 16 logjam run
            { -4f, 836f, 27.50f, 8f, 8f },     // 17
            { 14f, 884f, 26.90f, 8.5f, 8.5f }, // 18
            { 22f, 932f, 26.40f, 12f, 12f },   // 19 the opening
            { 28f, 982f, 26.25f, 22f, 22f },   // 20 end lake
            { 30f, 1040f, 26.20f, 28f, 28f },  // 21
            { 30f, 1100f, 26.20f, 28f, 28f },  // 22
        };

        enum Section { Pool, Corridor, Gorge, Recovery, Backwater, Logjam, Opening }
        static readonly int[] SectionStartKnot = { 0, 2, 7, 10, 13, 15, 18 };
        const float KayakStart = 14f;

        /// <summary>Riffle stretches (distance from, to): the bed is shallow gravel there.</summary>
        static readonly Vector2[] Shallows = { new Vector2(172f, 190f), new Vector2(917f, 933f) };

        /// <summary>Rocks that make eddies (distance, lateral, size).</summary>
        static readonly Vector3[] Rocks =
        {
            new Vector3(160f, -3.4f, 2.8f), new Vector3(214f, 3.0f, 3.2f), new Vector3(262f, -2.6f, 2.6f), new Vector3(312f, 2.8f, 3.0f),
            new Vector3(356f, -2.4f, 1.8f), new Vector3(382f, 2.1f, 1.6f), new Vector3(438f, -2.8f, 1.7f),
            new Vector3(876f, 3.4f, 2.2f),
        };

        /// <summary>The old rock prefabs (Small, Medium, Large): a 0.5 m sphere collider on a root scaled per placement.</summary>
        static readonly float[] RockVariantScales = { 1.8f, 2.4f, 3.2f };

        static Material water, rock, wood, sand;
        static int obstacleLayer, environmentLayer;

        [MenuItem("CampanhaRio/Setup/Build KayakTest Scene")]
        public static void Build()
        {
            obstacleLayer = LayerMask.NameToLayer("Obstacle");
            environmentLayer = LayerMask.NameToLayer("Environment");
            water = WaterAssets.WaterMaterial();
            rock = GrayboxMaterials.Get("Rock", new Color(0.78f, 0.55f, 0.36f));
            wood = GrayboxMaterials.Get("Wood", new Color(0.45f, 0.32f, 0.22f));
            sand = GrayboxMaterials.Get("Ground", new Color(0.52f, 0.66f, 0.36f));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLightAndCamera();

            var riverGo = new GameObject("River", typeof(SplineContainer), typeof(RiverPath));
            var river = riverGo.GetComponent<RiverPath>();
            BuildSpline(river);
            var terrain = BuildTerrain(river);

            var zones = new GameObject("Current Zones").transform;
            BuildBackwaterZone(river, zones);
            BuildPier(river);
            BuildRocks(river);
            BuildFeatures(river);
            BuildWaterElements(river, terrain);
            BuildCrossCurrent(river, zones);
            BuildWaterMesh(river);
            PlaceKayak(river);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Campanha] KayakTest built: {river.Length:0} m, {river.GetPointAtDistance(0f).point.y - river.GetPointAtDistance(river.Length).point.y:0.0} m of descent.");
        }

        // ------------------------------------------------------------------ river

        static void BuildSpline(RiverPath river)
        {
            river.transform.position = Vector3.zero;
            var spline = river.Container.Spline;
            spline.Clear();
            int n = Knots.GetLength(0);
            for (int k = 0; k < n; k++)
                spline.Add(new float3(Knots[k, 0], Knots[k, 2], Knots[k, 1]), TangentMode.AutoSmooth);
            river.leftWidth = new SplineData<float> { PathIndexUnit = PathIndexUnit.Distance };
            river.rightWidth = new SplineData<float> { PathIndexUnit = PathIndexUnit.Distance };
            for (int k = 0; k < n; k++)
            {
                float distance = spline.ConvertIndexUnit(k, PathIndexUnit.Knot, PathIndexUnit.Distance);
                RiverPathEditor.AddOrReplace(river.leftWidth, distance, Knots[k, 3]);
                RiverPathEditor.AddOrReplace(river.rightWidth, distance, Knots[k, 4]);
            }
            river.MarkChanged();
        }

        static Section SectionAt(RiverPath river, float distance)
        {
            for (int i = SectionStartKnot.Length - 1; i >= 0; i--)
                if (distance >= river.GetKnotDistance(SectionStartKnot[i])) return (Section)i;
            return Section.Pool;
        }

        static float SectionStart(RiverPath river, Section s) => river.GetKnotDistance(SectionStartKnot[(int)s]);
        static float SectionEnd(RiverPath river, Section s) =>
            (int)s + 1 < SectionStartKnot.Length ? river.GetKnotDistance(SectionStartKnot[(int)s + 1]) : river.Length;

        // ------------------------------------------------------------------ terrain (the old builder's bed and banks)

        struct Bank { public float height, rise, valleySlope, hills, bedDepth; }

        static Bank BankFor(Section s) => s switch
        {
            Section.Pool => new Bank { height = 2.2f, rise = 6f, valleySlope = 0.28f, hills = 8f, bedDepth = 1.8f },
            Section.Corridor => new Bank { height = 4.2f, rise = 5f, valleySlope = 0.38f, hills = 10f, bedDepth = 1.4f },
            Section.Gorge => new Bank { height = 5f, rise = 3.5f, valleySlope = 0.55f, hills = 14f, bedDepth = 1.5f },
            Section.Recovery => new Bank { height = 1.6f, rise = 8f, valleySlope = 0.3f, hills = 8f, bedDepth = 1.2f },
            Section.Backwater => new Bank { height = 1.3f, rise = 8f, valleySlope = 0.28f, hills = 8f, bedDepth = 1.0f },
            Section.Logjam => new Bank { height = 3.6f, rise = 5f, valleySlope = 0.36f, hills = 10f, bedDepth = 1.4f },
            _ => new Bank { height = 0.8f, rise = 14f, valleySlope = 0.07f, hills = 3f, bedDepth = 2.2f },
        };

        static Bank BankAt(RiverPath river, float distance)
        {
            var here = SectionAt(river, distance); // blend over 25 m around section boundaries
            float start = SectionStart(river, here), end = SectionEnd(river, here);
            var b = BankFor(here);
            if (distance - start < 25f && here > 0) return Lerp(BankFor(here - 1), b, 0.5f + 0.5f * (distance - start) / 25f);
            if (end - distance < 25f && (int)here < 6) return Lerp(b, BankFor(here + 1), 0.5f - 0.5f * (end - distance) / 25f);
            return b;
        }

        static Bank Lerp(Bank a, Bank b, float t) => new Bank
        {
            height = Mathf.Lerp(a.height, b.height, t), rise = Mathf.Lerp(a.rise, b.rise, t), valleySlope = Mathf.Lerp(a.valleySlope, b.valleySlope, t),
            hills = Mathf.Lerp(a.hills, b.hills, t), bedDepth = Mathf.Lerp(a.bedDepth, b.bedDepth, t),
        };

        static Terrain BuildTerrain(RiverPath river, float margin = 260f, float baseY = 18f)
        {
            Directory.CreateDirectory(DataDir);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (!data) { data = new TerrainData(); AssetDatabase.CreateAsset(data, TerrainDataPath); }

            var bounds = new Bounds(river.GetPointAtDistance(0f).point, Vector3.zero);
            for (float d = 0f; d <= river.Length; d += 5f)
            {
                var s = river.GetPointAtDistance(d);
                bounds.Encapsulate(s.point - s.right * s.leftWidth);
                bounds.Encapsulate(s.point + s.right * s.rightWidth);
            }
            bounds.Expand(new Vector3(margin, 0f, margin));
            data.heightmapResolution = 1025;
            data.alphamapResolution = 256;
            data.size = new Vector3(bounds.size.x, 60f, bounds.size.z);
            Vector3 origin = new Vector3(bounds.min.x, baseY, bounds.min.z);

            int res = data.heightmapResolution;
            var heights = new float[res, res];
            int hint = -1;
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float wx = origin.x + x / (res - 1f) * data.size.x, wz = origin.z + z / (res - 1f) * data.size.z;
                heights[z, x] = Mathf.Clamp01((HeightFor(river, wx, wz, ref hint) - baseY) / data.size.y);
            }
            data.SetHeights(0, 0, heights);
            data.terrainLayers = new[] { GroundLayer() };
            EditorUtility.SetDirty(data);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.layer = environmentLayer;
            go.transform.position = origin;
            var terrain = go.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            return terrain;
        }

        static float HeightFor(RiverPath river, float wx, float wz, ref int hint)
        {
            var s = river.SampleShape(new Vector3(wx, 0f, wz), ref hint);
            var b = BankAt(river, s.distanceAlong);
            float e = s.edgeDistance, surface = s.point.y;
            if (river.priority <= 0 && (s.distanceAlong <= 0.01f || s.distanceAlong >= river.Length - 0.01f))
            {
                Vector3 off = new Vector3(wx - s.point.x, 0f, wz - s.point.z);
                float past = Vector3.Dot(off, s.direction) * (s.distanceAlong <= 0.01f ? -1f : 1f);
                if (past > 0f) e = Mathf.Max(e, past);
            }
            if (e < 0f)
            {
                // Bed: shallow at the shore, deepest in the middle; riffles run over a shallow gravel bed
                float inward = Mathf.Clamp01(-e / 4.5f);
                float depth = b.bedDepth;
                foreach (var shallow in Shallows)
                    depth = Mathf.Lerp(depth, 0.45f, Smooth(shallow.x - 8f, shallow.x, s.distanceAlong) * (1f - Smooth(shallow.y, shallow.y + 8f, s.distanceAlong)));
                return surface - Mathf.Lerp(0.2f, depth, inward * inward * (3f - 2f * inward));
            }
            float lip = surface + 0.3f;
            float rise = b.height * Smooth(0f, b.rise, e);
            float valley = Mathf.Max(0f, e - b.rise) * b.valleySlope;
            float hills = (Mathf.PerlinNoise(wx * 0.012f + 3.3f, wz * 0.012f + 9.1f) - 0.3f) * b.hills * Smooth(b.rise, b.rise + 45f, e);
            float bumps = (Mathf.PerlinNoise(wx * 0.18f, wz * 0.18f) - 0.5f) * 0.5f * Smooth(0.5f, 3f, e);
            return lip + rise + valley + hills + bumps;
        }

        static TerrainLayer GroundLayer()
        {
            string path = DataDir + "/KayakTest_Ground.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer) return layer;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "KayakTest_GroundTex" };
            var c = new Color(0.52f, 0.66f, 0.36f);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            tex.SetPixels(pixels);
            tex.Apply();
            AssetDatabase.CreateAsset(tex, DataDir + "/KayakTest_GroundTex.asset");
            layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(4f, 4f) };
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        // ------------------------------------------------------------------ places

        static void BuildBackwaterZone(RiverPath river, Transform parent)
        {
            float d = (river.GetKnotDistance(14) + river.GetKnotDistance(15)) * 0.5f;
            var s = river.GetPointAtDistance(d);
            var go = new GameObject("Backwater", typeof(CurrentZone));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(s.point + s.right * 17f, Quaternion.LookRotation(s.direction));
            var zone = go.GetComponent<CurrentZone>();
            zone.type = CurrentZoneType.Backwater;
            zone.size = new Vector3(15f, 3f, 62f);
            zone.edgeFade = 5f;
            zone.currentMultiplier = 0.05f;
        }

        /// <summary>The camp pier in the backwater (the old placeholder: an 8 m deck on a box collider).</summary>
        static void BuildPier(RiverPath river)
        {
            float d = (river.GetKnotDistance(14) + river.GetKnotDistance(15)) * 0.5f;
            var s = river.GetPointAtDistance(d);
            Vector3 pier = s.point + s.right * (s.rightWidth - 4f) - s.direction * 6f;
            pier.y = river.GetWaterHeight(pier);
            var go = new GameObject("Pier");
            go.layer = obstacleLayer;
            go.transform.SetPositionAndRotation(pier, Quaternion.LookRotation(-s.right));
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.1f, 0f);
            box.size = new Vector3(2.2f, 1f, 8f);
            KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Deck", go.transform, new Vector3(0f, 0.4f, 0f), new Vector3(2.2f, 0.15f, 8f), wood);
        }

        static void BuildRocks(RiverPath river)
        {
            var parent = new GameObject("River Rocks").transform;
            var rng = new System.Random(7);
            foreach (var r in Rocks) PlaceRock(river, parent, r.x, r.y, r.z, (float)rng.NextDouble() * 360f);

            // Recovery bend: a big boulder on the inside of the bend makes the big eddy to catch
            float bend = river.GetKnotDistance(12) - 6f;
            var bs = river.GetPointAtDistance(bend);
            float inside = bs.curvature >= 0f ? 1f : -1f;
            float lateral = inside * ((inside > 0f ? bs.rightWidth : bs.leftWidth) - 3.2f);
            var big = PlaceRock(river, parent, bend, lateral, 3.4f, 40f);
            var flow = big.GetComponent<FlowObstacle>();
            flow.eddyLength = 6f; flow.eddyWidth = 1.5f; flow.returnFlow = 0.5f;
            flow.Refresh();
        }

        static GameObject PlaceRock(RiverPath river, Transform parent, float distance, float lateral, float size, float yaw)
        {
            var s = river.GetPointAtDistance(distance);
            float variantScale = ClosestVariant(size);
            Vector3 p = s.point + s.right * lateral;
            p.y = river.GetWaterHeight(p) - size * 0.28f;
            var go = Boulder(parent, p, Quaternion.Euler(0f, yaw, 0f), Vector3.one * (size / variantScale), true);
            go.AddComponent<FlowObstacle>().Refresh();
            return go;
        }

        static float ClosestVariant(float size)
        {
            float best = RockVariantScales[0];
            foreach (float v in RockVariantScales) if (Mathf.Abs(v - size) < Mathf.Abs(best - size)) best = v;
            return best;
        }

        /// <summary>The old boulder: a 0.5 m sphere collider (Obstacle) with a squashed blob inside, scaled at the root.</summary>
        static GameObject Boulder(Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, bool solid)
        {
            var go = new GameObject("Rock");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.layer = obstacleLayer;
            if (solid) go.AddComponent<SphereCollider>().radius = 0.5f;
            KayakPrefabBuilder.Primitive(PrimitiveType.Sphere, "Visual", go.transform, Vector3.zero, new Vector3(1f, 0.75f, 1.1f), rock);
            return go;
        }

        static void BuildFeatures(RiverPath river)
        {
            var parent = new GameObject("River Features").transform;
            foreach (var shallow in Shallows)
            {
                var riffle = Feature(river, parent, "Riffle", RiverFeatureType.Riffle, (shallow.x + shallow.y) * 0.5f, 0f);
                riffle.length = shallow.y - shallow.x;
            }
            var waves = Feature(river, parent, "Chute + Wave Train", RiverFeatureType.ChuteWaveTrain, river.GetKnotDistance(8) + 6f, 0f);
            waves.height = 0.62f; waves.length = 22f; waves.waveCount = 4; waves.chuteLength = 12f;

            var ledge = Feature(river, parent, "Ledge", RiverFeatureType.Ledge, river.GetKnotDistance(10) - 22f, 0f);
            ledge.height = 0.9f; ledge.length = 11f;
            LedgeShelf(river, ledge);

            var pour1 = Feature(river, parent, "Pour-over (gorge)", RiverFeatureType.PourOver, river.GetKnotDistance(7) + 17f, 1.4f);
            pour1.height = 0.3f; pour1.length = 6.5f; pour1.width = 3.2f;
            SubmergedRock(river, pour1.transform, 1.6f, 0.42f);
            var pour2 = Feature(river, parent, "Pour-over (logjam run)", RiverFeatureType.PourOver, river.GetKnotDistance(16) + 34f, -2.4f);
            pour2.height = 0.35f; pour2.length = 5f; pour2.width = 3f;
            SubmergedRock(river, pour2.transform, 1.5f, 0.4f);

            var ramp = Feature(river, parent, "Log Ramp", RiverFeatureType.LogRamp, river.GetKnotDistance(17) + 22f, 1.2f);
            ramp.height = 0.45f; ramp.length = 7f; ramp.width = 5.5f; ramp.logAngle = 25f;
            RampLog(river, ramp);
            foreach (var f in parent.GetComponentsInChildren<RiverFeature>()) f.Refresh();
        }

        static RiverFeature Feature(RiverPath river, Transform parent, string name, RiverFeatureType type, float distance, float lateral)
        {
            var s = river.GetPointAtDistance(distance);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(s.point + s.right * lateral, Quaternion.LookRotation(s.direction));
            var feature = go.AddComponent<RiverFeature>();
            feature.type = type;
            return feature;
        }

        /// <summary>Rock slabs at both ends of the ledge's lip (solid, the old bank rocks) and one under it (no collider).</summary>
        static void LedgeShelf(RiverPath river, RiverFeature ledge)
        {
            var s = river.Sample(ledge.transform.position);
            foreach (int side in new[] { -1, 1 })
            {
                float w = side > 0 ? s.rightWidth : s.leftWidth;
                Vector3 p = s.point + s.right * side * (w - 0.6f);
                p.y = river.GetWaterHeight(p) - 0.35f;
                var slab = new GameObject("Slab");
                slab.transform.SetParent(ledge.transform, false);
                slab.transform.SetPositionAndRotation(p, Quaternion.LookRotation(s.direction) * Quaternion.Euler(0f, side * 20f, 0f));
                slab.transform.localScale = new Vector3(2.6f, 0.9f, 1.8f);
                slab.layer = obstacleLayer;
                slab.AddComponent<SphereCollider>().radius = 0.48f;
                KayakPrefabBuilder.Primitive(PrimitiveType.Sphere, "Visual", slab.transform, Vector3.zero, new Vector3(1.2f, 0.7f, 1f), rock);
            }
            Vector3 under = s.point + s.direction * 0.4f;
            under.y = river.GetWaterHeight(under) - 0.75f;
            var shelf = KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Shelf", ledge.transform, Vector3.zero, Vector3.one, rock);
            shelf.SetPositionAndRotation(under, Quaternion.LookRotation(s.right));
            shelf.localScale = new Vector3((s.leftWidth + s.rightWidth) * 0.4f, 0.5f, 1.6f);
        }

        static void SubmergedRock(RiverPath river, Transform at, float size, float depth)
        {
            float variantScale = ClosestVariant(size);
            Vector3 p = at.position;
            p.y = river.GetWaterHeight(p) - depth - size * 0.35f;
            Boulder(at, p, Quaternion.Euler(0f, 30f, 0f), new Vector3(1f, 0.6f, 1f) * (size / variantScale), false);
        }

        /// <summary>The ramp's log is visual only (the kayak rides the feature's water, not a collider).</summary>
        static void RampLog(RiverPath river, RiverFeature ramp)
        {
            ramp.Refresh();
            float len = ramp.width + 1.5f;
            Vector3 dir = ramp.LogDirection;
            Vector3 c = ramp.transform.position;
            c.y = river.GetWaterHeight(c) - 0.18f;
            var log = KayakPrefabBuilder.Primitive(PrimitiveType.Cylinder, "Log", ramp.transform, Vector3.zero, new Vector3(0.5f, len * 0.5f, 0.5f), wood);
            log.SetPositionAndRotation(c, Quaternion.FromToRotation(Vector3.up, dir));
            log.gameObject.layer = environmentLayer;
        }

        static void BuildCrossCurrent(RiverPath river, Transform parent)
        {
            var s = river.GetPointAtDistance(river.GetKnotDistance(5) + 12f);
            var go = new GameObject("Creek Inflow (left)");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(s.point - s.right * (s.leftWidth - 3f), Quaternion.LookRotation(s.right + s.direction * 0.3f));
            var cross = go.AddComponent<CrossCurrent>();
            cross.size = new Vector2(7f, 9f);
            cross.strength = 1.4f;
        }

        // ------------------------------------------------------------------ the old "water elements": fallen trees, logjam, shallow rocks

        static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static void BuildWaterElements(RiverPath river, Terrain terrain)
        {
            var root = new GameObject("Water Elements").transform;
            var rng = new System.Random(21);
            // The old builder placed resting drift pieces here first; they are gone, but their random draws are kept so
            // the logjam and the shallow rocks below land exactly where they were
            var zone = GameObject.Find("Backwater");
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = zone.transform.position + new Vector3(Range(rng, -5f, 5f), 0f, Range(rng, -18f, 18f));
                SkipDriftPiece(river, p, rng);
            }
            float lake = river.Length - 90f;
            for (int i = 0; i < 3; i++)
            {
                var s = river.GetPointAtDistance(lake + i * 22f);
                SkipDriftPiece(river, s.point + s.right * Range(rng, -12f, 12f), rng);
            }

            FallenTree(river, terrain, root, river.GetKnotDistance(4) + 26f, -1, 5.2f, 50f);
            FallenTree(river, terrain, root, river.GetKnotDistance(16) + 8f, 1, 4.6f, 55f);
            Logjam(river, root, river.GetKnotDistance(17) - 6f, -1, rng);
            ShallowRocks(river, root, rng);
        }

        static void SkipDriftPiece(RiverPath river, Vector3 p, System.Random rng)
        {
            if (river.Sample(p).edgeDistance > -1f) return;
            Range(rng, 0f, 360f);
        }

        static void FallenTree(RiverPath river, Terrain terrain, Transform parent, float distance, int side, float reach, float angle)
        {
            var s = river.GetPointAtDistance(distance);
            float width = side > 0 ? s.rightWidth : s.leftWidth;
            Vector3 outward = s.right * side;
            Vector3 dir = Vector3.Lerp(-outward, s.direction, 1f - angle / 90f).normalized;
            float length = reach + 5f;
            Vector3 root = s.point + outward * (width + 4f);
            root.y = terrain.SampleHeight(root) + terrain.transform.position.y - 0.3f;
            Vector3 tip = root + dir * length;
            tip.y = river.GetWaterHeight(tip) - 0.2f;

            var tree = new GameObject("Fallen Tree").transform;
            tree.SetParent(parent, false);
            var trunk = LogPiece(tree, "Trunk", root, tip, 0.45f);
            var col = trunk.AddComponent<CapsuleCollider>();
            col.direction = 1; col.radius = 0.4f; col.height = length; col.center = new Vector3(0f, length * 0.5f, 0f);
            trunk.layer = obstacleLayer;

            var eddy = new GameObject("Eddy").transform;
            eddy.SetParent(tree, false);
            Vector3 mid = s.point + outward * (width - reach * 0.5f);
            mid.y = river.GetWaterHeight(mid);
            eddy.position = mid;
            var flow = eddy.gameObject.AddComponent<FlowObstacle>();
            flow.radius = reach * 0.45f; flow.eddyLength = 3f; flow.eddyWidth = 1f;
            flow.Refresh();
        }

        static void Logjam(RiverPath river, Transform parent, float distance, int side, System.Random rng)
        {
            var s = river.GetPointAtDistance(distance);
            float width = side > 0 ? s.rightWidth : s.leftWidth;
            Vector3 center = s.point + s.right * side * (width - 2.4f);
            center.y = river.GetWaterHeight(center);
            var jam = new GameObject("Logjam").transform;
            jam.SetParent(parent, false);
            jam.position = center;
            for (int i = 0; i < 6; i++)
            {
                float yaw = Range(rng, -70f, 70f);
                Vector3 axis = Quaternion.AngleAxis(yaw, Vector3.up) * s.right;
                float len = Range(rng, 3.5f, 6f);
                Vector3 mid = center + s.direction * Range(rng, -1.5f, 1.5f) + s.right * side * Range(rng, -0.5f, 1.5f) + Vector3.up * (i * 0.18f - 0.15f);
                var log = LogPiece(jam, "Log", mid - axis * len * 0.5f, mid + axis * len * 0.5f, Range(rng, 0.2f, 0.32f));
                if (i < 3)
                {
                    var col = log.AddComponent<CapsuleCollider>();
                    col.direction = 1; col.radius = 0.3f; col.height = len; col.center = new Vector3(0f, len * 0.5f, 0f);
                    log.layer = obstacleLayer;
                }
            }
            var flow = jam.gameObject.AddComponent<FlowObstacle>();
            flow.radius = 2.2f; flow.eddyLength = 3.5f;
            flow.Refresh();
        }

        /// <summary>A log from a to b: the object sits at a, its +Y along the log (as the old trunk meshes).</summary>
        static GameObject LogPiece(Transform parent, string name, Vector3 a, Vector3 b, float radius)
        {
            float length = Vector3.Distance(a, b);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(a, Quaternion.FromToRotation(Vector3.up, b - a));
            go.layer = environmentLayer;
            KayakPrefabBuilder.Primitive(PrimitiveType.Cylinder, "Visual", go.transform, new Vector3(0f, length * 0.5f, 0f), new Vector3(radius * 2f, length * 0.5f, radius * 2f), wood);
            return go;
        }

        static void ShallowRocks(RiverPath river, Transform parent, System.Random rng)
        {
            var group = new GameObject("Shallow Rocks").transform;
            group.SetParent(parent, false);
            for (int i = 0; i < 18; i++)
            {
                float d = Range(rng, 110f, river.Length - 120f);
                var s = river.GetPointAtDistance(d);
                int side = rng.NextDouble() < 0.5 ? -1 : 1;
                float width = side > 0 ? s.rightWidth : s.leftWidth;
                Vector3 p = s.point + s.right * side * (width - Range(rng, 0.8f, 2.4f));
                bool emerging = rng.NextDouble() < 0.5;
                float size = Range(rng, 0.7f, 1.4f);
                float variantScale = RockVariantScales[rng.Next(RockVariantScales.Length)];
                p.y = river.GetWaterHeight(p) - size * 0.35f + (emerging ? 0.15f : -0.28f);
                Boulder(group, p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), new Vector3(1f, 0.7f, 1f) * (size / variantScale), emerging);
            }
        }

        // ------------------------------------------------------------------ water, kayak, light

        static void BuildWaterMesh(RiverPath river)
        {
            var go = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer), typeof(RiverMeshBuilder));
            go.transform.SetParent(river.transform, false);
            go.layer = LayerMask.NameToLayer("Water");
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var builder = go.GetComponent<RiverMeshBuilder>();
            builder.river = river;
            builder.Rebuild();
        }

        static void PlaceKayak(RiverPath river)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KayakPrefabBuilder.KayakPath);
            var kayak = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var s = river.GetPointAtDistance(KayakStart);
            kayak.transform.SetPositionAndRotation(s.point, Quaternion.LookRotation(s.direction));
            var spawner = new GameObject("Kayak Bots (F3)").AddComponent<KayakBotSpawner>();
            spawner.prefab = prefab.GetComponent<KayakController>();
            var cam = Object.FindAnyObjectByType<KayakCamera>();
            cam.target = kayak.GetComponent<KayakController>();
        }

        static void BuildLightAndCamera()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.74f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.26f);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.farClipPlane = 1500f;
            camGo.AddComponent<KayakCamera>();
        }

        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
    }
}
