using CampanhaRio.Kayak;
using CampanhaRio.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Checks the river water around and inside the kayak: the KayakTest kayak sunk a little and pitched stern-down (as it
    /// rides while paddling), seen from above-behind and from the side. Batch: CR_SHOT_DIR=&lt;folder&gt;
    ///   -executeMethod CampanhaRio.Editor.KayakWaterCheck.Capture
    /// </summary>
    public static class KayakWaterCheck
    {
        public static void Capture()
        {
            string dir = System.Environment.GetEnvironmentVariable("CR_SHOT_DIR");
            EditorSceneManager.OpenScene(KayakTestBuilder.ScenePath);
            var kayak = Object.FindAnyObjectByType<KayakController>().transform;
            kayak.position += Vector3.down * 0.1f;
            kayak.rotation *= Quaternion.Euler(-4f, 0f, 0f); // stern down
            var cam = Camera.main;
            var follow = cam.GetComponent<MonoBehaviour>();
            foreach (var mb in cam.GetComponents<MonoBehaviour>()) mb.enabled = false;
            var shots = new[] { ("top", new Vector3(0.8f, 3.2f, -2.6f)), ("side", new Vector3(2.6f, 1.0f, -0.6f)) };
            Object.DestroyImmediate(LookDevCapture.Render(cam, 64));
            foreach (var (name, offset) in shots)
            {
                cam.transform.position = kayak.TransformPoint(offset);
                cam.transform.LookAt(kayak.position + Vector3.up * 0.1f);
                cam.fieldOfView = 50f;
                var tex = LookDevCapture.Render(cam, 1280, 720);
                LookDevCapture.Save(tex, System.IO.Path.Combine(dir, $"kayak_water_{name}.png"));
                Object.DestroyImmediate(tex);
            }
            Debug.Log("[Campanha] Kayak water check captured.");
        }
    }
}
