using System;
using System.Globalization;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// The command-line switches for test instances (several builds on one PC, see Docs/TECH_DECISIONS.md):
    ///   -cc-host                   open a direct room
    ///   -cc-join [ip[:port]]       join one (127.0.0.1 by default)
    ///   -cc-name Ana  -cc-color 2  this instance's player (never saved over the real player's settings)
    ///   -cc-bot normal             the autopilot drives this player's kayak (Careful / Normal / Risky)
    ///   -cc-scene KayakTest        open this scene after Core (solo or as the host)
    ///   -cc-script streamwalk      run a scripted test (see TestScripts)
    ///   -cc-shots &lt;dir&gt;           capture the standard views into dir, then quit (see ScreenshotCapture)
    ///   -cc-quit 60                quit after this many seconds
    /// Parsed once; everything reads the static properties.
    /// </summary>
    public static class TestSwitches
    {
        static string[] args;
        static string[] Args => args ??= Environment.GetCommandLineArgs();

        public static bool Has(string name) => Array.IndexOf(Args, name) >= 0;

        /// <summary>The value after a switch (or fallback when the switch is missing or has no value).</summary>
        public static string Value(string name, string fallback = null)
        {
            int i = Array.IndexOf(Args, name);
            if (i < 0) return fallback;
            if (i + 1 < Args.Length && !Args[i + 1].StartsWith("-")) return Args[i + 1];
            return fallback;
        }

        public static float Number(string name, float fallback) =>
            float.TryParse(Value(name), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : fallback;

        public static bool Host => Has("-cc-host");
        public static bool Join => Has("-cc-join");
        public static string JoinAddress => Value("-cc-join", "127.0.0.1");
        public static string Scene => Value("-cc-scene");
        public static string Script => Value("-cc-script");
        public static string ShotsFolder => Value("-cc-shots");
        public static float QuitAfter => Number("-cc-quit", 0f);

        /// <summary>Set by -cc-bot: the autopilot drives the local player's kayak with this profile.</summary>
        public static AutopilotProfile? LocalBotProfile
        {
            get
            {
                if (!Has("-cc-bot")) return null;
                return Enum.TryParse(Value("-cc-bot", "Normal"), true, out AutopilotProfile p) ? p : AutopilotProfile.Normal;
            }
        }

        /// <summary>Applies -cc-name / -cc-color (call once at boot, before hosting or joining).</summary>
        public static void ApplyPlayerOverrides()
        {
            string name = Value("-cc-name");
            if (!string.IsNullOrEmpty(name)) Net.NetSession.NameOverride = name;
            if (int.TryParse(Value("-cc-color"), out int color)) Net.NetSession.ColorOverride = color;
        }
    }
}
