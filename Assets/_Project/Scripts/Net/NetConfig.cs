using Unity.Netcode;
using UnityEngine;

namespace CampanhaRio.Net
{
    /// <summary>
    /// Multiplayer settings and prefabs, loaded from Resources/NetConfig (CampanhaRio > Setup > Build Net Setup creates it).
    /// Nothing here is used solo.
    /// </summary>
    [CreateAssetMenu(menuName = "CampanhaRio/Net Config", fileName = "NetConfig")]
    public class NetConfig : ScriptableObject
    {
        [Tooltip("The networked kayak (the Kayak prefab + NetworkObject + KayakNetSync), used for players and the host's bots.")]
        public NetworkObject kayakPrefab;
        [Tooltip("The player's body in the world (a graybox stand-in until the animals exist), spawned for each player.")]
        public NetworkObject playerPrefab;
        [Tooltip("Other networked prefabs the game spawns (segment props, the van...).")]
        public NetworkObject[] extraPrefabs = new NetworkObject[0];

        [Header("Connection")]
        public ushort port = 7777;
        [Tooltip("Players per room (host included). Nothing assumes 4.")]
        [Min(2)] public int maxPlayers = 4;
        [Tooltip("Bumped whenever the network messages change: a different version can't join.")]
        public string protocol = "cr-net-1";
        [Tooltip("Seconds to wait for a direct connection before giving up.")]
        public float connectTimeout = 8f;

        [Header("Kayak sync")]
        [Tooltip("Snapshots per second each owner sends (one every N physics steps: 50 Hz / 2 = 25 Hz).")]
        [Range(1, 5)] public int snapshotEverySteps = 2;
        [Tooltip("Remote kayaks are shown this far in the past (s), so there is always a snapshot on each side.")]
        public float interpolationDelay = 0.1f;
        [Tooltip("When snapshots are late, keep moving along the last velocity for at most this long (s).")]
        public float maxExtrapolation = 0.25f;
        [Tooltip("The local kayak is pushed out of a friend's (kinematic) proxy at most this fast (m/s): no explosions.")]
        public float maxDepenetration = 2.5f;

        static NetConfig loaded;
        public static NetConfig Load() => loaded ? loaded : loaded = Resources.Load<NetConfig>("NetConfig");
    }
}
