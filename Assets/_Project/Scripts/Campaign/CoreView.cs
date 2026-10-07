using CampanhaRio.Jobs;
using CampanhaRio.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// Core's camera on foot (graybox UI): it orbits the local player's body. Click in the game to hold the mouse, then
    /// the mouse turns the camera (and the scroll wheel brings it closer or further); Esc lets the mouse go, and any open
    /// panel (the orders board, the upgrades, the room) lets it go too, so it can be clicked. WASD walks the way the
    /// camera looks (<see cref="Yaw"/>). A small debug line shows the session, the loaded segments and the checkpoint.
    /// </summary>
    public class CoreView : MonoBehaviour
    {
        public float distance = 9f;
        public Vector2 distanceRange = new Vector2(4f, 16f);
        public float pitch = 24f;
        public Vector2 pitchRange = new Vector2(-5f, 70f);
        public float mouseSensitivity = 0.12f;
        public float smoothing = 10f;

        /// <summary>Where the camera looks (degrees around Y): the walking direction.</summary>
        public static float Yaw { get; private set; }

        float yaw;
        bool snapped;

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        static bool PanelOpen()
        {
            var flow = AgencyFlow.Instance;
            if (!flow) return false;
            var board = flow.GetComponent<JobBoard>();
            var upgrades = flow.GetComponent<UpgradesPanel>();
            var room = flow.GetComponent<RoomPanel>();
            return (board && board.IsOpen) || (upgrades && upgrades.IsOpen) || (room && room.IsOpen);
        }

        void Update()
        {
            var mouse = Mouse.current;
            var keys = Keyboard.current;
            bool panel = PanelOpen();
            if (panel || (keys != null && keys.escapeKey.wasPressedThisFrame))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame && GUIUtility.hotControl == 0 && Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            bool looking = Cursor.lockState == CursorLockMode.Locked || (mouse != null && mouse.rightButton.isPressed && !panel);
            if (mouse != null && looking)
            {
                var d = mouse.delta.ReadValue() * mouseSensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, pitchRange.x, pitchRange.y);
            }
            if (mouse != null && !panel)
                distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * 0.01f, distanceRange.x, distanceRange.y);
            Yaw = yaw;
        }

        void LateUpdate()
        {
            var target = NetworkPlayer.Local;
            if (!target) return;
            Vector3 pivot = target.transform.position + Vector3.up * 1.2f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 want = pivot - rot * Vector3.forward * distance;
            // Never below the ground (a hill behind the player)
            if (Physics.Raycast(want + Vector3.up * 20f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) && !hit.rigidbody && want.y < hit.point.y + 0.6f)
                want.y = hit.point.y + 0.6f;
            if (!snapped || (transform.position - want).sqrMagnitude > 40f * 40f) { transform.position = want; snapped = true; } // a teleport: no long glide
            else transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(pivot - transform.position);
        }

        void OnGUI()
        {
            var session = NetSession.Instance;
            string net = session ? $"{(session.IsHost ? "host" : "cliente")} - {Core.Loc.T(session.Status)}" : "sem rede";
            string segs = SegmentStreamer.Instance ? string.Join(", ", SegmentStreamer.Instance.LoadedSegments) : "-";
            string cp = CampaignState.Current != null ? CampaignState.Current.Save.checkpoint : "(do host)";
            GUI.Label(new Rect(12, 8, 900, 22), $"CampanhaRio (graybox)   rede: {net}   jogadores: {NetworkPlayer.All.Count}");
            GUI.Label(new Rect(12, 28, 1100, 22), $"trechos carregados: {segs}   checkpoint: {cp}   WASD anda, Shift corre, clique prende o mouse (Esc solta), E usa, M mapa, Tab sala");
        }
    }
}
