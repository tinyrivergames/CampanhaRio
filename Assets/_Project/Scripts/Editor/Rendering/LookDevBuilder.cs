using System.Collections.Generic;
using System.IO;
using CampanhaRio.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// The look's setup and the LookDev scene:
    ///   - URP: MSAA 4x, soft shadows to 100 m, HDR; the default post-processing profile (Neutral tonemapping, gentle colour
    ///     grading, subtle bloom, a light vignette); the sky material.
    ///   - Scenes/Dev/LookDev.unity: the DayCycle (key 0 = lookdev_rig.json), the sky, a neutral 40 m ground, a turntable
    ///     with the subjects (MatchTest: a sphere and a cube with the same SoftToon material as the Blender match test;
    ///     the Pebble once exported), the 0.9 m animal scale reference, a capture camera and the scene camera.
    /// Menu: CampanhaRio > LookDev > Build Scene / Capture. Batch: CampanhaRio.Editor.LookDevBuilder.Build / CaptureBatch
    /// </summary>
    public static class LookDevBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Dev/LookDev.unity";
        public const string RigPath = "Assets/_Project/Art/LookDev/lookdev_rig.json";
        const string SettingsDir = "Assets/_Project/Settings";
        const string ProfilePath = SettingsDir + "/PostProcess_Default.asset";
        const string SkyPath = "Assets/_Project/Art/Materials/M_Sky.mat";
        const string MatchMatPath = "Assets/_Project/Art/Materials/LookDev/M_MatchTest.mat";
        const string GroundMatPath = "Assets/_Project/Art/Materials/LookDev/M_LookDevGround.mat";
        const string ScaleRefMatPath = "Assets/_Project/Art/Materials/LookDev/M_ScaleRef.mat";
        public const string PebblePath = "Assets/_Project/Art/Models/Test/Pebble.fbx";

        [MenuItem("CampanhaRio/LookDev/Build Scene")]
        public static void Build()
        {
            SetupPipeline();
            var rigAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(RigPath);
            var rig = LookDevRig.Parse(rigAsset.text);
            SetupContactAO(rig);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            var day = new GameObject("Day Cycle").AddComponent<DayCycle>();
            day.sun = sun;
            day.skybox = SkyMaterial(rig);
            day.keys = DayCycle.DefaultKeys();
            day.keys[0] = DayCycle.FromRig(rig, day.keys[0]);
            day.Apply();
            PostVolume(rig);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = Vector3.one * (rig.ground.size / 10f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = SoftToon(GroundMatPath, LookDevRig.Hex(rig.ground.color), m => m.SetFloat("_RimStrength", 0f));

            var turntable = new GameObject("Turntable").AddComponent<Turntable>();
            var subjects = new List<Transform> { MatchTest(turntable.transform) };
            // Every exported model is a subject (Art/Models/<Family>/<Asset>.fbx)
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Project/Art/Models" }))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var p = (GameObject)PrefabUtility.InstantiatePrefab(model, turntable.transform);
                p.name = model.name;
                subjects.Add(p.transform);
            }
            var scaleRef = ScaleReference(rig);

            var captureCam = new GameObject("Capture Camera").AddComponent<Camera>();
            captureCam.enabled = false;
            captureCam.clearFlags = CameraClearFlags.Skybox;
            captureCam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var viewCam = new GameObject("Main Camera").AddComponent<Camera>();
            viewCam.tag = "MainCamera";
            viewCam.gameObject.AddComponent<AudioListener>();
            viewCam.fieldOfView = 40f;
            viewCam.transform.SetPositionAndRotation(new Vector3(2.4f, 1.5f, 3.6f), Quaternion.Euler(16f, 213f, 0f));
            viewCam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            viewCam.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var controller = new GameObject("LookDev").AddComponent<LookDevController>();
            controller.rigJson = rigAsset;
            controller.day = day;
            controller.turntable = turntable;
            controller.subjects = subjects.ToArray();
            controller.captureCamera = captureCam;
            controller.viewCamera = viewCam;
            controller.scaleReference = scaleRef;
            controller.Show(subjects.Count - 1);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Campanha] LookDev built with {subjects.Count} subject(s).");
        }

        [MenuItem("CampanhaRio/LookDev/Capture (open LookDev scene)")]
        static void CaptureMenu()
        {
            var controller = Object.FindAnyObjectByType<LookDevController>();
            if (!controller) { EditorUtility.DisplayDialog("LookDev", "Open Scenes/Dev/LookDev first.", "OK"); return; }
            Capture(controller, "lookdev");
        }

        /// <summary>Batch: open LookDev and capture every subject and time of day (needs graphics: no -nographics).</summary>
        public static void CaptureBatch()
        {
            EditorSceneManager.OpenScene(ScenePath);
            string label = System.Environment.GetEnvironmentVariable("CR_SHOT_LABEL");
            Capture(Object.FindAnyObjectByType<LookDevController>(), string.IsNullOrEmpty(label) ? "lookdev" : label);
        }

        static void Capture(LookDevController controller, string label)
        {
            string folder = Path.Combine("Assets/_Project/Docs/Screenshots", System.DateTime.Now.ToString("yyyy-MM-dd_HHmm") + "_" + label);
            var files = controller.CaptureAll(folder);
            AssetDatabase.Refresh();
            Debug.Log($"[Campanha] LookDev captured {files.Count} images into {folder}");
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>A sphere and a cube with one SoftToon material: the Blender/Unity match test (ArtSource/Test/match_test.py).</summary>
        static Transform MatchTest(Transform parent)
        {
            var root = new GameObject("MatchTest").transform;
            root.SetParent(parent, false);
            var mat = SoftToon(MatchMatPath, LookDevRig.Hex("#D08A55"), null);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Sphere";
            sphere.transform.SetParent(root, false);
            sphere.transform.localPosition = new Vector3(-0.7f, 0.5f, 0f);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Cube";
            cube.transform.SetParent(root, false);
            cube.transform.localPosition = new Vector3(0.7f, 0.4f, 0f);
            cube.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            cube.transform.localScale = Vector3.one * 0.8f;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) { r.sharedMaterial = mat; Object.DestroyImmediate(r.GetComponent<Collider>()); }
            return root;
        }

        /// <summary>The same 0.9 m rounded animal as the Blender rig (lookdev.py scale_reference), beside the turntable.</summary>
        static GameObject ScaleReference(LookDevRig rig)
        {
            var root = new GameObject("Scale Reference (0.9 m)").transform;
            root.position = new Vector3(-1.9f, 0f, 0.6f);
            float s = rig.scaleRef.height / 0.9f;
            var mat = SoftToon(ScaleRefMatPath, LookDevRig.Hex(rig.scaleRef.color), m => { m.SetFloat("_Wrap", 0.55f); m.SetFloat("_RampSoftness", 0.22f); m.SetFloat("_RimStrength", 0.22f); });
            // Blender (x, y, z) -> Unity (-x, z, -y); sphere radius 1 in Blender = scale 2 in Unity's 0.5 m sphere
            void Blob(string name, Vector3 b, Vector3 r)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(-b.x, b.z, -b.y) * s;
                go.transform.localScale = new Vector3(r.x, r.z, r.y) * 2f * s;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            Blob("Body", new Vector3(0, 0, 0.30f), new Vector3(0.22f, 0.2f, 0.30f));
            Blob("Head", new Vector3(0, -0.03f, 0.70f), new Vector3(0.17f, 0.16f, 0.16f));
            Blob("Snout", new Vector3(0, -0.17f, 0.66f), new Vector3(0.07f, 0.06f, 0.05f));
            Blob("EarL", new Vector3(-0.09f, -0.01f, 0.85f), new Vector3(0.04f, 0.03f, 0.07f));
            Blob("EarR", new Vector3(0.09f, -0.01f, 0.85f), new Vector3(0.04f, 0.03f, 0.07f));
            return root.gameObject;
        }

        public static Material SoftToon(string path, Color baseColor, System.Action<Material> tweak)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("CampanhaRio/SoftToon")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("CampanhaRio/SoftToon");
            mat.SetColor("_BaseColor", baseColor);
            mat.enableInstancing = true;
            tweak?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material SkyMaterial(LookDevRig rig)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (!mat) { mat = new Material(Shader.Find("CampanhaRio/SkyGradient")); AssetDatabase.CreateAsset(mat, SkyPath); }
            mat.SetFloat("_HorizonSharpness", rig.sky.horizonSharpness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The global post-processing volume with the default profile.</summary>
        public static Volume PostVolume(LookDevRig rig)
        {
            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile(rig);
            return volume;
        }

        /// <summary>The default profile; the grading numbers come from lookdev_rig.json (the Blender sheet applies the same).</summary>
        public static VolumeProfile PostProfile(LookDevRig rig)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile)
            {
                // Rebuilt in place (scenes keep their reference to the asset)
                foreach (var old in profile.components.ToArray()) Object.DestroyImmediate(old, true);
                profile.components.Clear();
            }
            else
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(rig.grading.saturation);
            color.contrast.Override(rig.grading.contrast);
            color.postExposure.Override(rig.grading.postExposure);
            var wb = profile.Add<WhiteBalance>(true);
            wb.temperature.Override(rig.grading.temperature);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.25f);
            bloom.scatter.Override(0.6f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.14f);
            vignette.smoothness.Override(0.5f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>URP quality: MSAA 4x, soft shadows out to 100 m (4 cascades), HDR, LOD cross-fade.</summary>
        public static void SetupPipeline()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SettingsDir + "/URP_Main.asset");
            rp.msaaSampleCount = 4;
            rp.shadowDistance = 100f;
            rp.shadowCascadeCount = 4;
            rp.supportsHDR = true;
            EditorUtility.SetDirty(rp);
        }

        /// <summary>
        /// Contact darkening: URP's SSAO on the renderer, soft and small (lookdev_rig.json "contactAO"), so objects sit on
        /// the ground at any time of day, even at dusk when there is no sun shadow. SoftToon applies it to the ambient
        /// (and a little to the sunlight). Its settings are internal to URP, so they are written through the serialized object.
        /// </summary>
        public static void SetupContactAO(LookDevRig rig)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsDir + "/URP_Renderer.asset");
            ScreenSpaceAmbientOcclusion ssao = null;
            foreach (var f in data.rendererFeatures) if (f is ScreenSpaceAmbientOcclusion s) ssao = s;
            if (!ssao)
            {
                ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ssao.name = "Contact AO (SSAO)";
                AssetDatabase.AddObjectToAsset(ssao, data);
                data.rendererFeatures.Add(ssao);
            }
            var so = new SerializedObject(ssao);
            so.FindProperty("m_Settings.Intensity").floatValue = rig.contactAO.intensity;
            so.FindProperty("m_Settings.Radius").floatValue = rig.contactAO.radius;
            so.FindProperty("m_Settings.DirectLightingStrength").floatValue = rig.contactAO.directStrength;
            so.FindProperty("m_Settings.Falloff").floatValue = 60f;
            so.FindProperty("m_Settings.AOMethod").enumValueIndex = 1;   // Interleaved Gradient: smooth, no blue-noise grain
            so.FindProperty("m_Settings.Samples").enumValueIndex = 0;    // High (12 samples)
            so.FindProperty("m_Settings.NormalSamples").enumValueIndex = 2; // High
            so.FindProperty("m_Settings.BlurQuality").enumValueIndex = 0; // High (bilateral)
            so.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
        }
    }
}
