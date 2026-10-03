using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace CampanhaRio.Net
{
    /// <summary>
    /// Unity Transport that counts the Netcode payload bytes going out and coming in (for the debug panel and the bandwidth
    /// numbers in the multi-instance test). The UDP/IP and Unity Transport headers (~30–40 bytes per packet) come on top.
    /// </summary>
    public class CountingTransport : UnityTransport
    {
        public long BytesSent { get; private set; }
        public long BytesReceived { get; private set; }
        public int PacketsSent { get; private set; }
        public int PacketsReceived { get; private set; }

        /// <summary>Bytes per second over the last full second (out, in).</summary>
        public float SentPerSecond { get; private set; }
        public float ReceivedPerSecond { get; private set; }

        long windowSent, windowReceived;
        float windowStart;

        void Awake() => OnTransportEvent += CountReceived;

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            BytesSent += payload.Count;
            PacketsSent++;
            base.Send(clientId, payload, networkDelivery);
        }

        void CountReceived(NetworkEvent eventType, ulong clientId, ArraySegment<byte> payload, float receiveTime)
        {
            if (eventType != NetworkEvent.Data) return;
            BytesReceived += payload.Count;
            PacketsReceived++;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now - windowStart < 1f) return;
            float span = now - windowStart;
            SentPerSecond = (BytesSent - windowSent) / span;
            ReceivedPerSecond = (BytesReceived - windowReceived) / span;
            windowSent = BytesSent; windowReceived = BytesReceived; windowStart = now;
        }
    }
}
