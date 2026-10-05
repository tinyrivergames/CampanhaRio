using System.IO;
using CampanhaRio.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampanhaRio.Editor
{
    /// <summary>
    /// Checks an asset's LODs at distance: a row of copies from near to far in the LookDev light, seen from the eye
    /// height of a kayaker, at the afternoon and at sunset. Batch: CR_LOD_ASSET=Assets/.../X.fbx
    ///   -executeMethod CampanhaRio.Editor.LodRowCapture.CaptureBatch  (Docs/Screenshots/&lt;date&gt;_lods_&lt;asset&gt;/)
    /// </summary>
    public static class LodRowCapture
    {
        static readonly float[] Distances = { 12f, 30f, 60f, 110f, 180f, 260f };

        public static void CaptureBatch()
        {
            string path = System.Environment.GetEnvironmentVariable("CR_LOD_ASSET");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EditorSceneManager.OpenScene(LookDevBuilder.ScenePath);
            var controller = Object.FindAnyObjectByType<LookDevController>();
            foreach (var s in controller.subjects) if (s) s.gameObject.SetActive(false);
            if (controller.scaleReference) controller.scaleReference.SetActive(false);

            var ground = GameObject.Find("Ground");
            if (ground) ground.transform.localScale = Vector3.one * 60f; // 600 m: the row needs a floor
            for (int i = 0; i < Distances.Length; i++)
            {
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(model);
                float side = Distances[i] * Mathf.Tan(Mathf.Deg2Rad * Mathf.Lerp(-32f, 32f, i / (Distances.Length - 1f))); // spread across the view, never behind each other
                copy.transform.SetPositionAndRotation(new Vector3(side, 0f, Distances[i]), Quaternion.Euler(0f, i * 47f, 0f));
                if (int.TryParse(System.Environment.GetEnvironmentVariable("CR_FORCE_LOD"), out int force)) copy.GetComponent<LODGroup>().ForceLOD(force);
            }

            var cam = controller.viewCamera;
            cam.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.Euler(-4f, 0f, 0f));
            cam.fieldOfView = 55f;
            cam.farClipPlane = 1000f;
            string folder = Path.Combine("Assets/_Project/Docs/Screenshots", System.DateTime.Now.ToString("yyyy-MM-dd_HHmm") + "_lods_" + model.name);
            Directory.CreateDirectory(folder);
            var warm = LookDevCapture.Render(cam, 64);
            Object.DestroyImmediate(warm);
            foreach (var (name, t) in new[] { ("tarde", DayCycle.Afternoon), ("por_do_sol", DayCycle.Sunset) })
            {
                controller.day.progress = t;
                controller.day.Apply();
                var tex = LookDevCapture.Render(cam, 1920, 1080);
                LookDevCapture.Save(tex, Path.Combine(folder, $"{model.name}_lods_{name}.png"));
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.Refresh();
            Debug.Log($"[Campanha] LOD row captured into {folder}");
        }
    }
}
