using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Release builds for testing (Builds/ is git-ignored). Always verify visuals and multiplayer with these, never with
    /// -nographics runs: release builds can differ from the editor (strip settings, shader variants, timing).
    ///   CampanhaRio > Build > Windows (release): Builds/CampanhaRio/CampanhaRio.exe
    ///   Batch: -executeMethod CampanhaRio.Editor.BuildTools.BuildRelease   (CR_BUILD_OUT overrides the folder)
    /// The scenes are the ones listed in BuildScenes (the first is the boot scene).
    /// </summary>
    public static class BuildTools
    {
        public static readonly string[] BuildScenes =
        {
            "Assets/_Project/Scenes/Core/Core.unity",
            "Assets/_Project/Scenes/Segments/Test_A.unity",
            "Assets/_Project/Scenes/Segments/Test_B.unity",
            "Assets/_Project/Scenes/Segments/Test_C.unity",
            "Assets/_Project/Scenes/Segments/Agencia.unity",
            "Assets/_Project/Scenes/Segments/Estrada_Vale.unity",
            "Assets/_Project/Scenes/Segments/Rio_Moinho.unity",
            "Assets/_Project/Scenes/Dev/KayakTest.unity",
            "Assets/_Project/Scenes/Dev/LookDev.unity",
        };

        [MenuItem("CampanhaRio/Build/Windows (release)")]
        public static void BuildRelease()
        {
            string dir = System.Environment.GetEnvironmentVariable("CR_BUILD_OUT");
            if (string.IsNullOrEmpty(dir)) dir = "Builds/CampanhaRio";
            var scenes = BuildScenes.Where(File.Exists).ToArray();
            EditorBuildSettings.scenes = scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(dir, "CampanhaRio.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            var s = report.summary;
            if (s.result == BuildResult.Succeeded) ClearPipelineCache();
            Debug.Log($"[Campanha] Build {s.result}: {s.outputPath} ({s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0} s)");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>
        /// Deletes this machine's D3D12 pipeline cache of the player (%TEMP%/&lt;company&gt;/&lt;product&gt;/dx12_pso_cache_lib.bin).
        /// After many builds with changing shader render states the cache got corrupted, and the player then crashed in
        /// D3D12Core.dll while saving it on quit (TECH_DECISIONS, 2026-10-06). A fresh cache per build avoids that.
        /// </summary>
        static void ClearPipelineCache()
        {
            string path = Path.Combine(Path.GetTempPath(), PlayerSettings.companyName, PlayerSettings.productName, "dx12_pso_cache_lib.bin");
            if (!File.Exists(path)) return;
            try { File.Delete(path); Debug.Log($"[Campanha] Cleared the D3D12 pipeline cache ({path})."); }
            catch (IOException e) { Debug.LogWarning($"[Campanha] Could not clear the D3D12 pipeline cache (is the game running?): {e.Message}"); }
        }
    }
}
