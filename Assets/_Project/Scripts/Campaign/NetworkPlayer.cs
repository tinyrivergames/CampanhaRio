using System.Collections.Generic;
using CampanhaRio.Dev;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// A player's body in the world. A GRAYBOX stand-in (a capsule) until the animals exist: it walks on flat ground and
    /// its owner moves it (the NetworkTransform on the prefab is owner-authoritative). The streamer reads every player's
    /// position on the host. Test switch: -cc-script streamwalk walks it forward on its own.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        static readonly List<NetworkPlayer> all = new List<NetworkPlayer>();
        public static IReadOnlyList<NetworkPlayer> All => all;
        public static NetworkPlayer Local { get; private set; }

        public float walkSpeed = 6f;
        [Tooltip("Set by a test script: walk forward (+Z) at this speed (m/s) without input.")]
        public static float ScriptedWalk;

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            if (IsOwner) Local = this;
            name = $"Player {OwnerClientId}";
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            if (Local == this) Local = null;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            Vector3 move = Vector3.zero;
            if (ScriptedWalk > 0f) move = Vector3.forward * ScriptedWalk;
            else if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float z = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                move = new Vector3(x, 0f, z).normalized * walkSpeed * (k.leftShiftKey.isPressed ? 2f : 1f);
            }
            transform.position += move * Time.deltaTime;
        }
    }
}
