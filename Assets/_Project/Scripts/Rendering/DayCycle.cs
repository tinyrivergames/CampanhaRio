using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampanhaRio.Rendering
{
    /// <summary>
    /// The time of day from one number: <see cref="progress"/> 0 (afternoon) .. 1 (night), through golden hour, sunset and
    /// dusk. It drives the sun (angle, colour, intensity), the SoftToon ambient and shadow tint, the fog and the sky
    /// gradient. The river's sunset timer (RiverChallenge) moves it; the LookDev scene has a slider.
    /// The afternoon key is the shared lookdev rig (the Blender previews use the same light).
    /// </summary>
    [ExecuteAlways]
    public class DayCycle : MonoBehaviour
    {
        [Serializable]
        public struct DayKey
        {
            public string name;
            [Range(0f, 1f)] public float at;
            [Header("Sun")]
            public float sunElevation;
            public float sunAzimuth;
            public Color sunColor;
            public float sunIntensity;
            [Header("Ambient and shadows")]
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public float ambientStrength;
            public Color shadowTint;
            public float shadowTintStrength;
            [Header("Sky and fog")]
            public Color skyTop;
            public Color skyHorizon;
            public Color skyGround;
            public float sunGlow;
            public Color fogColor;
            public float fogDensity;
        }

        public static DayCycle Instance { get; private set; }

        /// <summary>The sun's shadow bias (depth, normal). Low, so shadows start right at the feet.</summary>
        public const float ShadowBias = 0.4f, ShadowNormalBias = 0.35f;

        [Range(0f, 1f)] public float progress;
        public Light sun;
        [Tooltip("The CampanhaRio/SkyGradient material (set as the scene skybox).")]
        public Material skybox;
        [Tooltip("The states along the day, in order of 'at'. The first is the lookdev rig's afternoon.")]
        public DayKey[] keys = DefaultKeys();

        /// <summary>The state applied last (for other systems: the water, the HUD).</summary>
        public DayKey Current { get; private set; }

        static readonly int ShadowTintId = Shader.PropertyToID("_CR_ShadowTint");
        static readonly int AmbientSkyId = Shader.PropertyToID("_CR_AmbientSky");
        static readonly int AmbientEquatorId = Shader.PropertyToID("_CR_AmbientEquator");
        static readonly int AmbientGroundId = Shader.PropertyToID("_CR_AmbientGround");
        static readonly int SkyTopId = Shader.PropertyToID("_CR_SkyTop");
        static readonly int SkyHorizonId = Shader.PropertyToID("_CR_SkyHorizon");
        static readonly int SkyGroundId = Shader.PropertyToID("_CR_SkyGround");
        static readonly int SunGlowId = Shader.PropertyToID("_CR_SunGlow");
        static readonly int SunDirId = Shader.PropertyToID("_CR_SunDir");

        void OnEnable() { Instance = this; Apply(); }
        void OnDisable() { if (Instance == this) Instance = null; }
        void Update() => Apply();
        void OnValidate() => Apply();

        public void Apply()
        {
            if (keys == null || keys.Length == 0) return;
            var k = Evaluate(progress);
            Current = k;
            if (sun)
            {
                sun.transform.rotation = Quaternion.Euler(k.sunElevation, k.sunAzimuth, 0f);
                sun.color = k.sunColor;
                sun.intensity = k.sunIntensity;
                sun.shadows = k.sunIntensity > 0.02f ? LightShadows.Soft : LightShadows.None;
                // Low bias: a high one detaches the shadow from the feet ("peter-panning"). The ramp hides the terminator.
                var urp = sun.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                if (urp) urp.usePipelineSettings = false;
                sun.shadowBias = ShadowBias;
                sun.shadowNormalBias = ShadowNormalBias;
            }
            Shader.SetGlobalVector(ShadowTintId, Linear(k.shadowTint) * k.shadowTintStrength);
            Shader.SetGlobalVector(AmbientSkyId, Linear(k.ambientSky) * k.ambientStrength);
            Shader.SetGlobalVector(AmbientEquatorId, Linear(k.ambientEquator) * k.ambientStrength);
            Shader.SetGlobalVector(AmbientGroundId, Linear(k.ambientGround) * k.ambientStrength);
            Shader.SetGlobalVector(SkyTopId, Linear(k.skyTop));
            Shader.SetGlobalVector(SkyHorizonId, Linear(k.skyHorizon));
            Shader.SetGlobalVector(SkyGroundId, Linear(k.skyGround));
            Shader.SetGlobalVector(SunGlowId, Linear(k.sunColor) * k.sunGlow);
            Shader.SetGlobalVector(SunDirId, -(Quaternion.Euler(k.sunElevation, k.sunAzimuth, 0f) * Vector3.forward));

            // Other shaders (the water, URP Lit) read Unity's own ambient and fog: keep them in step
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = k.ambientSky * k.ambientStrength;
            RenderSettings.ambientEquatorColor = k.ambientEquator * k.ambientStrength;
            RenderSettings.ambientGroundColor = k.ambientGround * k.ambientStrength;
            RenderSettings.fog = k.fogDensity > 0f;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = k.fogColor;
            RenderSettings.fogDensity = k.fogDensity;
            if (skybox) RenderSettings.skybox = skybox;
            if (sun) RenderSettings.sun = sun;
        }

        static Vector4 Linear(Color c) { var l = c.linear; return new Vector4(l.r, l.g, l.b, 1f); }

        /// <summary>The blended state at t (0..1).</summary>
        public DayKey Evaluate(float t)
        {
            if (t <= keys[0].at) return keys[0];
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].at) continue;
                var a = keys[i - 1];
                var b = keys[i];
                float f = Mathf.InverseLerp(a.at, b.at, t);
                return Lerp(a, b, f);
            }
            return keys[keys.Length - 1];
        }

        static DayKey Lerp(DayKey a, DayKey b, float t) => new DayKey
        {
            name = t < 0.5f ? a.name : b.name,
            at = Mathf.Lerp(a.at, b.at, t),
            sunElevation = Mathf.Lerp(a.sunElevation, b.sunElevation, t),
            sunAzimuth = Mathf.LerpAngle(a.sunAzimuth, b.sunAzimuth, t),
            sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
            sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
            ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
            ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t),
            ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
            ambientStrength = Mathf.Lerp(a.ambientStrength, b.ambientStrength, t),
            shadowTint = Color.Lerp(a.shadowTint, b.shadowTint, t),
            shadowTintStrength = Mathf.Lerp(a.shadowTintStrength, b.shadowTintStrength, t),
            skyTop = Color.Lerp(a.skyTop, b.skyTop, t),
            skyHorizon = Color.Lerp(a.skyHorizon, b.skyHorizon, t),
            skyGround = Color.Lerp(a.skyGround, b.skyGround, t),
            sunGlow = Mathf.Lerp(a.sunGlow, b.sunGlow, t),
            fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
            fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
        };

        /// <summary>Progress values of the named states (for captures and tests).</summary>
        public const float Afternoon = 0f, GoldenHour = 0.4f, Sunset = 0.65f, Dusk = 0.85f, Night = 1f;

        static Color C(string hex) => LookDevRig.Hex(hex);

        /// <summary>
        /// The default day. Key 0 equals lookdev_rig.json (the builder overwrites it from the file, so a change in the rig
        /// reaches both Blender and Unity).
        /// </summary>
        public static DayKey[] DefaultKeys() => new[]
        {
            new DayKey { name = "Tarde", at = Afternoon, sunElevation = 42f, sunAzimuth = -38f, sunColor = C("#FFF0D6"), sunIntensity = 1.15f,
                ambientSky = C("#9DB8D9"), ambientEquator = C("#A9AE9C"), ambientGround = C("#6E6150"), ambientStrength = 0.55f,
                shadowTint = C("#5D6A9E"), shadowTintStrength = 0.35f, skyTop = C("#78A9DC"), skyHorizon = C("#D4E3EC"), skyGround = C("#8A8577"),
                sunGlow = 0.25f, fogColor = C("#B7D3EA"), fogDensity = 0.0011f }, // (clear afternoon: the developer's reference)
            new DayKey { name = "Hora dourada", at = GoldenHour, sunElevation = 17f, sunAzimuth = -52f, sunColor = C("#FFC985"), sunIntensity = 1.1f,
                ambientSky = C("#A7B2D6"), ambientEquator = C("#CBAE8E"), ambientGround = C("#6B5444"), ambientStrength = 0.5f,
                shadowTint = C("#6A5E9E"), shadowTintStrength = 0.42f, skyTop = C("#6E9BD0"), skyHorizon = C("#F3D3A4"), skyGround = C("#7E6C5C"),
                sunGlow = 0.5f, fogColor = C("#E6CDA6"), fogDensity = 0.0045f },
            new DayKey { name = "Por do sol", at = Sunset, sunElevation = 4f, sunAzimuth = -62f, sunColor = C("#FF965A"), sunIntensity = 0.85f,
                ambientSky = C("#8E8DB8"), ambientEquator = C("#C98F78"), ambientGround = C("#4F3E3E"), ambientStrength = 0.48f,
                shadowTint = C("#6A4E8E"), shadowTintStrength = 0.5f, skyTop = C("#4D6AA8"), skyHorizon = C("#F59A6B"), skyGround = C("#5E4846"),
                sunGlow = 0.9f, fogColor = C("#D99577"), fogDensity = 0.006f },
            new DayKey { name = "Crepusculo", at = Dusk, sunElevation = -3f, sunAzimuth = -68f, sunColor = C("#C77B8F"), sunIntensity = 0.22f,
                ambientSky = C("#4F5788"), ambientEquator = C("#6E5872"), ambientGround = C("#2E2A3A"), ambientStrength = 0.5f,
                shadowTint = C("#3B3A6A"), shadowTintStrength = 0.55f, skyTop = C("#26305E"), skyHorizon = C("#8E6684"), skyGround = C("#2E2A3A"),
                sunGlow = 0.4f, fogColor = C("#5E5274"), fogDensity = 0.008f },
            new DayKey { name = "Noite", at = Night, sunElevation = -15f, sunAzimuth = -75f, sunColor = C("#3E4A78"), sunIntensity = 0.04f,
                ambientSky = C("#1C2440"), ambientEquator = C("#1E2032"), ambientGround = C("#121218"), ambientStrength = 0.4f,
                shadowTint = C("#141830"), shadowTintStrength = 0.5f, skyTop = C("#0A0F24"), skyHorizon = C("#1E2440"), skyGround = C("#101018"),
                sunGlow = 0.05f, fogColor = C("#1A1E30"), fogDensity = 0.01f },
        };

        /// <summary>Key 0 from the shared rig (sun, ambient, shadow tint, sky).</summary>
        public static DayKey FromRig(LookDevRig rig, DayKey baseKey)
        {
            var k = baseKey;
            k.sunElevation = rig.sun.elevationDeg;
            k.sunAzimuth = rig.sun.azimuthDeg;
            k.sunColor = LookDevRig.Hex(rig.sun.color);
            k.sunIntensity = rig.sun.intensity;
            k.ambientSky = LookDevRig.Hex(rig.ambient.sky);
            k.ambientEquator = LookDevRig.Hex(rig.ambient.equator);
            k.ambientGround = LookDevRig.Hex(rig.ambient.ground);
            k.ambientStrength = rig.ambient.strength;
            k.shadowTint = LookDevRig.Hex(rig.shadowTint);
            k.shadowTintStrength = rig.shadowTintStrength;
            k.skyTop = LookDevRig.Hex(rig.background.top);
            k.skyHorizon = LookDevRig.Hex(rig.background.horizon);
            k.skyGround = LookDevRig.Hex(rig.sky.ground);
            return k;
        }
    }
}
