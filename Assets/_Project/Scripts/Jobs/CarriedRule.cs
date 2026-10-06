using CampanhaRio.Kayak;
using UnityEngine;

namespace CampanhaRio.Jobs
{
    /// <summary>
    /// A rule about something one kayak carries (the letter, the goat): picks the CARRIER at each attempt (the local
    /// player's kayak, else the first of the group) and listens to its events (capsize, hits, landings). A rule can hand
    /// the cargo to another kayak (<see cref="SetCarrier"/>), e.g. whoever fishes the goat out of the water.
    /// </summary>
    public abstract class CarriedRule : JobModifier
    {
        public KayakController Carrier { get; private set; }

        public override void OnAttemptBegin()
        {
            base.OnAttemptBegin();
            KayakController first = null;
            foreach (var k in KayakRegistry.All) { first = k; break; }
            SetCarrier(KayakRegistry.Local ? KayakRegistry.Local : first);
        }

        public void SetCarrier(KayakController kayak)
        {
            if (Carrier)
            {
                Carrier.OnCapsize -= Capsized;
                Carrier.OnImpact -= Hit;
                Carrier.OnLand -= Landed;
            }
            Carrier = kayak;
            if (!Carrier) return;
            Carrier.OnCapsize += Capsized;
            Carrier.OnImpact += Hit;
            Carrier.OnLand += Landed;
        }

        protected virtual void OnDestroy() => SetCarrier(null);

        /// <summary>The carrier capsized.</summary>
        protected virtual void Capsized() { }
        /// <summary>The carrier hit something (level, intensity 0..1).</summary>
        protected virtual void Hit(ImpactLevel level, float intensity, Vector3 point, Vector3 normal) { }
        /// <summary>The carrier landed after this many seconds in the air (a drop, a jump).</summary>
        protected virtual void Landed(float airTime) { }
    }
}
