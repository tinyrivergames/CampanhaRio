using System.Collections;
using System.IO;
using CampanhaRio.Kayak;
using CampanhaRio.Rendering;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-shots &lt;dir&gt; in a build: screenshots of the running game (release build, real graphics), then quit.
    /// The LookDev scene does its own captures (LookDevController); any other scene gets three 1920x1080 shots from the
    /// main camera at 4, 10 and 18 s (with -cc-bot the local kayak's autopilot paddles meanwhile).
    /// </summary>
    public class ShotCapture : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (string.IsNullOrEmpty(TestSwitches.ShotsFolder)) return;
            SceneManager.sceneLoaded += (scene, mode) => Consider(scene);
            Consider(SceneManager.GetActiveScene());
        }

        /// <summary>Captures in the scene asked for with -cc-scene (or the first one), unless it has its own (LookDev).</summary>
        static void Consider(Scene scene)
        {
            if (!IsTarget(scene) || FindAnyObjectByType<ShotCapture>() || FindAnyObjectByType<LookDevController>()) return;
            DontDestroyOnLoad(new GameObject("Shot Capture").AddComponent<ShotCapture>().gameObject);
        }

        public static bool IsTarget(Scene scene) => string.IsNullOrEmpty(TestSwitches.Scene) || scene.name == TestSwitches.Scene;

        IEnumerator Start()
        {
            string folder = TestSwitches.ShotsFolder;
            Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(1f);
            if (TestSwitches.LocalBotProfile.HasValue && KayakRegistry.Local)
            {
                var auto = KayakRegistry.Local.GetComponent<KayakAutopilot>();
                if (auto) { auto.profile = TestSwitches.LocalBotProfile.Value; auto.SetActive(true); }
            }
            float start = Time.time;
            foreach (float at in new[] { 4f, 10f, 18f })
            {
                while (Time.time - start < at) yield return null;
                yield return new WaitForEndOfFrame();
                var cam = Camera.main;
                if (!cam) continue;
                var tex = LookDevCapture.Render(cam, 1920, 1080);
                string path = Path.Combine(folder, $"{SceneManager.GetActiveScene().name}_{at:00}s.png");
                LookDevCapture.Save(tex, path);
                Destroy(tex);
                Debug.Log($"[Campanha] shot {path} (fps {1f / Time.smoothDeltaTime:0})");
            }
            Application.Quit();
        }
    }
}
