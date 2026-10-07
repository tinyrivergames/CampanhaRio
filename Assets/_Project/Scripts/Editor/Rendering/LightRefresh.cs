using CampanhaRio.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// After a change to the light (lookdev_rig.json or DayCycle.DefaultKeys): rewrites the post-processing profile (shared
    /// by every scene) and the day keys and sky of Core, KayakTest and LookDev, without rebuilding the scenes.
    /// Menu: CampanhaRio > LookDev > Refresh the Light. Batch: CampanhaRio.Editor.LightRefresh.Run
    /// </summary>
    public static class LightRefresh
    {
        [MenuItem("CampanhaRio/LookDev/Refresh the Light")]
        public static void Run()
        {
            var rig = LookDevRig.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(LookDevBuilder.RigPath).text);
            LookDevBuilder.PostProfile(rig);
            LookDevBuilder.SkyMaterial(rig);
            foreach (string path in new[] { "Assets/_Project/Scenes/Core/Core.unity", KayakTestBuilder.ScenePath, LookDevBuilder.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                var day = Object.FindAnyObjectByType<DayCycle>();
                if (day)
                {
                    day.keys = DayCycle.DefaultKeys();
                    day.keys[0] = DayCycle.FromRig(rig, day.keys[0]);
                    day.Apply();
                    EditorUtility.SetDirty(day);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Campanha] Light refreshed (post profile, sky, day keys).");
        }
    }
}
