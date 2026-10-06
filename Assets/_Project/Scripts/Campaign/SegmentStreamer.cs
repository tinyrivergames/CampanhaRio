using System;
using System.Collections.Generic;
using CampanhaRio.Dev;
using CampanhaRio.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// Streams the world by segments (Core stays; segments come and go), so it feels open without being open-world.
    /// THE HOST DECIDES, clients follow: the host loads and unloads through Netcode's scene management and every client
    /// does the same automatically.
    ///   - Load: when any player enters the last loaded segment's "load next" zone, the next segment in <see cref="order"/>
    ///     loads (additively, asynchronously), while the group is still in the current one.
    ///   - Unload: a segment is dropped once nobody is in it and every player is in a later segment.
    ///   - Checkpoint: the latest segment that holds every player becomes the campaign checkpoint (saved by the host).
    /// One scene event at a time (Netcode's rule); the next decision waits for the current one to finish.
    /// </summary>
    public class SegmentStreamer : MonoBehaviour
    {
        public static SegmentStreamer Instance { get; private set; }

        [Tooltip("The segments in travel order (scene names, all in the build).")]
        public string[] order = { "Test_A", "Test_B", "Test_C" };
        [Tooltip("Seconds between decisions.")]
        public float checkInterval = 0.25f;

        public event Action<string> SegmentLoaded, SegmentUnloaded;

        readonly List<string> loaded = new List<string>();
        public IReadOnlyList<string> LoadedSegments => loaded;
        string busy;
        float busySince, nextCheck;
        bool started;

        NetworkManager Net => NetSession.Instance ? NetSession.Instance.Manager : null;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Host: start the journey at this segment (the checkpoint).</summary>
        public void Begin(string first)
        {
            if (started || !Net || !Net.IsServer) return;
            started = true;
            Net.SceneManager.OnSceneEvent += OnSceneEvent;
            Load(string.IsNullOrEmpty(first) ? order[0] : first);
        }

        string jumpTo, pinned; // pinned: the jump's target stays loaded until someone is in it

        /// <summary>Host: load this segment now, wherever the group is (a trip back to the agency). Segments left
        /// behind or ahead of everyone unload by themselves.</summary>
        public void JumpTo(string id)
        {
            if (!started) return;
            pinned = id;
            if (loaded.Contains(id)) return;
            if (busy == null) Load(id); else jumpTo = id;
        }

        public bool IsLoaded(string id) => loaded.Contains(id);

        void Update()
        {
            if (!started || busy != null || Time.unscaledTime < nextCheck || !Net || !Net.IsServer) return;
            if (jumpTo != null) { var j = jumpTo; jumpTo = null; Load(j); return; }
            nextCheck = Time.unscaledTime + checkInterval;
            if (NetworkPlayer.All.Count == 0 || loaded.Count == 0) return;

            // Load the next segment when someone reaches the end of a loaded one
            foreach (string id in loaded)
            {
                var seg = Segment.Find(id);
                int index = Array.IndexOf(order, id);
                if (!seg || index < 0 || index + 1 >= order.Length || loaded.Contains(order[index + 1])) continue;
                foreach (var p in NetworkPlayer.All)
                    if (seg.InLoadNextZone(p.transform.position)) { Load(order[index + 1]); return; }
            }

            // Unload a segment behind everyone, or more than one ahead of everyone (after a trip back)
            int minIndex = int.MaxValue, maxIndex = -1;
            foreach (var p in NetworkPlayer.All)
            {
                int i = SegmentIndexOf(p.transform.position);
                if (i < 0) return; // someone is between segments: decide later
                minIndex = Mathf.Min(minIndex, i);
                maxIndex = Mathf.Max(maxIndex, i);
                if (pinned != null && i >= 0 && order[i] == pinned) pinned = null; // arrived
            }
            foreach (string id in loaded)
            {
                int i = Array.IndexOf(order, id);
                if (id != pinned && (i < minIndex || i > maxIndex + 1)) { Unload(id); return; }
            }

            // The checkpoint: the segment where the whole group is
            if (CampaignState.Current != null && minIndex >= 0 && minIndex < order.Length)
                CampaignState.Current.ReachCheckpoint(order[minIndex], minIndex, Array.IndexOf(order, CampaignState.Current.Save.checkpoint));
        }

        int SegmentIndexOf(Vector3 p)
        {
            int best = -1;
            foreach (var s in Segment.Loaded)
                if (s.Contains(p)) best = Mathf.Max(best, Array.IndexOf(order, s.id));
            return best;
        }

        void Load(string id)
        {
            if (loaded.Contains(id)) return;
            var status = Net.SceneManager.LoadScene(id, LoadSceneMode.Additive);
            if (status != SceneEventProgressStatus.Started) { Debug.LogWarning($"[Stream] load {id}: {status}"); return; }
            busy = id;
            busySince = Time.realtimeSinceStartup;
            FrameMonitor.Mark("load " + id);
        }

        void Unload(string id)
        {
            var scene = SceneManager.GetSceneByName(id);
            if (!scene.isLoaded) { loaded.Remove(id); return; }
            var status = Net.SceneManager.UnloadScene(scene);
            if (status != SceneEventProgressStatus.Started) { Debug.LogWarning($"[Stream] unload {id}: {status}"); return; }
            busy = id;
            busySince = Time.realtimeSinceStartup;
            FrameMonitor.Mark("unload " + id);
        }

        void OnSceneEvent(SceneEvent e)
        {
            if (e.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                if (!loaded.Contains(e.SceneName)) loaded.Add(e.SceneName);
                Debug.Log($"[Stream] loaded {e.SceneName} for {e.ClientsThatCompleted.Count} machine(s) in {(Time.realtimeSinceStartup - busySince) * 1000f:0} ms" +
                          (e.ClientsThatTimedOut.Count > 0 ? $", {e.ClientsThatTimedOut.Count} timed out" : "") + $"; loaded: {string.Join(", ", loaded)}");
                busy = null;
                SegmentLoaded?.Invoke(e.SceneName);
            }
            else if (e.SceneEventType == SceneEventType.UnloadEventCompleted)
            {
                loaded.Remove(e.SceneName);
                Debug.Log($"[Stream] unloaded {e.SceneName} in {(Time.realtimeSinceStartup - busySince) * 1000f:0} ms; loaded: {string.Join(", ", loaded)}");
                busy = null;
                SegmentUnloaded?.Invoke(e.SceneName);
            }
        }
    }
}
