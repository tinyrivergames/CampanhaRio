using System;
using System.Collections.Generic;

namespace CampanhaRio.Kayak
{
    /// <summary>
    /// Every kayak in the scene (players and bots), and which one is the local player's.
    /// There is no "the kayak": anything that concerns all of them loops over All; the camera, HUD and input use Local.
    /// </summary>
    public static class KayakRegistry
    {
        static readonly List<KayakController> all = new List<KayakController>();
        public static IReadOnlyList<KayakController> All => all;

        /// <summary>The kayak marked with <see cref="LocalPlayer"/> (null if none).</summary>
        public static KayakController Local { get; private set; }

        /// <summary>Raised when the local kayak changes (spawned, swapped, destroyed).</summary>
        public static event Action<KayakController> LocalChanged;

        /// <summary>A kayak joined (a bot spawned, later a friend connected).</summary>
        public static event Action<KayakController> Added;

        internal static void Register(KayakController kayak)
        {
            if (all.Contains(kayak)) return;
            all.Add(kayak);
            Added?.Invoke(kayak);
        }

        internal static void Unregister(KayakController kayak)
        {
            all.Remove(kayak);
            if (Local == kayak) SetLocal(null);
        }

        internal static void SetLocal(KayakController kayak)
        {
            if (Local == kayak) return;
            Local = kayak;
            LocalChanged?.Invoke(kayak);
        }
    }
}
