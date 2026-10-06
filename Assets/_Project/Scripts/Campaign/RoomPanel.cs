using CampanhaRio.Core;
using CampanhaRio.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// The room panel (Tab), graybox: every session is a room friends can join (the solo game too: it listens on the
    /// network). It shows this machine's address and who is in, and joins a friend's room by IP. Joining reopens the game
    /// connected to that address (-cc-join), so nothing of this session's scenes is left behind. Online rooms with a code
    /// need the Unity Cloud link (NetOnline.Available); until then direct IP (or a virtual LAN) is the way.
    /// </summary>
    public class RoomPanel : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        string address = "";
        string playerName;
        GUIStyle title, text;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) IsOpen = !IsOpen;
        }

        void OnGUI()
        {
            if (!IsOpen) return;
            var session = NetSession.Instance;
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            text ??= new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            float s = Screen.height / 1080f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            GUILayout.BeginArea(new Rect(1920 / 2 - 420, 200, 840, 560), GUI.skin.box);
            GUILayout.Label(Loc.T("Room"), title);
            if (session)
            {
                GUILayout.Label(Loc.T(session.Status), text);
                if (session.IsHost)
                    GUILayout.Label(Loc.F("Friends join with this address: {0}:{1}", NetSession.LocalAddress(), session.Config.port), text);
                GUILayout.Label(Loc.F("Players: {0}", NetworkPlayer.All.Count), text);
                foreach (var info in session.Players.Values) GUILayout.Label("· " + info.name, text);
            }
            GUILayout.Space(10);
            playerName ??= NetSession.PlayerName;
            GUILayout.BeginHorizontal();
            GUILayout.Label(Loc.T("Your name:"), text, GUILayout.Width(140));
            playerName = GUILayout.TextField(playerName, 16, GUILayout.Width(260), GUILayout.Height(34));
            if (GUILayout.Button(Loc.T("Save"), GUILayout.Width(120), GUILayout.Height(34))) NetSession.PlayerName = playerName;
            GUILayout.EndHorizontal();
            GUILayout.Label(Loc.T("(the friends see it from the next time you join a room)"), text);
            GUILayout.Space(16);
            GUILayout.Label(Loc.T("Join a friend's room (their address):"), text);
            GUILayout.BeginHorizontal();
            address = GUILayout.TextField(address, GUILayout.Width(360), GUILayout.Height(34));
            if (GUILayout.Button(Loc.T("Join"), GUILayout.Width(160), GUILayout.Height(34)) && !string.IsNullOrWhiteSpace(address)) Rejoin(address.Trim());
            GUILayout.EndHorizontal();
            if (!NetOnline.Available) GUILayout.Label(Loc.T("Online rooms with a code come later (they need the Unity Cloud link). For now: the same network, or a virtual LAN like Radmin VPN."), text);
            GUILayout.Space(10);
            if (GUILayout.Button(Loc.T("Close") + " (Tab)", GUILayout.Height(34))) IsOpen = false;
            GUILayout.EndArea();
            GUI.matrix = m;
        }

        /// <summary>Reopen the game joined to that address (a clean start as a client).</summary>
        public static void Rejoin(string address)
        {
            if (Application.isEditor) { Debug.Log($"[Campanha] Room: would rejoin {address} (not in the editor)"); return; }
            string exe = System.Environment.GetCommandLineArgs()[0];
            System.Diagnostics.Process.Start(exe, $"-cc-join {address}");
            Application.Quit();
        }
    }
}
