using System;
using System.Collections.Generic;
using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Kayak
{
    /// <summary>
    /// Sync jump (Stage 8, together): two or more kayaks that took off from the same feature (or within Distance of each
    /// other) within Window seconds of each other, and each got real air (Min Air), all get a SyncJump skill as they land.
    /// Each kayak is paid once per jump; the value is the group size.
    /// Multiplayer (Stage 9): friends' takeoffs and landings arrive as events stamped with the shared clock (RemoteTakeOff,
    /// RemoteLand), and every machine pays only the kayaks it simulates, so each owner pays itself exactly once.
    /// </summary>
    public static class SyncJumps
    {
        public static float Window = 0.5f;
        public static float Distance = 12f;
        public static float MinAir = 0.3f;
        public static float Meter = 10f;
        /// <summary>The clock takeoffs are stamped with (the network's shared clock in multiplayer).</summary>
        public static Func<double> Clock = () => Time.fixedTimeAsDouble;

        class Jump { public KayakController kayak; public double takeOff; public IRiverModifier feature; public Vector3 position; public bool landed, paid; }
        static readonly List<Jump> jumps = new List<Jump>();

        /// <summary>A kayak just left the water (KayakPhysics calls this on takeoff).</summary>
        internal static void TakeOff(KayakController kayak, IRiverModifier feature) => Add(kayak, Clock(), feature, kayak.transform.position);

        /// <summary>A friend's kayak left the water at this shared-clock time and place (from the network).</summary>
        public static void RemoteTakeOff(KayakController kayak, double time, Vector3 position)
        {
            var feature = kayak.river ? kayak.river.FeatureAt(position, out _) : null;
            Add(kayak, time, feature, position);
        }

        static void Add(KayakController kayak, double time, IRiverModifier feature, Vector3 position)
        {
            jumps.RemoveAll(j => !j.kayak || j.kayak == kayak || time - j.takeOff > 4.0);
            jumps.Add(new Jump { kayak = kayak, takeOff = time, feature = feature, position = position });
        }

        /// <summary>A kayak touched down after this much air: pays everyone in its group who has landed a real jump too.</summary>
        internal static void Land(KayakController kayak, float airTime)
        {
            var me = jumps.Find(j => j.kayak == kayak && !j.landed);
            if (me == null) return;
            if (airTime < MinAir) { jumps.Remove(me); return; }
            me.landed = true;
            var group = jumps.FindAll(j => j.landed && Together(j, me));
            if (group.Count < 2) return;
            foreach (var j in group)
            {
                if (j.paid || j.kayak.IsRemote) continue; // a friend's owner pays them on their machine
                j.paid = true;
                j.kayak.RaiseSkill(SkillType.SyncJump, group.Count, Meter);
            }
        }

        /// <summary>A friend's kayak landed (from the network): it may complete a group for a kayak simulated here.</summary>
        public static void RemoteLand(KayakController kayak, float airTime) => Land(kayak, airTime);

        static bool Together(Jump a, Jump b) =>
            Math.Abs(a.takeOff - b.takeOff) <= Window &&
            ((a.feature != null && a.feature == b.feature) || (a.position - b.position).sqrMagnitude <= Distance * Distance);

        public static void Clear() => jumps.Clear();
    }
}
