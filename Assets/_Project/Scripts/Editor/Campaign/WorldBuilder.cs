using System.IO;
using CampanhaRio.Campaign;
using CampanhaRio.Jobs;
using CampanhaRio.River;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// The vertical slice's places as GRAYBOX segments (Phase 2, part C), laid out around the KayakTest river (which
    /// becomes the Rio do Moinho), in travel order:
    ///   Agencia        z -400..-260  the agency (Grandma Nina's old shed), the orders board, the van's stop
    ///   Estrada_Vale   z -260..-120  a short valley road down to the river
    ///   Rio_Moinho     z -120..1230  the KayakTest river (its rapids, eddies, ledge, forest and rocks), the put-in beach,
    ///                                the finish at Vila do Moinho (houses, dock, the arrival stop, Seu Alce's spot)
    /// Markers (<see cref="WorldMarker"/>) tell the flow where things are. Core gets the new segment order.
    /// Menu: CampanhaRio > Setup > Build Vertical Slice World. Batch: CampanhaRio.Editor.WorldBuilder.Build
    /// </summary>
    public static class WorldBuilder
    {
        const string SegmentDir = "Assets/_Project/Scenes/Segments";
        public static readonly string[] Order = { "Agencia", "Estrada_Vale", "Rio_Moinho" };
        const float Ground = 37f; // the land level upstream of the river (its water starts at 36 m)

        [MenuItem("CampanhaRio/Setup/Build Vertical Slice World")]
        public static void Build()
        {
            BuildAgencia();
            BuildEstrada();
            BuildRioMoinho();
            PatchCore();
            Debug.Log("[Campanha] Vertical slice world built: " + string.Join(", ", Order));
        }

        // ---------------------------------------------------------------- Agencia
        static void BuildAgencia()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Agencia").transform;
            var grass = GrayboxMaterials.Get("Grass", new Color(0.42f, 0.56f, 0.3f));
            var wood = GrayboxMaterials.Get("Wood", new Color(0.45f, 0.32f, 0.22f));
            var plank = GrayboxMaterials.Get("Plank", new Color(0.62f, 0.48f, 0.33f));
            var roof = GrayboxMaterials.Get("Roof", new Color(0.55f, 0.22f, 0.18f));
            var paper = GrayboxMaterials.Get("Paper", new Color(0.93f, 0.9f, 0.8f));

            Solid(root, "Ground", new Vector3(0f, Ground - 0.5f, -330f), new Vector3(200f, 1f, 140f), grass);
            // Grandma Nina's old shed: walls, a roof, a big open door toward the yard
            var shed = new GameObject("Galpao").transform;
            shed.SetParent(root, false);
            Solid(shed, "Back", new Vector3(-18f, Ground + 3f, -352f), new Vector3(16f, 6f, 0.4f), plank);
            Solid(shed, "Left", new Vector3(-26f, Ground + 3f, -346f), new Vector3(0.4f, 6f, 12f), plank);
            Solid(shed, "Right", new Vector3(-10f, Ground + 3f, -346f), new Vector3(0.4f, 6f, 12f), plank);
            Solid(shed, "FrontL", new Vector3(-23.5f, Ground + 3f, -340f), new Vector3(5f, 6f, 0.4f), plank);
            Solid(shed, "FrontR", new Vector3(-12.5f, Ground + 3f, -340f), new Vector3(5f, 6f, 0.4f), plank);
            KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Roof", shed, new Vector3(-18f, Ground + 6.3f, -346f), new Vector3(17f, 0.5f, 13f), roof);
            KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Sign", shed, new Vector3(-18f, Ground + 5f, -339.7f), new Vector3(7f, 1.2f, 0.2f), paper);
            // Kayaks resting on a rack beside the shed
            for (int i = 0; i < 4; i++)
                KayakPrefabBuilder.Primitive(PrimitiveType.Capsule, "RackKayak", shed, new Vector3(-31f, Ground + 0.6f + i * 0.5f, -346f), new Vector3(0.6f, 1.6f, 0.3f),
                    GrayboxMaterials.Get("PaddleBlade", new Color(0.4f, 0.08f, 0.13f))).localRotation = Quaternion.Euler(90f, 0f, 0f);

            // The orders board in the yard: two posts and the board, facing the yard
            var board = new GameObject("QuadroDePedidos").transform;
            board.SetParent(root, false);
            Solid(board, "PostL", new Vector3(4.2f, Ground + 1.1f, -330f), new Vector3(0.2f, 2.2f, 0.2f), wood);
            Solid(board, "PostR", new Vector3(7.8f, Ground + 1.1f, -330f), new Vector3(0.2f, 2.2f, 0.2f), wood);
            KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Board", board, new Vector3(6f, Ground + 1.6f, -330f), new Vector3(3.8f, 1.6f, 0.15f), plank);
            for (int i = 0; i < 3; i++)
                KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Order", board, new Vector3(4.9f + i * 1.1f, Ground + 1.7f, -330.1f), new Vector3(0.7f, 0.9f, 0.05f), paper);
            Marker(root, WorldMarker.Kind.JobBoard, new Vector3(6f, Ground, -331.6f), 0f);

            // The van's stop at the yard's gate (facing the road)
            Solid(root, "Gravel", new Vector3(0f, Ground - 0.45f, -290f), new Vector3(10f, 1f, 50f), GrayboxMaterials.Get("Gravel", new Color(0.62f, 0.58f, 0.5f)));
            Marker(root, WorldMarker.Kind.VanStopAgency, new Vector3(0f, Ground, -285f), 0f);
            Trees(root, new Rect(-100f, -400f, 200f, 140f), 60, 11, new Rect(-40f, -360f, 60f, 100f));

            var entry = Marker(root, WorldMarker.Kind.Entry, new Vector3(-6f, Ground + 1f, -326f), 0f);
            Finish(scene, root, "Agencia", new Bounds(new Vector3(0f, 0f, -330f), new Vector3(200f, 200f, 140f)),
                new Bounds(new Vector3(0f, 0f, -272f), new Vector3(200f, 200f, 24f)), entry);
        }

        // ---------------------------------------------------------------- Estrada_Vale
        static void BuildEstrada()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Estrada_Vale").transform;
            var grass = GrayboxMaterials.Get("Grass", new Color(0.42f, 0.56f, 0.3f));
            var road = GrayboxMaterials.Get("Gravel", new Color(0.62f, 0.58f, 0.5f));
            Solid(root, "Ground", new Vector3(0f, Ground - 0.5f, -190f), new Vector3(200f, 1f, 140f), grass);
            Solid(root, "Road", new Vector3(0f, Ground - 0.45f, -190f), new Vector3(8f, 1f, 140f), road);
            // Road posts every 20 m, and a lookout bench halfway (the valley road's "mirante")
            for (float z = -255f; z <= -125f; z += 20f)
                foreach (float x in new[] { -5f, 5f })
                    KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Post", root, new Vector3(x, Ground + 0.5f, z), new Vector3(0.2f, 1f, 0.2f), GrayboxMaterials.Get("Paper", new Color(0.93f, 0.9f, 0.8f)));
            Solid(root, "Mirante", new Vector3(14f, Ground + 0.25f, -195f), new Vector3(6f, 0.5f, 3f), GrayboxMaterials.Get("Plank", new Color(0.62f, 0.48f, 0.33f)));
            Trees(root, new Rect(-100f, -260f, 200f, 140f), 70, 23, new Rect(-9f, -260f, 18f, 140f));
            var entry = Marker(root, WorldMarker.Kind.Entry, new Vector3(0f, Ground + 1f, -250f), 0f);
            Finish(scene, root, "Estrada_Vale", new Bounds(new Vector3(0f, 0f, -190f), new Vector3(200f, 200f, 140f)),
                new Bounds(new Vector3(0f, 0f, -150f), new Vector3(200f, 200f, 60f)), entry);
        }

        // ---------------------------------------------------------------- Rio_Moinho
        static void BuildRioMoinho()
        {
            // The KayakTest river and everything on it, without the dev scene's camera, light, kayak and bots
            var scene = EditorSceneManager.OpenScene(KayakTestBuilder.ScenePath);
            foreach (string dev in new[] { "Main Camera", "Sun", "Day Cycle", "Post Processing", "Kayak", "Kayak Bots (F3)" })
            {
                var go = GameObject.Find(dev);
                if (go) Object.DestroyImmediate(go);
            }
            var river = Object.FindAnyObjectByType<RiverPath>();
            var terrain = Object.FindAnyObjectByType<Terrain>();
            var root = new GameObject("Rio_Moinho").transform;

            var challenge = Object.FindAnyObjectByType<RiverChallenge>();
            challenge.riverId = "Rio_Moinho";
            challenge.startOnPlay = false; // a job starts it
            challenge.startAlong = 14f;
            challenge.finishAlong = river.Length - 45f;
            challenge.day = null; // Core's
            if (!challenge.GetComponent<JobRun>()) challenge.gameObject.AddComponent<JobRun>();
            EditorUtility.SetDirty(challenge);

            float H(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;
            // The put-in: a gravel beach on the left bank at the start, where the van stops
            var startPoint = river.GetPointAtDistance(challenge.startAlong);
            var beach = startPoint.point - startPoint.right * (startPoint.leftWidth + 9f);
            beach.y = H(beach);
            Solid(root, "PutIn", beach + Vector3.down * 0.4f, new Vector3(12f, 1f, 18f), GrayboxMaterials.Get("Gravel", new Color(0.62f, 0.58f, 0.5f)));
            Marker(root, WorldMarker.Kind.VanStopPutIn, beach + Vector3.up * 0.1f, Yaw(startPoint.direction));
            // The road from the valley comes down to the beach (the van follows the ground)
            Marker(root, WorldMarker.Kind.RoadPoint, new Vector3(0f, 0f, -110f), 0f, 0);
            Marker(root, WorldMarker.Kind.RoadPoint, new Vector3(beach.x * 0.4f, 0f, -60f), 0f, 1);
            Marker(root, WorldMarker.Kind.RoadPoint, beach - startPoint.direction * 20f, 0f, 2);

            // The finish: Vila do Moinho on the right bank, a dock, the finish post, the van's arrival stop, Seu Alce
            var finish = river.GetPointAtDistance(challenge.finishAlong);
            var bank = finish.point + finish.right * (finish.rightWidth + 6f);
            bank.y = H(bank);
            var vila = new GameObject("Vila_do_Moinho").transform;
            vila.SetParent(root, false);
            var plank = GrayboxMaterials.Get("Plank", new Color(0.62f, 0.48f, 0.33f));
            var walls = GrayboxMaterials.Get("Paper", new Color(0.93f, 0.9f, 0.8f));
            var roofs = GrayboxMaterials.Get("Roof", new Color(0.55f, 0.22f, 0.18f));
            KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Dock", vila, finish.point + finish.right * (finish.rightWidth - 2f) + Vector3.up * 0.25f, new Vector3(5f, 0.3f, 10f), plank)
                .rotation = Quaternion.LookRotation(finish.direction);
            Solid(vila, "FinishPostL", finish.point + finish.right * finish.rightWidth + Vector3.up * 1.5f, new Vector3(0.4f, 4f, 0.4f), plank);
            Solid(vila, "FinishPostR", finish.point - finish.right * finish.leftWidth + Vector3.up * 1.5f, new Vector3(0.4f, 4f, 0.4f), plank);
            KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "FinishBanner", vila, finish.point + Vector3.up * 3.6f, new Vector3((finish.leftWidth + finish.rightWidth), 0.6f, 0.1f), walls)
                .rotation = Quaternion.LookRotation(finish.direction);
            for (int i = 0; i < 5; i++)
            {
                var hp = bank + finish.right * (8f + (i % 2) * 9f) + finish.direction * (-14f + i * 7f);
                hp.y = H(hp);
                Solid(vila, "House", hp + Vector3.up * 2f, new Vector3(5f, 4f, 5f), walls);
                KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Roof", vila, hp + Vector3.up * 4.4f, new Vector3(5.6f, 0.8f, 5.6f), roofs);
            }
            var arrival = bank + finish.direction * 6f;
            arrival.y = H(arrival);
            Marker(root, WorldMarker.Kind.VanStopArrival, arrival, Yaw(-finish.direction));
            var alce = bank - finish.direction * 4f;
            alce.y = H(alce);
            Marker(root, WorldMarker.Kind.SeuAlce, alce, Yaw(-finish.right));
            Marker(root, WorldMarker.Kind.Landing, finish.point + finish.right * (finish.rightWidth + 2f) + finish.direction * 4f, Yaw(finish.right));

            var entry = Marker(root, WorldMarker.Kind.Entry, beach + Vector3.up * 1f, Yaw(startPoint.direction));
            var t = terrain.transform.position;
            var size = terrain.terrainData.size;
            var area = new Bounds(new Vector3(t.x + size.x / 2f, 0f, -120f + (t.z + size.z + 120f) / 2f), new Vector3(size.x, 400f, t.z + size.z + 120f));
            Finish(scene, root, "Rio_Moinho", area, new Bounds(new Vector3(0f, -9999f, 0f), Vector3.zero), entry, save: false);
            EditorSceneManager.SaveScene(scene, $"{SegmentDir}/Rio_Moinho.unity", true); // a copy: KayakTest stays as it is
        }

        // ---------------------------------------------------------------- Core and the build
        static void PatchCore()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Core/Core.unity");
            var streamer = Object.FindAnyObjectByType<SegmentStreamer>();
            streamer.order = Order;
            EditorUtility.SetDirty(streamer);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- helpers
        static void Finish(UnityEngine.SceneManagement.Scene scene, Transform root, string id, Bounds area, Bounds loadNext, Transform entry, bool save = true)
        {
            var segment = root.gameObject.AddComponent<Segment>();
            segment.id = id;
            segment.area = area;
            segment.loadNextZone = loadNext;
            segment.entry = entry;
            if (!save) return;
            Directory.CreateDirectory(SegmentDir);
            EditorSceneManager.SaveScene(scene, $"{SegmentDir}/{id}.unity");
        }

        static Transform Marker(Transform root, WorldMarker.Kind kind, Vector3 position, float yaw, int index = 0)
        {
            var go = new GameObject($"Marker_{kind}{(kind == WorldMarker.Kind.RoadPoint ? index.ToString() : "")}");
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var m = go.AddComponent<WorldMarker>();
            m.kind = kind;
            m.index = index;
            return go.transform;
        }

        static float Yaw(Vector3 dir) => Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z)).eulerAngles.y;

        static Transform Solid(Transform parent, string name, Vector3 position, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); // keeps its collider: ground, walls, posts
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go.transform;
        }

        /// <summary>Graybox trees (the approved pine) scattered in a rect, away from a clearing.</summary>
        static void Trees(Transform root, Rect area, int count, int seed, Rect clear)
        {
            var pine = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Pine/PinheiroA.fbx");
            var rng = new System.Random(seed);
            var trees = new GameObject("Trees").transform;
            trees.SetParent(root, false);
            for (int i = 0, tries = 0; i < count && tries < count * 20; tries++)
            {
                var p = new Vector3(area.x + (float)rng.NextDouble() * area.width, Ground - 0.1f, area.y + (float)rng.NextDouble() * area.height);
                if (clear.Contains(new Vector2(p.x, p.z))) continue;
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(pine, trees);
                tree.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                tree.transform.localScale = Vector3.one * (0.8f + (float)rng.NextDouble() * 0.5f);
                i++;
            }
        }
    }
}
