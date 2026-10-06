using CampanhaRio.Kayak;
using UnityEngine;

namespace CampanhaRio.Jobs
{
    /// <summary>
    /// The urgent letter (job 1, the basic sunset rule): a tight deadline, and the letter must not get too wet. Capsizing
    /// soaks it, big drops and hard hits splash it. The condition is how dry it is: it gives the cargo star.
    /// </summary>
    public class UrgentLetterRule : CarriedRule
    {
        [Tooltip("Wetness from a capsize.")]
        public float capsize = 0.35f;
        [Tooltip("Wetness from a landing, per second in the air (big drops splash more).")]
        public float landingPerSecond = 0.12f;
        [Tooltip("Landings shorter than this are free (little hops).")]
        public float freeAirTime = 0.35f;
        [Tooltip("Wetness from a hit, by level (Bump, Scrape, SpinOut) times its intensity.")]
        public float bump = 0.01f, scrape = 0.03f, spinOut = 0.06f;

        public override string Label => "Letter";

        public override string State =>
            Condition > 0.85f ? "dry" : Condition > 0.6f ? "a bit damp" : Condition > 0.3f ? "wet" : "soaked";

        void Wet(float amount, string why)
        {
            if (amount <= 0f) return;
            Condition = Mathf.Clamp01(Condition - amount);
            Debug.Log($"[Job] letter: {why}, -{amount:0.00} -> {Condition:0.00} ({State})");
        }

        protected override void Capsized() => Wet(capsize, "capsized");

        protected override void Landed(float airTime)
        {
            if (airTime > freeAirTime) Wet((airTime - freeAirTime) * landingPerSecond * 4f, $"splash after {airTime:0.0} s in the air");
        }

        protected override void Hit(ImpactLevel level, float intensity, Vector3 point, Vector3 normal)
        {
            float k = level == ImpactLevel.SpinOut ? spinOut : level == ImpactLevel.Scrape ? scrape : level == ImpactLevel.Bump ? bump : 0f;
            Wet(k * Mathf.Clamp01(intensity), $"{level} hit");
        }
    }
}
