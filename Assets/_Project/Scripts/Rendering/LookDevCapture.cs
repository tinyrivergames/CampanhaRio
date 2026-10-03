using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CampanhaRio.Rendering
{
    /// <summary>
    /// Renders the lookdev views of a subject exactly like the Blender preview (ArtSource/pipeline/lookdev.py frame()):
    /// orbit (pitch, yaw) around the bounding sphere's centre, far enough to fit the sphere in the vertical field of view.
    /// Used by the editor menu (CampanhaRio > LookDev > Capture) and by builds with -cc-shots.
    /// </summary>
    public static class LookDevCapture
    {
        /// <summary>The front / side / back / 3/4 views (+ the close-up) as PNGs, plus one strip with all of them.</summary>
        public static List<string> CaptureViews(LookDevRig rig, Camera cam, IList<Renderer> subject, string folder, string prefix)
        {
            Directory.CreateDirectory(folder);
            var files = new List<string>();
            var tiles = new List<Texture2D>();
            var bounds = Bounds(subject);
            foreach (var view in rig.views)
            {
                Place(cam, bounds, view.yawDeg, rig.camera.pitchDeg, rig.camera.fovDeg, rig.camera.margin, 1f);
                tiles.Add(Render(cam, rig.tileSize));
                files.Add(Save(tiles[tiles.Count - 1], Path.Combine(folder, $"{prefix}_{view.name}.png")));
            }
            Place(cam, bounds, rig.closeup.yawDeg, rig.closeup.pitchDeg, rig.camera.fovDeg, rig.camera.margin, rig.closeup.zoom);
            tiles.Add(Render(cam, rig.tileSize));
            files.Add(Save(tiles[tiles.Count - 1], Path.Combine(folder, $"{prefix}_closeup.png")));
            files.Add(Save(Strip(tiles), Path.Combine(folder, $"{prefix}_views.png")));
            foreach (var t in tiles) { if (Application.isPlaying) Object.Destroy(t); else Object.DestroyImmediate(t); }
            return files;
        }

        public static Bounds Bounds(IList<Renderer> renderers)
        {
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        public static void Place(Camera cam, Bounds b, float yaw, float pitch, float fov, float margin, float zoom)
        {
            float radius = Mathf.Max(b.extents.magnitude, 0.01f);
            float dist = radius * margin / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) / zoom;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.SetPositionAndRotation(b.center - rot * Vector3.forward * dist, rot);
            cam.fieldOfView = fov;
            cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 4f);
            cam.farClipPlane = dist + radius * 8f + 500f;
        }

        /// <summary>One frame from this camera into a texture (MSAA, post-processing on, sRGB like the screen).</summary>
        public static Texture2D Render(Camera cam, int width, int height = 0)
        {
            if (height <= 0) height = width;
            var rt = RenderTexture.GetTemporary(new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24) { msaaSamples = 4, sRGB = true });
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            cam.targetTexture = previous;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        public static string Save(Texture2D tex, string path)
        {
            File.WriteAllBytes(path, tex.EncodeToPNG());
            return path;
        }

        static Texture2D Strip(List<Texture2D> tiles)
        {
            int t = tiles[0].width, gap = 8;
            var strip = new Texture2D(tiles.Count * t + (tiles.Count - 1) * gap, t, TextureFormat.RGB24, false);
            var fill = new Color32[strip.width * t];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(236, 231, 221, 255);
            strip.SetPixels32(fill);
            for (int i = 0; i < tiles.Count; i++) strip.SetPixels(i * (t + gap), 0, t, t, tiles[i].GetPixels());
            strip.Apply();
            return strip;
        }
    }
}
