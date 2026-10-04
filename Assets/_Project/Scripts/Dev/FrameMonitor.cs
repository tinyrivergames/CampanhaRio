using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// Watches frame times for hitches: a 3 s window opens on every scene load/unload on this machine (and on Mark),
    /// and its worst and average frame are logged as "[Frames] ...". The worst frame of the whole run (after a short
    /// warm-up) is logged at quit. A hitch is a frame over <see cref="HitchMs"/>.
    /// </summary>
    public class FrameMonitor : MonoBehaviour
    {
        public const float HitchMs = 50f;
        const float Window = 3f, WarmUp = 5f;

        class Win { public string label; public float start, worst, sum; public int frames; }
        static readonly List<Win> open = new List<Win>();
        static float runWorst; static string runWorstAt = "-";
        static int hitches;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("Frame Monitor");
            DontDestroyOnLoad(go);
            go.AddComponent<FrameMonitor>();
            SceneManager.sceneLoaded += (s, m) => Mark("scene loaded " + s.name);
            SceneManager.sceneUnloaded += s => Mark("scene unloaded " + s.name);
        }

        /// <summary>Opens a measuring window starting now.</summary>
        public static void Mark(string label) => open.Add(new Win { label = label, start = Time.realtimeSinceStartup });

        void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            float now = Time.realtimeSinceStartup;
            if (now > WarmUp)
            {
                if (ms > runWorst) { runWorst = ms; runWorstAt = $"{now:0.0} s"; }
                if (ms > HitchMs) hitches++;
            }
            for (int i = open.Count - 1; i >= 0; i--)
            {
                var w = open[i];
                w.worst = Mathf.Max(w.worst, ms);
                w.sum += ms;
                w.frames++;
                if (now - w.start < Window) continue;
                Debug.Log($"[Frames] {w.label}: worst {w.worst:0.0} ms, avg {w.sum / w.frames:0.0} ms over {w.frames} frames{(w.worst > HitchMs ? "  HITCH" : "")}");
                open.RemoveAt(i);
            }
        }

        void OnApplicationQuit() =>
            Debug.Log($"[Frames] run: worst frame {runWorst:0.0} ms at {runWorstAt}, frames over {HitchMs:0} ms after warm-up: {hitches}");
    }
}
