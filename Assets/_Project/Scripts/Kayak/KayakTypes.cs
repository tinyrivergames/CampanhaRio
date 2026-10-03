using System;
using UnityEngine;

namespace CampanhaRio.Kayak
{
    /// <summary>
    /// One physics step of intent for one kayak. The simulation reads only this, so it doesn't matter who produced it:
    /// the local player (KayakInput), a bot (KayakAutopilot), a test (FeelBenchmark) or, in Etapa 8, the network.
    /// </summary>
    [Serializable]
    public struct KayakInputState
    {
        /// <summary>-1 left .. 1 right.</summary>
        public float steer;
        /// <summary>Forward paddling: held, or tapped since the last step (a quick tap is never lost).</summary>
        public bool paddle;
        public bool backPaddle;
        /// <summary>Roll pressed since the last step (the eskimo roll while capsized).</summary>
        public bool roll;
        /// <summary>Boost held (Stage 8): spends the meter for a strong push.</summary>
        public bool boost;
        /// <summary>Camera look this frame (only the local player's camera uses it).</summary>
        public Vector2 look;
        /// <summary>Camera zoom this frame (+ = in).</summary>
        public float zoom;
    }

    /// <summary>Anything that can drive a kayak. ReadInput is called once per physics step and clears one-shot presses.</summary>
    public interface IKayakInputSource
    {
        KayakInputState ReadInput();
    }

    /// <summary>What the kayak is going through. Animation, FX and the camera react to it.</summary>
    public enum KayakMode : byte { Normal, SpinOut, Capsizing, Capsized, Rolling, Recovering, Airborne }

    /// <summary>What a hit did (from the contact angle and the speed into the obstacle).</summary>
    public enum ImpactLevel : byte { Bump, Scrape, SpinOut, Capsize }

    /// <summary>What the paddler is doing in Normal mode (for the animation and the HUD).</summary>
    public enum KayakActivity : byte { Resting, Carried, Steering, Paddling, Airborne }

    /// <summary>
    /// Everything another machine needs to show this kayak (Etapa 8: the owner sends it, others interpolate it).
    /// Compact and serializable; presentation reads it instead of private controller fields.
    /// </summary>
    [Serializable]
    public struct KayakState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public KayakMode mode;
        /// <summary>Side of the last stroke: -1 left, +1 right.</summary>
        public sbyte strokeSide;
        /// <summary>0 at the catch of the last stroke .. 1 when the next one is due.</summary>
        public float strokePhase;
        /// <summary>0 upright .. 1 upside down.</summary>
        public float capsizeProgress;
        /// <summary>The smoothed steer value (-1..1).</summary>
        public float steer;
    }
}

namespace CampanhaRio.Kayak
{
    /// <summary>Something the paddler did well (or badly) that the meter, the score and the FX care about (Stage 8).</summary>
    public enum SkillType : byte
    {
        FastLine, PerfectStroke, Slingshot, Carve, Boof, Spin, BarrelRoll, CleanLanding, SloppyLanding, NearMiss, Boost,
        Draft, SyncJump, Pickup, Gate,
    }

    /// <summary>One skill moment: what, how big (degrees for spins, charge for slingshots, seconds for carves), and the meter it gave.</summary>
    public struct SkillEvent
    {
        public SkillType type;
        public float value;
        public float meter;

        public SkillEvent(SkillType type, float value, float meter) { this.type = type; this.value = value; this.meter = meter; }
    }
}
