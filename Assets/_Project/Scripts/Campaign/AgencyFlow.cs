using System.Collections;
using System.Collections.Generic;
using CampanhaRio.CameraSystem;
using CampanhaRio.Core;
using CampanhaRio.Jobs;
using CampanhaRio.Kayak;
using CampanhaRio.Net;
using CampanhaRio.Rendering;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// The vertical slice's loop (PLANO_CAMPANHA 4), run by the HOST and shown to everyone:
    ///   Hub        at the agency; the host picks a job at the orders board (E)
    ///   Boarding   everyone to the van (E next to it)
    ///   Driving    Seu Alce drives the road down to the river's put-in (the segments stream in as the van goes)
    ///   Kayaking   a kayak for each player at the start line; the job runs on the river (its rule, the sunset)
    ///   Arrived    the rating, then everyone goes ashore at Vila do Moinho, where the van already is
    ///   Returning  everyone in the van (E): the screen fades and the group is back at the agency
    /// Clients get the phase, the message, the job HUD and result, the sky's time of day, the fades and the goat.
    /// </summary>
    public class AgencyFlow : NetworkBehaviour
    {
        public enum Phase : byte { Starting, Hub, Boarding, Driving, Kayaking, Arrived, ReturnBoarding, Returning }

        public static AgencyFlow Instance { get; private set; }

        [Tooltip("How close (m) a player must be to the board or the van to use it.")]
        public float reach = 5f;
        public float resultTime = 7f;

        readonly NetworkVariable<Phase> phase = new NetworkVariable<Phase>();
        readonly NetworkVariable<FixedString128Bytes> message = new NetworkVariable<FixedString128Bytes>();
        readonly NetworkVariable<JobHudState> hud = new NetworkVariable<JobHudState>();
        readonly NetworkVariable<JobResult> result = new NetworkVariable<JobResult>();
        readonly NetworkVariable<bool> showResult = new NetworkVariable<bool>();
        readonly NetworkVariable<float> dayProgress = new NetworkVariable<float>(DayCycle.Afternoon);
        readonly NetworkVariable<float> fade = new NetworkVariable<float>();
        readonly NetworkVariable<GoatNet> goat = new NetworkVariable<GoatNet>();
        readonly NetworkVariable<int> arrivals = new NetworkVariable<int>();

        public Phase Current => phase.Value;
        /// <summary>How many times the group arrived at the end of a river (Seu Alce's jokes go in this order).</summary>
        public int Arrivals => arrivals.Value;
        public string Message => message.Value.ToString();
        public JobDefinition Job { get; private set; }
        public JobRun Run { get; private set; }
        /// <summary>Host: completed jobs this session (tests read it).</summary>
        public int JobsDone { get; private set; }

        JobBoard board;
        DayCycle day;
        readonly HashSet<ulong> ready = new HashSet<ulong>();

        void Awake() => Instance = this;
        public override void OnDestroy() { if (Instance == this) Instance = null; base.OnDestroy(); }

        public override void OnNetworkSpawn()
        {
            board = GetComponent<JobBoard>();
            if (!IsServer) return;
            board.Accepted += Accept;
            NetworkPlayer.Interacted += OnInteract;
            if (CampaignState.Current != null) arrivals.Value = CampaignState.Current.Save.arrivals;
            StartCoroutine(Begin());
        }

        public override void OnNetworkDespawn()
        {
            if (board) board.Accepted -= Accept;
            NetworkPlayer.Interacted -= OnInteract;
        }

        // ================================================================ host

        IEnumerator Begin()
        {
            while (!WorldMarker.Find(WorldMarker.Kind.VanStopAgency)) yield return null; // the agency is loaded
            ParkVanAt(WorldMarker.Kind.VanStopAgency);
            ToHub();
        }

        void ToHub()
        {
            phase.Value = Phase.Hub;
            Say("Pick an order at the board (E)");
        }

        void Say(string english) => message.Value = new FixedString128Bytes(english);

        void OnInteract(NetworkPlayer player)
        {
            var p = player.transform.position;
            switch (phase.Value)
            {
                case Phase.Hub:
                    var spot = WorldMarker.Find(WorldMarker.Kind.JobBoard);
                    if (!spot || (spot.transform.position - p).sqrMagnitude > reach * reach) return;
                    if (player.OwnerClientId == NetworkManager.ServerClientId) board.Open();
                    else Say("The host picks the order at the board");
                    break;
                case Phase.Boarding:
                case Phase.ReturnBoarding:
                    if (!ValleyVan.Instance || (ValleyVan.Instance.transform.position - p).sqrMagnitude > reach * reach * 2f) return;
                    ready.Add(player.OwnerClientId);
                    player.SetMode(NetworkPlayer.PlayerMode.Van, IndexOf(player));
                    if (ready.Count >= NetworkPlayer.All.Count) StartCoroutine(phase.Value == Phase.Boarding ? DriveToRiver() : ReturnToAgency());
                    break;
            }
        }

        static int IndexOf(NetworkPlayer p)
        {
            for (int i = 0; i < NetworkPlayer.All.Count; i++) if (NetworkPlayer.All[i] == p) return i;
            return 0;
        }

        /// <summary>Host: take this job (the board, or a test).</summary>
        public void Accept(JobDefinition job)
        {
            if (phase.Value != Phase.Hub) return;
            Job = job;
            ready.Clear();
            phase.Value = Phase.Boarding;
            Say("Everyone to the van! (E next to it)");
            Debug.Log($"[Flow] job {job.id} accepted: boarding");
        }

        /// <summary>Host (tests): everyone presses E next to the van.</summary>
        public void BoardEveryone()
        {
            foreach (var p in NetworkPlayer.All)
            {
                if (ValleyVan.Instance) p.Teleport(ValleyVan.Instance.transform.position + ValleyVan.Instance.transform.right * 2.5f);
                OnInteractNear(p);
            }
        }

        void OnInteractNear(NetworkPlayer p)
        {
            ready.Add(p.OwnerClientId);
            p.SetMode(NetworkPlayer.PlayerMode.Van, IndexOf(p));
            if (ready.Count >= NetworkPlayer.All.Count) StartCoroutine(phase.Value == Phase.Boarding ? DriveToRiver() : ReturnToAgency());
        }

        IEnumerator DriveToRiver()
        {
            phase.Value = Phase.Driving;
            Say("Seu Alce drives you down to the river");
            Debug.Log("[Flow] driving to the river");
            var van = ValleyVan.Instance;
            bool done = false;
            var stop = van.transform.position;
            van.Drive(ValleyVan.Points(stop + van.transform.forward * 8f, new Vector3(0f, 37f, -240f), new Vector3(0f, 37f, -132f)), () => done = true);
            while (!done) yield return null;
            // The rest of the road is in the river's segment: wait for it (it streams in as the van comes)
            while (!WorldMarker.Find(WorldMarker.Kind.VanStopPutIn)) yield return null;
            var road = new List<Vector3>();
            foreach (var m in WorldMarker.Road()) road.Add(m.transform.position);
            road.Add(WorldMarker.Find(WorldMarker.Kind.VanStopPutIn).transform.position);
            done = false;
            van.Drive(road, () => done = true);
            while (!done) yield return null;
            yield return Launch();
        }

        IEnumerator Launch()
        {
            var challenge = FindAnyObjectByType<RiverChallenge>();
            Run = challenge.GetComponent<JobRun>();
            Run.drawHud = false;
            var river = challenge.river ? challenge.river : FindAnyObjectByType<River.RiverPath>();
            var s = river.GetPointAtDistance(challenge.startAlong);
            var session = NetSession.Instance;
            for (int i = 0; i < NetworkPlayer.All.Count; i++)
            {
                var p = NetworkPlayer.All[i];
                float side = ((i % 2 == 0) ? 1f : -1f) * 2.2f * ((i + 1) / 2);
                session.SpawnKayak(p.OwnerClientId, new Pose(s.point + s.right * side, Quaternion.LookRotation(s.direction)));
                p.SetMode(NetworkPlayer.PlayerMode.Kayak);
            }
            int bots = Mathf.RoundToInt(Dev.TestSwitches.Number("-cc-bots", 0f)); // test groups: autopilot kayaks owned by the host
            for (int b = 0; b < bots; b++)
            {
                int i = NetworkPlayer.All.Count + b;
                float side = ((i % 2 == 0) ? 1f : -1f) * 2.2f * ((i + 1) / 2);
                session.SpawnKayak(NetworkManager.ServerClientId, new Pose(s.point + s.right * side - s.direction * 4f, Quaternion.LookRotation(s.direction)), bot: true);
            }
            ParkVanAt(WorldMarker.Kind.VanStopArrival); // Seu Alce takes the van round to the village
            yield return new WaitForSeconds(1f); // the kayaks spawn everywhere
            Run.Finished -= Finished;
            Run.Finished += Finished;
            Run.Play(Job);
            phase.Value = Phase.Kayaking;
            Say("");
            Debug.Log($"[Flow] {NetworkPlayer.All.Count} kayak(s) on the river: {Job.id}");
        }

        void Finished(JobResult r)
        {
            JobsDone++;
            result.Value = r;
            showResult.Value = true;
            phase.Value = Phase.Arrived;
            StartCoroutine(GoAshore());
        }

        IEnumerator GoAshore()
        {
            yield return new WaitForSeconds(resultTime);
            showResult.Value = false;
            foreach (var sync in new List<KayakNetSync>(KayakNetSync.All))
                if (sync && sync.NetworkObject.IsSpawned) sync.NetworkObject.Despawn(true);
            Run.End();
            hud.Value = default;
            goat.Value = default;
            var landing = WorldMarker.Find(WorldMarker.Kind.Landing);
            for (int i = 0; i < NetworkPlayer.All.Count; i++)
            {
                var p = NetworkPlayer.All[i];
                p.SetMode(NetworkPlayer.PlayerMode.Walk);
                if (landing) p.Teleport(landing.transform.position + landing.transform.right * (1.6f * i) + Vector3.up);
            }
            if (CampaignState.Current != null) arrivals.Value = CampaignState.Current.CountArrival();
            else arrivals.Value++;
            Debug.Log($"[Flow] arrival {arrivals.Value}: Seu Alce says '{SeuAlce.JokeFor(arrivals.Value - 1)}'");
            ready.Clear();
            phase.Value = Phase.ReturnBoarding;
            Say("Back to the agency: everyone to the van (E)");
            Debug.Log("[Flow] ashore at the village: back to the van");
        }

        IEnumerator ReturnToAgency()
        {
            phase.Value = Phase.Returning;
            for (float t = 0f; t < 1f; t += Time.deltaTime) { fade.Value = t; yield return null; }
            fade.Value = 1f;
            SegmentStreamer.Instance.JumpTo("Agencia");
            while (!WorldMarker.Find(WorldMarker.Kind.VanStopAgency)) yield return null;
            ParkVanAt(WorldMarker.Kind.VanStopAgency);
            var entry = WorldMarker.Find(WorldMarker.Kind.Entry);
            foreach (var m in WorldMarker.All) if (m.kind == WorldMarker.Kind.Entry && m.GetComponentInParent<Segment>()?.id == "Agencia") entry = m;
            for (int i = 0; i < NetworkPlayer.All.Count; i++)
            {
                var p = NetworkPlayer.All[i];
                p.SetMode(NetworkPlayer.PlayerMode.Walk);
                p.Teleport(entry.transform.position + Vector3.right * (1.6f * i));
            }
            if (day) day.progress = DayCycle.Afternoon;
            yield return new WaitForSeconds(0.6f);
            for (float t = 1f; t > 0f; t -= Time.deltaTime) { fade.Value = t; yield return null; }
            fade.Value = 0f;
            Debug.Log("[Flow] back at the agency");
            ToHub();
        }

        void ParkVanAt(WorldMarker.Kind kind)
        {
            var m = WorldMarker.Find(kind);
            if (m && ValleyVan.Instance) ValleyVan.Instance.Park(m.transform.position, m.transform.rotation);
        }

        float nextHud;

        void HostUpdate()
        {
            if (!day) day = FindAnyObjectByType<DayCycle>();
            if (day && Mathf.Abs(day.progress - dayProgress.Value) > 0.002f) dayProgress.Value = day.progress;
            if (Run && Run.Challenge && phase.Value == Phase.Kayaking)
            {
                float black = Run.Challenge.Black;
                if (Mathf.Abs(black - fade.Value) > 0.02f) fade.Value = black;
            }
            if (Time.unscaledTime < nextHud) return;
            nextHud = Time.unscaledTime + 0.1f;
            if (Run && Run.Job && (phase.Value == Phase.Kayaking || phase.Value == Phase.Arrived)) hud.Value = Run.Hud();
            if (Run && Run.Rule is ScaredGoatRule g)
            {
                var carrier = g.Carrier ? KayakNetSync.Of(g.Carrier) : null;
                goat.Value = new GoatNet { active = true, inWater = g.InWater, position = g.GoatPosition, carrier = carrier ? carrier.OwnerClientId : 0 };
            }
        }

        // ================================================================ everyone

        GameObject goatView;
        KayakCamera kayakCamera;
        CoreView coreView;

        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer) HostUpdate();
            else
            {
                if (!day) day = FindAnyObjectByType<DayCycle>();
                if (day) day.progress = dayProgress.Value;
            }
            Cameras();
            if (!IsServer) GoatView();
        }

        void Cameras()
        {
            var cam = Camera.main;
            if (!cam) return;
            if (!coreView) coreView = cam.GetComponent<CoreView>();
            if (!kayakCamera) kayakCamera = cam.GetComponent<KayakCamera>();
            bool kayaking = NetworkPlayer.Local && NetworkPlayer.Local.Mode == NetworkPlayer.PlayerMode.Kayak && KayakRegistry.Local;
            if (coreView) coreView.enabled = !kayaking;
            if (kayakCamera) kayakCamera.enabled = kayaking;
        }

        void GoatView()
        {
            var g = goat.Value;
            if (!g.active) { if (goatView) Destroy(goatView); return; }
            if (!goatView) goatView = ScaredGoatRule.BuildGoat();
            var carrier = g.inWater ? null : KayakNetSync.ForClient(g.carrier);
            if (carrier)
            {
                goatView.transform.SetParent(carrier.transform, false);
                goatView.transform.SetLocalPositionAndRotation(new Vector3(0f, 0.32f, -0.95f), Quaternion.identity);
            }
            else
            {
                goatView.transform.SetParent(null, true);
                goatView.transform.position = Vector3.Lerp(goatView.transform.position, g.position, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }

        GUIStyle note;

        void OnGUI()
        {
            if (!IsSpawned) return;
            JobHud.Draw(hud.Value, showResult.Value ? result.Value : (JobResult?)null);
            string msg = message.Value.ToString();
            if (!string.IsNullOrEmpty(msg) && !board.IsOpen)
            {
                note ??= new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(20 * Screen.height / 1080f), alignment = TextAnchor.MiddleCenter, wordWrap = true };
                float w = Screen.width * 0.42f, h = Screen.height * 0.06f;
                GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height * 0.08f, w, h), Loc.T(msg), note);
            }
            if (fade.Value > 0f)
            {
                GUI.color = new Color(0f, 0f, 0.02f, fade.Value);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
        }

        public struct GoatNet : INetworkSerializable
        {
            public bool active, inWater;
            public Vector3 position;
            public ulong carrier;

            public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
            {
                s.SerializeValue(ref active); s.SerializeValue(ref inWater); s.SerializeValue(ref position); s.SerializeValue(ref carrier);
            }
        }
    }
}
