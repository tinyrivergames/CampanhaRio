using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampanhaRio.Dev
{
    /// <summary>-cc-scene &lt;name&gt;: open that scene right after the first one loads (test builds start anywhere).</summary>
    static class DevSceneSwitch
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Switch()
        {
            string scene = TestSwitches.Scene;
            if (string.IsNullOrEmpty(scene) || SceneManager.GetActiveScene().name == scene) return;
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else Debug.LogError($"[Campanha] -cc-scene {scene}: not in the build.");
        }
    }
}
