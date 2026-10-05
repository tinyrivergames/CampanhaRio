using System.Collections;
using System.Collections.Generic;
using System.IO;
using CampanhaRio.Dev;
using UnityEngine;

namespace CampanhaRio.Rendering
{
    /// <summary>
    /// The LookDev scene (Scenes/Dev/LookDev): pick the subject on the turntable, slide the time of day, and capture.
    ///   Play mode: a small panel (subject buttons, a time-of-day slider, Capture).
    ///   Builds with -cc-shots &lt;dir&gt;: captures every subject's views at the afternoon (the Blender preview light) and one
    ///   wide shot per time of day, then quits. The editor menu CampanhaRio > LookDev > Capture does the same in Edit mode.
    /// </summary>
    public class LookDevController : MonoBehaviour
    {
        public TextAsset rigJson;
        public DayCycle day;
        public Turntable turntable;
        [Tooltip("Children of the turntable: one is shown at a time.")]
        public Transform[] subjects;
        [Tooltip("Renders the views (disabled; it only renders on demand).")]
        public Camera captureCamera;
        [Tooltip("The scene camera (the wide time-of-day shots).")]
        public Camera viewCamera;
        [Tooltip("Hidden while the subject views are captured (the Blender views show the asset alone).")]
        public GameObject scaleReference;

        int current;

        void Start()
        {
            Show(current);
            string folder = TestSwitches.ShotsFolder;
            if (!string.IsNullOrEmpty(folder) && ShotCapture.IsTarget(gameObject.scene)) StartCoroutine(CaptureAndQuit(folder));
        }

        public LookDevRig Rig => LookDevRig.Parse(rigJson.text);

        public void Show(int index)
        {
            current = Mathf.Clamp(index, 0, subjects.Length - 1);
            for (int i = 0; i < subjects.Length; i++) if (subjects[i]) subjects[i].gameObject.SetActive(i == current);
        }

        public List<Renderer> SubjectRenderers(int index)
        {
            var list = new List<Renderer>();
            foreach (var r in subjects[index].GetComponentsInChildren<Renderer>())
            {
                if (r is MeshRenderer && r.enabled && r.gameObject.activeInHierarchy && !(r.GetComponentInParent<LODGroup>() && !IsLod0(r))) list.Add(r);
            }
            return list;
        }

        static bool IsLod0(Renderer r) => r.name.EndsWith("_LOD0") || !r.name.Contains("_LOD");

        /// <summary>Every subject's views at the rig's afternoon, then one wide shot per time of day.</summary>
        public List<string> CaptureAll(string folder)
        {
            var rig = Rig;
            var files = new List<string>();
            bool spin = turntable && turntable.spinning;
            if (turntable) { turntable.spinning = false; turntable.ResetAngle(); }
            float progress = day.progress;
            day.progress = DayCycle.Afternoon;
            day.Apply();
            if (scaleReference) scaleReference.SetActive(false);
            var warmup = LookDevCapture.Render(captureCamera, 64); // the first render after loading can miss shaders and globals
            if (Application.isPlaying) Destroy(warmup); else DestroyImmediate(warmup);
            for (int i = 0; i < subjects.Length; i++)
            {
                if (!subjects[i]) continue;
                Show(i);
                var renderers = SubjectRenderers(i);
                var settle = LookDevCapture.Render(captureCamera, 64); // the first frame after a switch is not settled yet
                if (Application.isPlaying) Destroy(settle); else DestroyImmediate(settle);
                if (renderers.Count > 0) files.AddRange(LookDevCapture.CaptureViews(rig, captureCamera, renderers, folder, subjects[i].name));
            }
            if (scaleReference) scaleReference.SetActive(true);
            // One wide shot per time of day for every subject (with the scale animal beside it)
            for (int i = 0; i < subjects.Length; i++)
            {
                if (!subjects[i]) continue;
                Show(i);
                // A wide 3/4 shot framed on this subject (and the scale animal beside it), as the scene camera would
                var wide = SubjectRenderers(i);
                if (scaleReference) wide.AddRange(scaleReference.GetComponentsInChildren<Renderer>());
                LookDevCapture.Place(viewCamera, LookDevCapture.Bounds(wide), 205f, 10f, 40f, 1.15f, 1f);
                foreach (var (name, t) in new[] { ("tarde", DayCycle.Afternoon), ("hora_dourada", DayCycle.GoldenHour), ("por_do_sol", DayCycle.Sunset), ("crepusculo", DayCycle.Dusk) })
                {
                    day.progress = t;
                    day.Apply();
                    var tex = LookDevCapture.Render(viewCamera, 1280, 720);
                    files.Add(LookDevCapture.Save(tex, Path.Combine(folder, $"{subjects[i].name}_day_{(int)(t * 100):000}_{name}.png")));
                    if (Application.isPlaying) Destroy(tex); else DestroyImmediate(tex);
                }
            }
            Show(current);
            day.progress = progress;
            day.Apply();
            if (turntable) turntable.spinning = spin;
            return files;
        }

        IEnumerator CaptureAndQuit(string folder)
        {
            for (int i = 0; i < 10; i++) yield return null; // let shadows and post settle
            var files = CaptureAll(folder);
            Debug.Log($"[Campanha] LookDev captured {files.Count} images into {folder}");
            Application.Quit();
        }

        void OnGUI()
        {
            if (!string.IsNullOrEmpty(TestSwitches.ShotsFolder)) return;
            GUILayout.BeginArea(new Rect(12, 12, 320, 200), GUI.skin.box);
            GUILayout.Label($"Hora do dia: {day.Current.name}  ({day.progress:0.00})");
            day.progress = GUILayout.HorizontalSlider(day.progress, 0f, 1f);
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < subjects.Length; i++)
                if (subjects[i] && GUILayout.Button(subjects[i].name)) Show(i);
            GUILayout.EndHorizontal();
            if (turntable) turntable.spinning = GUILayout.Toggle(turntable.spinning, "Girar");
            if (GUILayout.Button("Capturar (Docs/Screenshots)"))
                CaptureAll(Path.Combine(Application.dataPath, "_Project/Docs/Screenshots", System.DateTime.Now.ToString("yyyy-MM-dd_HHmm") + "_lookdev"));
            GUILayout.EndArea();
        }
    }
}
