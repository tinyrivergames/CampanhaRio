using System;
using CampanhaRio.Dev;
using CampanhaRio.Kayak;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// CampanhaRio > Feel Benchmark: plays Scenes/Dev/KayakTest (the River_01 graybox) and runs <see cref="FeelBenchmark"/> (as fast as the machine allows:
    /// one physics step per frame, cameras off). Writes Docs/Benchmarks/&lt;timestamp&gt;_&lt;label&gt;.csv + .md.
    /// Batch (editor closed, no -quit, the runner exits the editor itself):
    ///   Unity.exe -batchmode -projectPath "&lt;path&gt;" -executeMethod CampanhaRio.Editor.FeelBenchmarkRunner.RunBatch
    /// Environment: CR_BENCH_LABEL (file suffix, default "baseline"), CR_BENCH_AUTOPILOT=0 to skip the full run.
    /// </summary>
    [InitializeOnLoad]
    public static class FeelBenchmarkRunner
    {
        const string ScenePath = "Assets/_Project/Scenes/Dev/KayakTest.unity";
        const string ActiveKey = "CampanhaRio.FeelBenchmark.Active";
        const string LabelKey = "CampanhaRio.FeelBenchmark.Label";
        const string AutopilotKey = "CampanhaRio.FeelBenchmark.Autopilot";

        static FeelBenchmarkRunner() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        [MenuItem("CampanhaRio/Feel Benchmark")]
        static void RunFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Start("manual", true);
        }

        public static void RunBatch()
        {
            string label = Environment.GetEnvironmentVariable("CR_BENCH_LABEL");
            Start(string.IsNullOrEmpty(label) ? "baseline" : label, Environment.GetEnvironmentVariable("CR_BENCH_AUTOPILOT") != "0");
        }

        static void Start(string label, bool autopilot)
        {
            // Always reopen: a scene already open at startup was loaded before the scripts recompiled and keeps the
            // old defaults of any new serialized field until it is loaded again
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetString(LabelKey, label);
            SessionState.SetBool(AutopilotKey, autopilot);
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                var kayak = KayakRegistry.Local ? KayakRegistry.Local : UnityEngine.Object.FindAnyObjectByType<KayakController>();
                if (!kayak) { Debug.LogError("[Campanha] Feel benchmark: no kayak in the scene."); EditorApplication.ExitPlaymode(); return; }
                if (!kayak.GetComponent<KayakAutopilot>()) kayak.gameObject.AddComponent<KayakAutopilot>();
                // Exactly one physics step per frame, as fast as possible; nothing needs to be rendered
                Time.captureFramerate = Mathf.RoundToInt(1f / Time.fixedDeltaTime);
                foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>()) cam.enabled = false;

                var bench = new GameObject("Feel Benchmark").AddComponent<FeelBenchmark>();
                bench.kayak = kayak;
                bench.label = SessionState.GetString(LabelKey, "baseline");
                bench.autopilotRun = SessionState.GetBool(AutopilotKey, true);
                bench.OnFinished += () => EditorApplication.delayCall += EditorApplication.ExitPlaymode;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.EraseBool(ActiveKey);
                Time.captureFramerate = 0;
                AssetDatabase.Refresh();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
        }
    }
}
