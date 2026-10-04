using CampanhaRio.Net;
using Unity.Netcode;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// Host: gives every player a body once they have the host's scenes. The first ones appear at the checkpoint's entry;
    /// a friend joining in the middle appears next to the group (beside the host's player).
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [Tooltip("Where the journey starts when no segment entry is loaded yet.")]
        public Vector3 fallbackSpawn = new Vector3(0f, 1f, 6f);
        public float spacing = 2.5f;

        readonly System.Collections.Generic.List<ulong> waiting = new System.Collections.Generic.List<ulong>();

        void OnEnable() => NetSession.PlayerReady += Spawn;
        void OnDisable() => NetSession.PlayerReady -= Spawn;

        void Update()
        {
            // Bodies wait for the first segment (there is no ground before it)
            if (waiting.Count == 0 || !SegmentStreamer.Instance || SegmentStreamer.Instance.LoadedSegments.Count == 0) return;
            var ids = waiting.ToArray();
            waiting.Clear();
            foreach (ulong id in ids) Spawn(id);
        }

        void Spawn(ulong clientId)
        {
            if (SegmentStreamer.Instance && SegmentStreamer.Instance.LoadedSegments.Count == 0) { waiting.Add(clientId); return; }
            var session = NetSession.Instance;
            if (!session || !session.IsHost || !session.Config.playerPrefab) return;
            if (session.Manager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject) return;

            Vector3 at = fallbackSpawn;
            var checkpoint = CampaignState.Current != null ? Segment.Find(CampaignState.Current.Save.checkpoint) : null;
            if (!checkpoint && Segment.Loaded.Count > 0) checkpoint = Segment.Loaded[0];
            if (checkpoint && checkpoint.entry) at = checkpoint.entry.position;
            if (NetworkPlayer.All.Count > 0) at = NetworkPlayer.All[0].transform.position; // join the group where it is
            at += Vector3.right * spacing * NetworkPlayer.All.Count;

            var player = Instantiate(session.Config.playerPrefab, at, Quaternion.identity);
            player.SpawnAsPlayerObject(clientId, true);
            Debug.Log($"[Campanha] Player {clientId} spawned at {at}");
        }
    }
}
