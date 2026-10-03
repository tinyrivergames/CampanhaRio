using System.Collections.Generic;
using UnityEngine;

namespace CampanhaRio.River
{
    /// <summary>
    /// Something placed in the river that locally shapes the water: its flow (eddies, cross currents, chutes),
    /// its height (ledges, waves, humps) and its foam. FlowObstacle, CrossCurrent and RiverFeature implement it.
    /// RiverPath asks every modifier after computing the base flow; the water mesh bakes height and foam.
    /// </summary>
    public interface IRiverModifier
    {
        string DisplayName { get; }
        /// <summary>World-space area of influence (XZ used) for a cheap early-out.</summary>
        Bounds Influence { get; }
        /// <summary>Water height added at a position (m).</summary>
        float HeightOffset(Vector3 position, RiverPath river);
        /// <summary>Changes the water velocity at a position (base flow in, modified flow out).</summary>
        void ModifyFlow(Vector3 position, in RiverPath.RiverSample sample, ref Vector3 velocity);
        /// <summary>Extra foam baked into the water mesh (0..1).</summary>
        float Foam(Vector3 position, RiverPath river);
        /// <summary>How much this modifier dominates at a position (0..1), for "feature under the kayak".</summary>
        float Weight(Vector3 position);
    }

    public static class RiverModifiers
    {
        static readonly List<IRiverModifier> all = new List<IRiverModifier>();
        public static IReadOnlyList<IRiverModifier> All => all;

        /// <summary>Increments whenever a modifier is added, removed or edited (the water mesh rebakes).</summary>
        public static int Version { get; private set; }

        public static void Register(IRiverModifier m) { if (!all.Contains(m)) all.Add(m); Version++; }
        public static void Unregister(IRiverModifier m) { all.Remove(m); Version++; }
        public static void Changed() => Version++;

        public static bool Touches(IRiverModifier m, Vector3 p)
        {
            var b = m.Influence;
            return p.x >= b.min.x && p.x <= b.max.x && p.z >= b.min.z && p.z <= b.max.z;
        }
    }
}
