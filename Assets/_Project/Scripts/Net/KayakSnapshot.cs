using CampanhaRio.Kayak;
using Unity.Netcode;
using UnityEngine;

namespace CampanhaRio.Net
{
    /// <summary>
    /// One owner-side frame of a kayak (Stage 9): about 45 bytes. Position at full precision; rotation smallest-three
    /// (4 bytes); velocities as halves; the discrete state in bytes. Stamped with the shared clock at the owner's step.
    /// </summary>
    public struct KayakSnapshot : INetworkSerializable
    {
        public double time;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity, angularVelocity;
        public KayakMode mode;
        public float steer, strokePhase, capsizeProgress, boost, airTime;
        public sbyte strokeSide;
        public byte strokeCount;
        public bool airborne, strokeBack, held;

        const byte FlagAirborne = 1, FlagBack = 2, FlagHeld = 4;

        public static KayakSnapshot From(KayakController k, double now)
        {
            var s = k.State;
            return new KayakSnapshot
            {
                time = now,
                position = k.transform.position,
                rotation = k.transform.rotation,
                velocity = k.Velocity,
                angularVelocity = s.angularVelocity,
                mode = k.Mode,
                steer = k.Steer,
                strokePhase = s.strokePhase,
                capsizeProgress = s.capsizeProgress,
                boost = k.BoostVisual,
                airTime = k.AirTime,
                strokeSide = (sbyte)k.LastStrokeSide,
                strokeCount = (byte)k.StrokeCount,
                airborne = k.IsAirborne,
                strokeBack = k.LastStrokeBack,
                held = k.Held,
            };
        }

        public KayakState ToState() => new KayakState
        {
            position = position, rotation = rotation, velocity = velocity, angularVelocity = angularVelocity, mode = mode,
            strokeSide = strokeSide, strokePhase = strokePhase, capsizeProgress = capsizeProgress, steer = steer,
        };

        /// <summary>Between two snapshots (the continuous parts; the discrete ones come from the older).</summary>
        public static KayakSnapshot Lerp(in KayakSnapshot a, in KayakSnapshot b, float t)
        {
            var s = a;
            s.time = a.time + (b.time - a.time) * t;
            s.position = Vector3.LerpUnclamped(a.position, b.position, t);
            s.rotation = Quaternion.Slerp(a.rotation, b.rotation, t);
            s.velocity = Vector3.Lerp(a.velocity, b.velocity, t);
            s.angularVelocity = Vector3.Lerp(a.angularVelocity, b.angularVelocity, t);
            s.steer = Mathf.Lerp(a.steer, b.steer, t);
            s.capsizeProgress = Mathf.Lerp(a.capsizeProgress, b.capsizeProgress, t);
            s.boost = Mathf.Lerp(a.boost, b.boost, t);
            s.strokePhase = b.strokeCount == a.strokeCount ? Mathf.Lerp(a.strokePhase, b.strokePhase, t) : a.strokePhase;
            return s;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref time);
            serializer.SerializeValue(ref position);
            uint rot = 0;
            if (serializer.IsWriter) rot = QuaternionCompressor.CompressQuaternion(ref rotation);
            serializer.SerializeValue(ref rot);
            if (serializer.IsReader) { rotation = Quaternion.identity; QuaternionCompressor.DecompressQuaternion(ref rotation, rot); }
            Half3(serializer, ref velocity);
            Half3(serializer, ref angularVelocity);
            byte m = (byte)mode, flags = (byte)((airborne ? FlagAirborne : 0) | (strokeBack ? FlagBack : 0) | (held ? FlagHeld : 0));
            byte phase = Unit(strokePhase), capsize = Unit(capsizeProgress), boostByte = Unit(boost);
            sbyte steerByte = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steer, -1f, 1f) * 127f);
            ushort air = (ushort)Mathf.Clamp(Mathf.RoundToInt(airTime * 100f), 0, ushort.MaxValue);
            serializer.SerializeValue(ref m);
            serializer.SerializeValue(ref flags);
            serializer.SerializeValue(ref phase);
            serializer.SerializeValue(ref capsize);
            serializer.SerializeValue(ref boostByte);
            serializer.SerializeValue(ref steerByte);
            serializer.SerializeValue(ref strokeSide);
            serializer.SerializeValue(ref strokeCount);
            serializer.SerializeValue(ref air);
            if (serializer.IsReader)
            {
                mode = (KayakMode)m;
                airborne = (flags & FlagAirborne) != 0; strokeBack = (flags & FlagBack) != 0; held = (flags & FlagHeld) != 0;
                strokePhase = phase / 255f; capsizeProgress = capsize / 255f; boost = boostByte / 255f;
                steer = steerByte / 127f;
                airTime = air / 100f;
            }
        }

        static byte Unit(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);

        static void Half3<T>(BufferSerializer<T> serializer, ref Vector3 v) where T : IReaderWriter
        {
            ushort x = 0, y = 0, z = 0;
            if (serializer.IsWriter) { x = Mathf.FloatToHalf(v.x); y = Mathf.FloatToHalf(v.y); z = Mathf.FloatToHalf(v.z); }
            serializer.SerializeValue(ref x); serializer.SerializeValue(ref y); serializer.SerializeValue(ref z);
            if (serializer.IsReader) v = new Vector3(Mathf.HalfToFloat(x), Mathf.HalfToFloat(y), Mathf.HalfToFloat(z));
        }

        /// <summary>Payload size in bytes (for the bandwidth estimate in the debug panel).</summary>
        public const int Bytes = 8 + 12 + 4 + 6 + 6 + 9 + 2;
    }
}
