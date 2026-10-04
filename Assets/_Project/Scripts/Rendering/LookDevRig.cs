using System;
using UnityEngine;

namespace CampanhaRio.Rendering
{
    /// <summary>
    /// Art/LookDev/lookdev_rig.json, the light and views shared with the Blender preview rig (ArtSource/pipeline/lookdev.py).
    /// Colours are sRGB hex strings; directions and views are in Unity space.
    /// </summary>
    [Serializable]
    public class LookDevRig
    {
        [Serializable] public class Sun { public float elevationDeg, azimuthDeg, intensity, angularDiameterDeg; public string color; }
        [Serializable] public class Ambient { public string sky, equator, ground; public float strength; }
        [Serializable] public class Background { public string top, horizon; }
        [Serializable] public class Ground { public string color; public float size; }
        [Serializable] public class CameraSettings { public float fovDeg, pitchDeg, margin; }
        [Serializable] public class View { public string name, label; public float yawDeg; }
        [Serializable] public class Closeup { public string label; public float yawDeg, pitchDeg, zoom; }
        [Serializable] public class ScaleRef { public string label, color; public float height, gap, yawDeg; }
        [Serializable] public class Grading { public float postExposure, saturation, contrast, temperature; }
        [Serializable] public class Sky { public float horizonSharpness; public string ground; }
        [Serializable] public class ContactAO { public float intensity, radius, directStrength; }

        public int version;
        public Sun sun;
        public Ambient ambient;
        public string shadowTint;
        public float shadowTintStrength;
        public Background background;
        public Ground ground;
        public CameraSettings camera;
        public View[] views;
        public Closeup closeup;
        public ScaleRef scaleRef;
        public Grading grading;
        public Sky sky;
        public ContactAO contactAO;
        public int tileSize;

        public static LookDevRig Parse(string json) => JsonUtility.FromJson<LookDevRig>(json);

        /// <summary>An sRGB hex colour ("#RRGGBB") as a Unity Color (sRGB, like the colour pickers).</summary>
        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
