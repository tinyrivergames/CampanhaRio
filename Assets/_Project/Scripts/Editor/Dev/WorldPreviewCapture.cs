using System.IO;
using CampanhaRio.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Pictures of the vertical slice's places (Core's light and sky, each segment loaded with it), from fixed views.
    /// Batch: CR_SHOT_DIR=&lt;dir&gt; -executeMethod CampanhaRio.Editor.WorldPreviewCapture.Capture
    /// </summary>
    public static class WorldPreviewCapture
    {
        static readonly (string scene, string name, Vector3 eye, Vector3 at)[] Views =
        {
            ("Agencia", "agencia_patio", new Vector3(14f, 44f, -300f), new Vector3(-6f, 38f, -335f)),
            ("Agencia", "agencia_quadro", new Vector3(6f, 39.2f, -337f), new Vector3(6f, 38.4f, -330f)),
            ("Estrada_Vale", "estrada", new Vector3(3f, 42f, -262f), new Vector3(0f, 37f, -150f)),
            ("Rio_Moinho", "rio_largada", new Vector3(-40f, 50f, -20f), new Vector3(-5f, 36f, 20f)),
            ("Rio_Moinho", "rio_meio", new Vector3(-60f, 60f, 480f), new Vector3(-25f, 30f, 560f)),
            ("Rio_Moinho", "rio_chegada_vila", new Vector3(10f, 40f, 1030f), new Vector3(55f, 27f, 1075f)),
        };

        public static void Capture()
        {
            string dir = System.Environment.GetEnvironmentVariable("CR_SHOT_DIR");
            Directory.CreateDirectory(dir);
            string current = null;
            foreach (var v in Views)
            {
                if (v.scene != current)
                {
                    EditorSceneManager.OpenScene("Assets/_Project/Scenes/Core/Core.unity");
                    EditorSceneManager.OpenScene($"Assets/_Project/Scenes/Segments/{v.scene}.unity", OpenSceneMode.Additive);
                    current = v.scene;
                    var day = Object.FindAnyObjectByType<DayCycle>();
                    day.progress = DayCycle.Afternoon;
                    day.Apply();
                }
                var cam = Camera.main;
                foreach (var mb in cam.GetComponents<MonoBehaviour>()) mb.enabled = false;
                cam.farClipPlane = 1500f;
                cam.fieldOfView = 55f;
                cam.transform.position = v.eye;
                cam.transform.LookAt(v.at);
                Object.DestroyImmediate(LookDevCapture.Render(cam, 64));
                var tex = LookDevCapture.Render(cam, 1600, 900);
                LookDevCapture.Save(tex, Path.Combine(dir, v.name + ".png"));
                Object.DestroyImmediate(tex);
            }
            Debug.Log($"[Campanha] World preview captured: {Views.Length} views in {dir}");
        }
    }
}
