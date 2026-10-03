using UnityEngine;

namespace CampanhaRio.Core
{
    /// <summary>The player's own settings, saved locally (PlayerPrefs). Grows with the settings menu.</summary>
    public static class GameSettings
    {
        public static float MouseSensitivity { get => PlayerPrefs.GetFloat("cr.sensitivity", 1f); set => PlayerPrefs.SetFloat("cr.sensitivity", value); }
        public static bool InvertY { get => PlayerPrefs.GetInt("cr.invertY", 0) == 1; set => PlayerPrefs.SetInt("cr.invertY", value ? 1 : 0); }
    }
}
