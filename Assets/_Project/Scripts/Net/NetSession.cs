using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using CampanhaRio.Core;
using CampanhaRio.Dev;
using CampanhaRio.Kayak;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampanhaRio.Net
{
    /// <summary>A player in the room (the host keeps the list).</summary>
    public class PlayerInfo
    {
        public ulong clientId;
        public string name;
        public Color color;
    }

    /// <summary>
    /// The multiplayer bootstrap. Lives across scenes (DontDestroyOnLoad) with the NetworkManager and the transport.
    ///   - Host / Join, directly (IP:port) or online (a join code, once the project is linked: see NetOnline).
    ///   - The connection check: the protocol version and the room size, with friendly messages for everything that fails.
    ///   - Netcode's scene management is on, and clients synchronize ADDITIVELY: everyone boots into Core, and the host's
    ///     segments stream in on top of it (see SegmentStreamer). The host decides; clients follow.
    ///   - The shared clock (<see cref="Now"/>): the same moment on every machine.
    ///   - <see cref="PlayerReady"/> (host only): a player has every scene the host has; the game spawns what it needs.
    /// </summary>
    public class NetSession : MonoBehaviour
    {
        public static NetSession Instance { get; private set; }
        /// <summary>A session is running (host or connected client).</summary>
        public static bool Active => Instance && Instance.Manager && (Instance.Manager.IsServer || Instance.Manager.IsClient) && Instance.inSession;

        public NetworkManager Manager { get; private set; }
        public CountingTransport Transport { get; private set; }
        public NetConfig Config { get; private set; }
        public bool IsHost => Manager && Manager.IsServer;
        /// <summary>Why the last attempt failed or the session ended (shown once by the menu).</summary>
        public static string LastMessage;
        public string Status { get; private set; } = "";
        /// <summary>The join code of an online room (empty for direct).</summary>
        public string JoinCode { get; internal set; } = "";

        /// <summary>Host: this player has the host's scenes now (the host itself right after starting, others after syncing).</summary>
        public static event Action<ulong> PlayerReady;
        /// <summary>Host: this player left (or crashed).</summary>
        public static event Action<ulong> PlayerLeft;
        /// <summary>The session ended on this machine (left, closed, refused, dropped), with the reason for the player.</summary>
        public static event Action<string> Ended;

        readonly Dictionary<ulong, PlayerInfo> players = new Dictionary<ulong, PlayerInfo>();
        public IReadOnlyDictionary<ulong, PlayerInfo> Players => players;
        bool inSession, leaving;
        Coroutine timeout;

        // ------------------------------------------------------------------ the local player's settings (saved locally)

        /// <summary>A name / colour for this run of the program only (test instances), never saved over the player's own.</summary>
        public static string NameOverride;
        public static int ColorOverride = -1;

        public static string PlayerName
        {
            get
            {
                if (!string.IsNullOrEmpty(NameOverride)) return NameOverride;
                string n = PlayerPrefs.GetString("cr.name", "");
                if (string.IsNullOrWhiteSpace(n)) { n = "Amigo " + UnityEngine.Random.Range(10, 99); PlayerPrefs.SetString("cr.name", n); }
                return n;
            }
            set => PlayerPrefs.SetString("cr.name", string.IsNullOrWhiteSpace(value) ? "Amigo" : value.Trim().Substring(0, Math.Min(value.Trim().Length, 16)));
        }

        public static int ColorIndex { get => PlayerPrefs.GetInt("cr.color", 0); set => PlayerPrefs.SetInt("cr.color", value); }

        /// <summary>The four player colours (placeholders until the cosmetics exist).</summary>
        public static readonly Color[] Palette =
        {
            new Color(0.85f, 0.21f, 0.16f), new Color(0.95f, 0.66f, 0.23f), new Color(0.25f, 0.62f, 0.56f), new Color(0.31f, 0.49f, 0.76f),
        };

        public static Color PlayerColor { get { int i = ColorOverride >= 0 ? ColorOverride : ColorIndex; return Palette[Mathf.Abs(i) % Palette.Length]; } }

        // ------------------------------------------------------------------ bootstrap

        public static NetSession Ensure()
        {
            if (Instance) return Instance;
            var config = NetConfig.Load();
            if (!config) { Debug.LogError("[Campanha] No Resources/NetConfig (run CampanhaRio > Setup > Build Net Setup)."); return null; }
            var go = new GameObject("Net Session");
            DontDestroyOnLoad(go);
            go.SetActive(false);
            var session = go.AddComponent<NetSession>();
            session.Config = config;
            session.Transport = go.AddComponent<CountingTransport>();
            // A crashed player (no goodbye) is dropped after 6 s of silence, not the default 30 s
            session.Transport.DisconnectTimeoutMS = 6000;
            session.Transport.HeartbeatTimeoutMS = 500;
            session.Manager = go.AddComponent<NetworkManager>();
            session.Manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = session.Transport,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                TickRate = 30,
                ClientConnectionBufferTimeout = 15,
            };
            go.SetActive(true);
            if (config.kayakPrefab) session.Manager.AddNetworkPrefab(config.kayakPrefab.gameObject);
            if (config.playerPrefab) session.Manager.AddNetworkPrefab(config.playerPrefab.gameObject);
            foreach (var p in config.extraPrefabs) if (p) session.Manager.AddNetworkPrefab(p.gameObject);
            return session;
        }

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>The shared clock: the server's time now, the same moment on every machine (Netcode's local time minus its buffer).</summary>
        public double Now
        {
            get
            {
                if (!Manager || Manager.NetworkTimeSystem == null) return Time.unscaledTimeAsDouble;
                var ts = Manager.NetworkTimeSystem;
                return Manager.IsServer ? ts.LocalTime : ts.LocalTime - ts.LocalBufferSec;
            }
        }

        public ulong LocalClientId => Manager ? Manager.LocalClientId : 0;

        // ------------------------------------------------------------------ host / join (direct)

        public bool HostDirect()
        {
            Prepare();
            Transport.SetConnectionData("127.0.0.1", Config.port, "0.0.0.0");
            JoinCode = "";
            Status = "Opening the room…";
            AddHostPlayer();
            if (!Manager.StartHost()) { Fail("Couldn't open the room: is the port already in use (another host on this PC)?"); return false; }
            BeginSession();
            Status = Loc.F("Room open on {0}:{1}", LocalAddress(), Config.port);
            PlayerReady?.Invoke(NetworkManager.ServerClientId);
            return true;
        }

        public bool JoinDirect(string address)
        {
            Prepare();
            address = string.IsNullOrWhiteSpace(address) ? "127.0.0.1" : address.Trim();
            ushort port = Config.port;
            int colon = address.LastIndexOf(':');
            if (colon > 0 && ushort.TryParse(address.Substring(colon + 1), out ushort p)) { port = p; address = address.Substring(0, colon); }
            if (!System.Net.IPAddress.TryParse(address, out _)) { Fail(Loc.F("\"{0}\" isn't an IP address (like 192.168.0.12 or 100.64.1.5).", address)); return false; }
            Transport.SetConnectionData(address, port);
            JoinCode = "";
            string where = $"{address}:{port}";
            Status = Loc.F("Connecting to {0}…", where);
            if (!Manager.StartClient()) { Fail("Couldn't start the connection."); return false; }
            BeginSession();
            if (timeout != null) StopCoroutine(timeout);
            timeout = StartCoroutine(ConnectTimeout(where));
            return true;
        }

        IEnumerator ConnectTimeout(string where)
        {
            float t = 0f;
            while (t < Config.connectTimeout && Manager && !Manager.IsConnectedClient) { t += Time.unscaledDeltaTime; yield return null; }
            if (Manager && !Manager.IsConnectedClient && inSession)
                Leave(Loc.F("Couldn't reach the host at {0} (timed out). Check the address and the host's firewall.", where));
        }

        // ------------------------------------------------------------------ host / join (online, through NetOnline)

        /// <summary>Before the Sessions API starts Netcode for us: the same payload and callbacks as a direct connection.</summary>
        internal void PrepareOnline()
        {
            Prepare();
            AddHostPlayer();
            Status = "Connecting to Unity's online services…";
        }

        /// <summary>The Sessions API started the host (or the client).</summary>
        internal void OnlineStarted(bool host, string code)
        {
            JoinCode = code;
            BeginSession();
            if (host) { Status = $"Online room: {code}"; PlayerReady?.Invoke(NetworkManager.ServerClientId); }
            else Status = "Connected";
        }

        void AddHostPlayer()
        {
            players.Clear();
            players[NetworkManager.ServerClientId] = new PlayerInfo { clientId = NetworkManager.ServerClientId, name = PlayerName, color = PlayerColor };
        }

        /// <summary>Settings shared by host and join: the connection payload (version, name, colour) and the callbacks.</summary>
        void Prepare()
        {
            if (Manager.IsListening) Manager.Shutdown();
            LastMessage = null;
            var payload = new ConnectPayload { protocol = Config.protocol, version = Application.version, name = PlayerName, color = PlayerColor };
            Manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            Manager.ConnectionApprovalCallback = Approve;
            Manager.OnClientConnectedCallback -= OnClientConnected; Manager.OnClientConnectedCallback += OnClientConnected;
            Manager.OnClientDisconnectCallback -= OnClientDisconnected; Manager.OnClientDisconnectCallback += OnClientDisconnected;
            Manager.OnTransportFailure -= OnTransportFailure; Manager.OnTransportFailure += OnTransportFailure;
            Transport.ConnectTimeoutMS = 1000;
            Transport.MaxConnectAttempts = Mathf.Max(1, Mathf.RoundToInt(Config.connectTimeout));
        }

        [Serializable]
        struct ConnectPayload { public string protocol, version, name; public Color color; }

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false; // the game spawns bodies on PlayerReady
            if (request.ClientNetworkId == NetworkManager.ServerClientId) { response.Approved = true; return; }
            ConnectPayload p;
            try { p = JsonUtility.FromJson<ConnectPayload>(Encoding.UTF8.GetString(request.Payload)); }
            catch { p = default; }
            if (p.protocol != Config.protocol)
            {
                response.Approved = false;
                response.Reason = Loc.F("Version mismatch: the host runs {0} (build {1}), you run {2} (build {3}). Update both to the same build.",
                    Config.protocol, Application.version, string.IsNullOrEmpty(p.protocol) ? "?" : p.protocol, p.version);
                return;
            }
            if (players.Count >= Config.maxPlayers)
            {
                response.Approved = false;
                response.Reason = Loc.F("The room is full ({0}/{1}).", players.Count, Config.maxPlayers);
                return;
            }
            players[request.ClientNetworkId] = new PlayerInfo { clientId = request.ClientNetworkId, name = Unique(p.name), color = p.color.a > 0f ? p.color : PlayerColor };
            response.Approved = true;
        }

        string Unique(string name)
        {
            name = string.IsNullOrWhiteSpace(name) ? "Amigo" : name.Trim();
            string n = name;
            for (int i = 2; ; i++)
            {
                bool taken = false;
                foreach (var p in players.Values) if (p.name == n) taken = true;
                if (!taken) return n;
                n = $"{name} {i}";
            }
        }

        // ------------------------------------------------------------------ session lifecycle

        void BeginSession()
        {
            inSession = true;
            leaving = false;
            SyncJumps.Clock = () => Now;
            // Everyone boots into Core: clients keep it and add the host's segments on top
            Manager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);
            Manager.SceneManager.OnSynchronizeComplete -= OnSynchronizeComplete; Manager.SceneManager.OnSynchronizeComplete += OnSynchronizeComplete;
        }

        void EndSession()
        {
            inSession = false;
            SyncJumps.Clock = () => Time.fixedTimeAsDouble;
            players.Clear();
            if (timeout != null) { StopCoroutine(timeout); timeout = null; }
        }

        /// <summary>Leave the room (or close it as the host), with an optional message for the player.</summary>
        public void Leave(string message = null)
        {
            if (leaving) return;
            leaving = true;
            Status = "";
            if (message != null) LastMessage = message = Loc.T(message);
            if (Manager && Manager.IsListening) Manager.Shutdown();
            EndSession();
            NetOnline.LeaveSession();
            Ended?.Invoke(message);
        }

        void Fail(string message)
        {
            LastMessage = message = Loc.T(message);
            Status = "";
            if (Manager.IsListening) Manager.Shutdown();
            EndSession();
            Debug.LogWarning("[Campanha] Net: " + message);
            Ended?.Invoke(message);
        }

        /// <summary>Host: a player who joined has every scene the host has now.</summary>
        void OnSynchronizeComplete(ulong clientId)
        {
            if (!Manager.IsServer || clientId == NetworkManager.ServerClientId) return;
            PlayerReady?.Invoke(clientId);
        }

        /// <summary>Host: a networked kayak for a player (or a bot), owned by them and simulated on their machine.</summary>
        public KayakNetSync SpawnKayak(ulong owner, Pose pose, bool bot = false, AutopilotProfile profile = AutopilotProfile.Normal)
        {
            if (!Manager.IsServer || !Config.kayakPrefab) return null;
            players.TryGetValue(owner, out var info);
            var no = Instantiate(Config.kayakPrefab, pose.position, pose.rotation);
            var sync = no.GetComponent<KayakNetSync>();
            string name = bot ? "Bot " + (KayakNetSync.All.Count + 1) : info != null ? info.name : "Amigo";
            sync.InitOnServer(name, info != null ? info.color : PlayerColor, bot, profile);
            sync.SetOriginalOwner(owner);
            no.DontDestroyWithOwner = true; // when a player leaves, the kayak fades out instead of vanishing
            no.SpawnWithOwnership(owner, true);
            return sync;
        }

        // ------------------------------------------------------------------ connection events

        void OnClientConnected(ulong clientId)
        {
            if (clientId == Manager.LocalClientId && !Manager.IsServer)
            {
                if (timeout != null) { StopCoroutine(timeout); timeout = null; }
                Status = "Connected";
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (Manager.IsServer)
            {
                if (clientId == Manager.LocalClientId) return;
                players.Remove(clientId);
                foreach (var sync in new List<KayakNetSync>(KayakNetSync.All))
                    if (sync && !sync.IsBot && sync.OriginalOwner == clientId) sync.Retire();
                PlayerLeft?.Invoke(clientId);
                return;
            }
            // This client was dropped: refused, the host left, or the connection timed out
            if (clientId != Manager.LocalClientId && clientId != NetworkManager.ServerClientId) return;
            string reason = Manager.DisconnectReason;
            bool wasConnected = Status == "Connected";
            if (wasConnected && !string.IsNullOrEmpty(reason) && reason.Contains("shutting down")) reason = null;
            if (!string.IsNullOrEmpty(reason)) Leave(reason);
            else if (wasConnected) Leave("The host closed the room.");
            else Leave("Couldn't connect to the host. Check the address and the host's firewall.");
        }

        void OnTransportFailure() => Leave("The connection failed (network error).");

        /// <summary>This PC's LAN address (what friends type to join directly).</summary>
        public static string LocalAddress()
        {
            try
            {
                foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(ip)) return ip.ToString();
            }
            catch { }
            return "127.0.0.1";
        }
    }
}
