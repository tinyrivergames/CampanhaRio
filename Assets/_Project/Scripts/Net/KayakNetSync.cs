using System;
using System.Collections;
using System.Collections.Generic;
using CampanhaRio.Dev;
using CampanhaRio.Kayak;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CampanhaRio.Net
{
    /// <summary>
    /// The networked kayak (Stage 9), next to a NetworkObject on the Kayak_Net prefab. Owner authority:
    ///   - The **owner** (a player's machine, or the host for its bots) simulates the kayak exactly as solo and sends a
    ///     compact snapshot every other physics step (25 Hz, unreliable), stamped with the shared clock, plus reliable events
    ///     (impacts, skills, capsize, roll, recovery, takeoff, landing, pickups, a reset's ghosting).
    ///   - Everyone else keeps a **kinematic proxy**, shown <see cref="NetConfig.interpolationDelay"/> in the past between two
    ///     snapshots (a short extrapolation when they are late). The proxy's state (velocity, mode, steer, strokes, air) is
    ///     written into its KayakController, so the paddler, the FX, the audio, drafting and the local collisions just work.
    ///     Events are replayed when the proxy reaches their time, so a splash lands with the kayak.
    /// KayakPhysics knows nothing about any of this.
    /// </summary>
    public class KayakNetSync : NetworkBehaviour
    {
        static readonly List<KayakNetSync> all = new List<KayakNetSync>();
        public static IReadOnlyList<KayakNetSync> All => all;
        public static KayakNetSync ForClient(ulong clientId) => all.Find(s => s && !s.IsBot && s.OwnerClientId == clientId);
        public static KayakNetSync Of(KayakController k) => k ? k.GetComponent<KayakNetSync>() : null;
        /// <summary>The kayak this machine's player drives (null for a host that is only watching).</summary>
        public static KayakNetSync LocalPlayerSync => all.Find(s => s && s.IsLocalPlayerKayak);

        readonly NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>();
        readonly NetworkVariable<Color> hullColor = new NetworkVariable<Color>(Color.red);
        readonly NetworkVariable<bool> isBot = new NetworkVariable<bool>();
        readonly NetworkVariable<byte> botProfile = new NetworkVariable<byte>();
        readonly NetworkVariable<bool> leaving = new NetworkVariable<bool>();
        readonly NetworkVariable<ulong> originalOwner = new NetworkVariable<ulong>();

        /// <summary>The client this kayak was spawned for (it keeps it after they leave, until it fades).</summary>
        public ulong OriginalOwner => originalOwner.Value;

        public KayakController Kayak { get; private set; }
        public string PlayerName => playerName.Value.ToString();
        public Color HullColor => hullColor.Value;
        public bool IsBot => isBot.Value;
        /// <summary>Simulated on this machine (the local player, or one of the host's bots).</summary>
        public bool SimulatedHere { get; private set; }
        public bool IsLocalPlayerKayak => SimulatedHere && !IsBot;

        NetConfig config;
        int stepCounter;

        // ------------------------------------------------------------------ spawn

        /// <summary>Host, before Spawn: who this kayak is.</summary>
        public void InitOnServer(string name, Color color, bool bot, AutopilotProfile profile)
        {
            pendingInit = (name.Length > 29 ? name.Substring(0, 29) : name, color, bot, profile);
            hasPending = true;
        }

        /// <summary>Host: whose kayak this is (set before Spawn).</summary>
        public void SetOriginalOwner(ulong clientId) => pendingOwner = clientId;

        // Written into the NetworkVariables once spawned (writing them before is not allowed)
        (string name, Color color, bool bot, AutopilotProfile profile) pendingInit;
        bool hasPending;
        ulong pendingOwner;

        public override void OnNetworkSpawn()
        {
            if (IsServer && hasPending)
            {
                hasPending = false;
                playerName.Value = new FixedString32Bytes(pendingInit.name);
                hullColor.Value = pendingInit.color;
                isBot.Value = pendingInit.bot;
                botProfile.Value = (byte)pendingInit.profile;
                originalOwner.Value = pendingOwner;
            }
            config = NetConfig.Load();
            Kayak = GetComponent<KayakController>();
            all.Add(this);
            ApplyIdentity();
            playerName.OnValueChanged += (a, b) => ApplyIdentity();
            hullColor.OnValueChanged += (a, b) => ApplyIdentity();
            leaving.OnValueChanged += (a, b) => { if (b) StartCoroutine(FadeOut()); };
            if (IsOwner) ConfigureOwner(); else ConfigureRemote();
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            UnsubscribeOwner();
        }

        /// <summary>Host: a player left (the kayak stays, owned by the server now) or a bot is removed: fade out, then despawn.</summary>
        public void Retire()
        {
            if (!IsServer || leaving.Value) return;
            leaving.Value = true;
            StartCoroutine(DespawnLater(1.3f));
        }

        IEnumerator DespawnLater(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (NetworkObject && NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        }

        void ApplyIdentity()
        {
            string n = PlayerName;
            gameObject.name = IsBot ? $"{n} ({(AutopilotProfile)botProfile.Value})" : n;
            KayakPaint.SetHullColor(Kayak, hullColor.Value);
        }

        void ConfigureOwner()
        {
            SimulatedHere = true;
            Kayak.SetRemote(false);
            Kayak.respawnAtRiverEnd = false;
            var body = GetComponent<Rigidbody>();
            body.maxDepenetrationVelocity = config.maxDepenetration; // a friend's kinematic proxy can't launch us
            body.position = transform.position; body.rotation = transform.rotation; // the spawn pose (the body can lag a frame)
            if (IsBot)
            {
                KayakBotSpawner.ConfigureAsBot(Kayak, (AutopilotProfile)botProfile.Value);
            }
            else
            {
                if (!GetComponent<LocalPlayer>()) gameObject.AddComponent<LocalPlayer>(); // the camera, HUD and input follow it
                var input = GetComponent<KayakInput>();
                if (input) { input.enabled = true; Kayak.input = input; Kayak.ResetInputSource(); }
                if (TestSwitches.LocalBotProfile.HasValue) // an autonomous test instance: the autopilot drives this player
                {
                    var auto = GetComponent<KayakAutopilot>();
                    if (!auto) auto = gameObject.AddComponent<KayakAutopilot>();
                    auto.profile = TestSwitches.LocalBotProfile.Value;
                }
            }
            SubscribeOwner();
        }

        void ConfigureRemote()
        {
            SimulatedHere = false;
            var marker = GetComponent<LocalPlayer>();
            if (marker) Destroy(marker);
            var input = GetComponent<KayakInput>();
            if (input) Destroy(input);
            var auto = GetComponent<KayakAutopilot>();
            if (auto) Destroy(auto);
            Kayak.input = null;
            Kayak.SetRemote(true);
        }

        // ------------------------------------------------------------------ owner: snapshots and events out

        void FixedUpdate()
        {
            if (!IsSpawned) return;
            if (SimulatedHere)
            {
                if (++stepCounter < config.snapshotEverySteps) return;
                stepCounter = 0;
                var snapshot = KayakSnapshot.From(Kayak, NetSession.Instance.Now);
                SnapshotRpc(snapshot);
                Stats.sent++;
            }
            else Interpolate(Time.fixedDeltaTime);
        }

        [Rpc(SendTo.NotMe, Delivery = RpcDelivery.Unreliable)]
        void SnapshotRpc(KayakSnapshot snapshot) { if (!SimulatedHere) Receive(snapshot); }

        void SubscribeOwner()
        {
            Kayak.OnImpact += OwnerImpact;
            Kayak.OnSkill += OwnerSkill;
            Kayak.OnCapsize += OwnerCapsize;
            Kayak.OnRollAttempt += OwnerRoll;
            Kayak.OnRecovered += OwnerRecovered;
            Kayak.OnTakeOff += OwnerTakeOff;
            Kayak.OnLand += OwnerLand;
        }

        void UnsubscribeOwner()
        {
            if (!Kayak) return;
            Kayak.OnImpact -= OwnerImpact;
            Kayak.OnSkill -= OwnerSkill;
            Kayak.OnCapsize -= OwnerCapsize;
            Kayak.OnRollAttempt -= OwnerRoll;
            Kayak.OnRecovered -= OwnerRecovered;
            Kayak.OnTakeOff -= OwnerTakeOff;
            Kayak.OnLand -= OwnerLand;
        }

        double Now => NetSession.Instance ? NetSession.Instance.Now : Time.timeAsDouble;

        void OwnerImpact(ImpactLevel level, float intensity, Vector3 point, Vector3 normal) => ImpactRpc((byte)level, intensity, point, normal, Now);
        void OwnerSkill(SkillEvent e) { if (e.type != SkillType.FastLine) SkillRpc((byte)e.type, e.value, e.meter, Now); } // the fast line trickles: meter only
        void OwnerCapsize() => CapsizeRpc(Now);
        void OwnerRoll(bool success) => RollRpc(success, Now);
        void OwnerRecovered() => RecoveredRpc(Now);
        void OwnerTakeOff() => TakeOffRpc(Now, Kayak.transform.position);
        void OwnerLand(float airTime) => LandRpc(airTime, Kayak.LastLandingImpact, Now);

        [Rpc(SendTo.NotMe)] void ImpactRpc(byte level, float intensity, Vector3 point, Vector3 normal, double t) =>
            Later(t, () => Kayak.RaiseImpact((ImpactLevel)level, intensity, point, normal));
        [Rpc(SendTo.NotMe)] void SkillRpc(byte type, float value, float meter, double t) =>
            Later(t, () => Kayak.RaiseSkill((SkillType)type, value, meter));
        [Rpc(SendTo.NotMe)] void CapsizeRpc(double t) => Later(t, () => Kayak.RaiseCapsize());
        [Rpc(SendTo.NotMe)] void RollRpc(bool success, double t) => Later(t, () => Kayak.RaiseRollAttempt(success));
        [Rpc(SendTo.NotMe)] void RecoveredRpc(double t) => Later(t, () => Kayak.RaiseRecovered());
        [Rpc(SendTo.NotMe)] void TakeOffRpc(double t, Vector3 position) { if (!SimulatedHere) SyncJumps.RemoteTakeOff(Kayak, t, position); }
        [Rpc(SendTo.NotMe)] void LandRpc(float airTime, float impact, double t) =>
            Later(t, () => { Kayak.RaiseLand(impact, airTime); SyncJumps.RemoteLand(Kayak, airTime); });

        // ------------------------------------------------------------------ host: put a kayak somewhere (a new attempt)

        /// <summary>Host: move this kayak (its owner simulates it, so the owner does it), e.g. to the start line.</summary>
        public void PlaceAt(Vector3 position, Quaternion rotation)
        {
            if (SimulatedHere) { Kayak.SetStart(position, rotation); Kayak.Teleport(position, rotation, Vector3.zero); }
            else PlaceRpc(position, rotation);
        }

        [Rpc(SendTo.Owner)]
        void PlaceRpc(Vector3 position, Quaternion rotation)
        {
            Kayak.SetStart(position, rotation);
            Kayak.Teleport(position, rotation, Vector3.zero);
        }

        // ------------------------------------------------------------------ R in a group run: ghosted for a moment

        /// <summary>After a reset onto the river: no collisions with the other kayaks for a moment, and a blink, everywhere.</summary>
        public void Ghost(float seconds)
        {
            ApplyGhost(seconds);
            GhostRpc(seconds);
        }

        [Rpc(SendTo.NotMe)] void GhostRpc(float seconds) => ApplyGhost(seconds);

        Coroutine ghostRoutine;
        void ApplyGhost(float seconds) { if (ghostRoutine != null) StopCoroutine(ghostRoutine); ghostRoutine = StartCoroutine(GhostRoutine(seconds)); }

        IEnumerator GhostRoutine(float seconds)
        {
            var mine = GetComponentsInChildren<Collider>();
            var ignored = new List<(Collider, Collider)>();
            foreach (var other in all)
            {
                if (!other || other == this) continue;
                foreach (var a in mine) foreach (var b in other.GetComponentsInChildren<Collider>()) { Physics.IgnoreCollision(a, b, true); ignored.Add((a, b)); }
            }
            var renderers = new List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>()) if (r.enabled) renderers.Add(r);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                bool on = Mathf.Repeat(t * 8f, 1f) < 0.6f;
                foreach (var r in renderers) if (r) r.enabled = on;
                yield return null;
            }
            foreach (var r in renderers) if (r) r.enabled = true;
            foreach (var (a, b) in ignored) if (a && b) Physics.IgnoreCollision(a, b, false);
        }

        IEnumerator FadeOut()
        {
            // Sink and shrink into the river, then the host despawns it
            var visual = transform.Find("Visual");
            Vector3 scale = visual ? visual.localScale : Vector3.one;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            for (float t = 0f; t < 1.2f; t += Time.deltaTime)
            {
                if (visual) { visual.localScale = scale * Mathf.Lerp(1f, 0.2f, t / 1.2f); visual.localPosition = Vector3.down * (t * 0.5f); }
                yield return null;
            }
            foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        }

        // ------------------------------------------------------------------ remote: the interpolation buffer

        readonly List<KayakSnapshot> buffer = new List<KayakSnapshot>();
        readonly List<(double t, Action a)> pending = new List<(double, Action)>();
        bool placed;
        double newestReceived = double.MinValue;

        /// <summary>The last shown pose's time and how far ahead the buffer ran (s), for the debug panel.</summary>
        public double RenderTime { get; private set; }
        public float BufferAhead { get; private set; }
        public bool Extrapolating { get; private set; }

        void Receive(in KayakSnapshot s)
        {
            Stats.received++;
            if (!Finite(s.position) || !Finite(s.velocity)) return; // never move a proxy to a broken pose
            if (s.time <= RenderTime - 0.5) { Stats.late++; return; } // far too old
            if (buffer.Count > 0 && (s.position - buffer[buffer.Count - 1].position).sqrMagnitude > 100f && s.time > buffer[buffer.Count - 1].time)
                buffer.Clear(); // a teleport (reset, new run): no sliding across the map
            int i = buffer.Count;
            while (i > 0 && buffer[i - 1].time > s.time) i--;
            if (i > 0 && buffer[i - 1].time == s.time) return;
            buffer.Insert(i, s);
            if (s.time > newestReceived) newestReceived = s.time;
            while (buffer.Count > 32) buffer.RemoveAt(0);
        }

        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        void Later(double t, Action a)
        {
            if (SimulatedHere) return;
            pending.Add((t, a));
        }

        void Interpolate(float dt)
        {
            double now = Now;
            double render = now - config.interpolationDelay;
            RenderTime = render;
            DispatchEvents(render);
            if (buffer.Count == 0) return;

            KayakSnapshot s;
            Extrapolating = false;
            int n = buffer.Count;
            if (render <= buffer[0].time) s = buffer[0];
            else if (render >= buffer[n - 1].time)
            {
                // Late: keep going along the last velocity for a moment, then wait
                s = buffer[n - 1];
                float ahead = Mathf.Min((float)(render - s.time), config.maxExtrapolation);
                s.position += s.velocity * ahead;
                Extrapolating = ahead > 0.02f;
            }
            else
            {
                int i = 0;
                while (i < n - 2 && buffer[i + 1].time <= render) i++;
                var a = buffer[i]; var b = buffer[i + 1];
                float t = (float)((render - a.time) / Math.Max(b.time - a.time, 1e-4));
                s = KayakSnapshot.Lerp(a, b, t);
                if (i > 1) buffer.RemoveRange(0, i - 1); // keep one before the pair
            }
            BufferAhead = (float)(newestReceived - render);
            Stats.Sample(BufferAhead, Extrapolating);

            var body = GetComponent<Rigidbody>();
            if (!placed)
            {
                placed = true;
                transform.SetPositionAndRotation(s.position, s.rotation);
                body.position = s.position; body.rotation = s.rotation;
            }
            else { body.MovePosition(s.position); body.MoveRotation(s.rotation); }
            Kayak.ApplyRemote(s.ToState(), s.airborne, s.airTime, s.boost, s.strokeCount, s.strokeBack, dt);
        }

        void DispatchEvents(double render)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].t > render) continue;
                var a = pending[i].a;
                pending.RemoveAt(i--);
                try { a(); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        // ------------------------------------------------------------------ stats for the debug panel and the test logs

        public static class Stats
        {
            public static int sent, received, late;
            static float aheadSum; static int samples, extrapolated;
            public static float AverageAhead => samples > 0 ? aheadSum / samples : 0f;
            public static float ExtrapolatedShare => samples > 0 ? (float)extrapolated / samples : 0f;
            public static void Sample(float ahead, bool extrapolating) { aheadSum += ahead; samples++; if (extrapolating) extrapolated++; }
            public static void ResetWindow() { aheadSum = 0f; samples = 0; extrapolated = 0; }
        }
    }
}
