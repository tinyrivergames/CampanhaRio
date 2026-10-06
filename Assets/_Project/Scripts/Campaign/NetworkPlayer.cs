using System.Collections.Generic;
using CampanhaRio.Dev;
using CampanhaRio.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// A player's body in the world. A GRAYBOX stand-in (a capsule) until the animals exist. Its owner moves it (the
    /// NetworkTransform on the prefab is owner-authoritative); the streamer reads every player's position on the host.
    /// The HOST sets its <see cref="Mode"/>: on foot (WASD, Shift runs, E interacts), riding the van (it sits on its seat)
    /// or kayaking (it follows its own kayak, hidden). Test switch: -cc-script streamwalk walks it forward on its own.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        public enum PlayerMode : byte { Walk, Van, Kayak }

        static readonly List<NetworkPlayer> all = new List<NetworkPlayer>();
        public static IReadOnlyList<NetworkPlayer> All => all;
        public static NetworkPlayer Local { get; private set; }

        public float walkSpeed = 6f;
        [Tooltip("Set by a test script: walk forward (+Z) at this speed (m/s) without input.")]
        public static float ScriptedWalk;

        readonly NetworkVariable<PlayerMode> mode = new NetworkVariable<PlayerMode>();
        readonly NetworkVariable<byte> seat = new NetworkVariable<byte>();
        public PlayerMode Mode => mode.Value;

        /// <summary>Host: this player pressed E (on foot).</summary>
        public static event System.Action<NetworkPlayer> Interacted;

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            all.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            if (IsOwner) Local = this;
            name = $"Player {OwnerClientId}";
            mode.OnValueChanged += (a, b) => ShowBody(b != PlayerMode.Kayak);
            ShowBody(mode.Value != PlayerMode.Kayak);
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            if (Local == this) Local = null;
        }

        /// <summary>Host: on foot, in the van (on this seat) or in a kayak.</summary>
        public void SetMode(PlayerMode m, int seatIndex = 0)
        {
            if (!IsServer) return;
            seat.Value = (byte)seatIndex;
            mode.Value = m;
        }

        /// <summary>Host: move this body (its owner does it).</summary>
        public void Teleport(Vector3 position)
        {
            if (IsOwner) transform.position = position;
            else TeleportRpc(position);
        }

        [Rpc(SendTo.Owner)] void TeleportRpc(Vector3 position) => transform.position = position;
        [Rpc(SendTo.Server)] void InteractRpc() => Interacted?.Invoke(this);

        void ShowBody(bool on)
        {
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = on;
        }

        void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            switch (mode.Value)
            {
                case PlayerMode.Van:
                    if (ValleyVan.Instance) transform.SetPositionAndRotation(ValleyVan.Instance.Seat(seat.Value), ValleyVan.Instance.transform.rotation);
                    return;
                case PlayerMode.Kayak:
                    var mine = KayakNetSync.ForClient(OwnerClientId);
                    if (mine) transform.position = mine.transform.position; // the streamer still knows where we are
                    return;
            }
            Vector3 move = Vector3.zero;
            if (ScriptedWalk > 0f) move = Vector3.forward * ScriptedWalk;
            else if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float z = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                move = new Vector3(x, 0f, z).normalized * walkSpeed * (k.leftShiftKey.isPressed ? 2f : 1f);
                if (k.eKey.wasPressedThisFrame) Interact();
            }
            var p = transform.position + move * Time.deltaTime;
            if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore) && !hit.rigidbody)
                p.y = Mathf.Lerp(p.y, hit.point.y + 1f, 1f - Mathf.Exp(-12f * Time.deltaTime)); // the capsule stands on the ground
            transform.position = p;
            if (move.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), 1f - Mathf.Exp(-10f * Time.deltaTime));
        }

        /// <summary>Owner: interact with what is near (the orders board, the van). Tests call it too.</summary>
        public void Interact() => InteractRpc();
    }
}
