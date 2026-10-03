namespace CampanhaRio.Kayak
{
    /// <summary>
    /// How earned meter turns into speed. Ported from the old project's "Natural" game feel (no boost button: what you earn
    /// by reading the water becomes an immediate surge, the water pushing you). Values are the Natural preset's.
    /// </summary>
    public sealed class KayakFeel
    {
        public static readonly KayakFeel Current = new KayakFeel();

        /// <summary>A boost button that spends the meter. Off: meter gains become an immediate surge instead.</summary>
        public bool manualBoost = false;
        /// <summary>Surge per meter point (m/s), the most that can be pending at once (m/s), and how fast it is given (m/s per s).</summary>
        public float surgePerMeter = 0.06f;
        public float maxSurge = 1.6f;
        public float surgeRate = 4f;
    }
}
