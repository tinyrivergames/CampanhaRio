using System.IO;
using CampanhaRio.Campaign;
using CampanhaRio.Kayak;
using CampanhaRio.Net;
using CampanhaRio.Rendering;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// The campaign skeleton (graybox, no art):
    ///   Scenes/Core/Core.unity           persistent: bootstrap (network + save), streamer, player spawner, light (DayCycle),
    ///                                    post, camera + debug panel, and empty roots for the van, audio and UI.
    ///   Scenes/Segments/Test_A/B/C       three 120 m stretches along +Z (plain shapes), each with its Segment boxes.
    ///   Prefabs/Player/Player_Net        the graybox player body (NetworkObject + owner NetworkTransform + NetworkPlayer).
    ///   Settings/Resources/NetConfig     the networked prefabs and connection settings.
    /// Also adds the RiverChallenge to KayakTest and sets the build scene list (Core first).
    /// Menu: CampanhaRio > Setup > Build Campaign Skeleton. Batch: CampanhaRio.Editor.CampaignBuilder.Build
    /// </summary>
    public static class CampaignBuilder
    {
        const string CorePath = "Assets/_Project/Scenes/Core/Core.unity";
        const string SegmentDir = "Assets/_Project/Scenes/Segments";
        const string PlayerPath = "Assets/_Project/Prefabs/Player/Player_Net.prefab";
        const string NetConfigPath = "Assets/_Project/Settings/Resources/NetConfig.asset";
        public static readonly string[] Segments = { "Test_A", "Test_B", "Test_C" };
        const float SegmentLength = 120f;

        [MenuItem("CampanhaRio/Setup/Build Campaign Skeleton")]
        public static void Build()
        {
            var player = BuildPlayerPrefab();
            BuildNetConfig(player);
            for (int i = 0; i < Segments.Length; i++) BuildSegment(i);
            BuildCore();
            AddRiverChallenge();
            AssetDatabase.SaveAssets();
            Debug.Log("[Campanha] Campaign skeleton built.");
        }

        static NetworkObject BuildPlayerPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPath));
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player_Net";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = GrayboxMaterials.Get("Player", new Color(0.31f, 0.49f, 0.76f));
            var nose = KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Facing", go.transform, new Vector3(0f, 0.4f, 0.45f), new Vector3(0.25f, 0.15f, 0.2f),
                GrayboxMaterials.Get("Blade", new Color(0.95f, 0.66f, 0.23f)));
            _ = nose;
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            go.AddComponent<NetworkPlayer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PlayerPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<NetworkObject>();
        }

        static void BuildNetConfig(NetworkObject player)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(NetConfigPath));
            var config = AssetDatabase.LoadAssetAtPath<NetConfig>(NetConfigPath);
            if (!config) { config = ScriptableObject.CreateInstance<NetConfig>(); AssetDatabase.CreateAsset(config, NetConfigPath); }
            config.playerPrefab = player;
            config.kayakPrefab = AssetDatabase.LoadAssetAtPath<NetworkObject>(KayakPrefabBuilder.KayakNetPath);
            EditorUtility.SetDirty(config);
        }

        /// <summary>A 120 m stretch: ground, a few trees and rocks (plain shapes), the Segment boxes, an entry point.</summary>
        static void BuildSegment(int index)
        {
            string id = Segments[index];
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            float z0 = index * SegmentLength;
            var root = new GameObject(id).transform;
            var tints = new[] { new Color(0.55f, 0.68f, 0.38f), new Color(0.62f, 0.66f, 0.4f), new Color(0.5f, 0.64f, 0.42f) };
            var ground = KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Ground", root, new Vector3(0f, -0.25f, z0 + SegmentLength * 0.5f),
                new Vector3(40f, 0.5f, SegmentLength), GrayboxMaterials.Get("Segment" + (char)('A' + index), tints[index]));
            ground.gameObject.AddComponent<BoxCollider>();
            ground.gameObject.layer = LayerMask.NameToLayer("Environment");
            KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Road", root, new Vector3(0f, 0.01f, z0 + SegmentLength * 0.5f),
                new Vector3(4f, 0.02f, SegmentLength), GrayboxMaterials.Get("Dirt", new Color(0.72f, 0.56f, 0.38f)));
            var rng = new System.Random(10 + index);
            var pine = GrayboxMaterials.Get("Pine", new Color(0.25f, 0.45f, 0.28f));
            var rock = GrayboxMaterials.Get("Rock", new Color(0.78f, 0.55f, 0.36f));
            for (int i = 0; i < 26; i++)
            {
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                var p = new Vector3(side * (5f + (float)rng.NextDouble() * 13f), 0f, z0 + (float)rng.NextDouble() * SegmentLength);
                if (i % 4 == 3) KayakPrefabBuilder.Primitive(PrimitiveType.Sphere, "Rock", root, p, new Vector3(2f, 1.2f, 1.6f), rock);
                else
                {
                    float h = 4f + (float)rng.NextDouble() * 4f;
                    KayakPrefabBuilder.Primitive(PrimitiveType.Cylinder, "Trunk", root, p + Vector3.up * 1f, new Vector3(0.3f, 1f, 0.3f), GrayboxMaterials.Get("Wood", new Color(0.45f, 0.32f, 0.22f)));
                    var crown = KayakPrefabBuilder.Primitive(PrimitiveType.Capsule, "Pine", root, p + Vector3.up * (1.5f + h * 0.5f), new Vector3(2.2f, h * 0.5f, 2.2f), pine);
                    _ = crown;
                }
            }
            // A gate marking the segment boundary, so the transitions are visible in captures
            KayakPrefabBuilder.Primitive(PrimitiveType.Cube, "Gate", root, new Vector3(0f, 2.5f, z0 + 0.5f), new Vector3(6f, 0.4f, 0.4f), GrayboxMaterials.Get("Blade", new Color(0.95f, 0.66f, 0.23f)));

            var segment = root.gameObject.AddComponent<Segment>();
            segment.id = id;
            segment.area = new Bounds(new Vector3(0f, 0f, z0 + SegmentLength * 0.5f), new Vector3(40f, 20f, SegmentLength));
            segment.loadNextZone = new Bounds(new Vector3(0f, 0f, z0 + SegmentLength - 25f), new Vector3(40f, 20f, 50f));
            var entry = new GameObject("Entry").transform;
            entry.SetParent(root, false);
            entry.position = new Vector3(0f, 1f, z0 + 6f);
            segment.entry = entry;

            Directory.CreateDirectory(SegmentDir);
            EditorSceneManager.SaveScene(scene, $"{SegmentDir}/{id}.unity");
        }

        static void BuildCore()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rig = LookDevRig.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(LookDevBuilder.RigPath).text);

            var game = new GameObject("Core");
            var streamer = game.AddComponent<SegmentStreamer>();
            streamer.order = Segments;
            game.AddComponent<PlayerSpawner>();
            game.AddComponent<CoreBootstrap>().streamer = streamer;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            var day = new GameObject("Day Cycle").AddComponent<DayCycle>();
            day.sun = sun;
            day.skybox = LookDevBuilder.SkyMaterial(rig);
            day.keys = DayCycle.DefaultKeys();
            day.keys[0] = DayCycle.FromRig(rig, day.keys[0]);
            day.Apply();
            LookDevBuilder.PostVolume(rig);

            var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.farClipPlane = 800f;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 6f, -6f), Quaternion.Euler(20f, 0f, 0f));
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            cam.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.gameObject.AddComponent<CoreView>();

            // Roots for what Core will hold (filled in later phases)
            new GameObject("Van (placeholder)");
            new GameObject("Audio");
            new GameObject("UI");

            Directory.CreateDirectory(Path.GetDirectoryName(CorePath));
            EditorSceneManager.SaveScene(scene, CorePath);
        }

        static void AddRiverChallenge()
        {
            var scene = EditorSceneManager.OpenScene(KayakTestBuilder.ScenePath);
            var challenge = Object.FindAnyObjectByType<RiverChallenge>();
            if (!challenge) challenge = new GameObject("River Challenge").AddComponent<RiverChallenge>();
            challenge.river = Object.FindAnyObjectByType<River.RiverPath>();
            challenge.day = Object.FindAnyObjectByType<DayCycle>();
            challenge.riverId = "Rio_Teste_River01";
            challenge.startAlong = 14f;
            challenge.finishAlong = challenge.river.Length - 20f;
            challenge.timeLimit = 420f;
            EditorUtility.SetDirty(challenge);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
