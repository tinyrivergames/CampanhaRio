using System;
using System.Collections;
using System.Collections.Generic;
using CampanhaRio.Kayak;
using CampanhaRio.Rendering;
using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// The river rule: get down before sunset.
    ///   - A sunset timer: while the attempt runs, the DayCycle goes from the afternoon to dusk over the time limit, so the
    ///     sky itself is the clock.
    ///   - Pass: at least HALF the group crosses the finish before the limit. The medal comes from the group's time
    ///     (the moment the half was reached) against the limit.
    ///   - Fail: night falls, the screen goes black, and everyone is back at the river start (not the road, not the van
    ///     event) for another try.
    ///   - A hidden assist: from the 3rd failure on, the limit grows a little each time (never shown, capped).
    /// Runs on the host (or solo). The group is every kayak in KayakRegistry. Results go to the campaign save.
    /// </summary>
    public class RiverChallenge : MonoBehaviour
    {
        public enum State { Idle, Running, Passed, Failed }

        public string riverId = "Rio_Teste";
        public RiverPath river;
        public DayCycle day;
        [Tooltip("Start and finish lines, meters along the river.")]
        public float startAlong = 14f, finishAlong = 1100f;
        [Tooltip("The time limit (s): the sun sets when it runs out.")]
        public float timeLimit = 420f;
        [Tooltip("Day progress at the start and at the limit (0 afternoon .. 0.85 dusk).")]
        public float startProgress = DayCycle.Afternoon, sunsetProgress = DayCycle.Dusk;
        [Tooltip("Medals by the group's time as a share of the limit.")]
        public float goldShare = 0.7f, silverShare = 0.85f;
        [Header("Hidden assist")]
        public int assistAfterFailures = 3;
        public float assistPerFailure = 0.08f, maxAssist = 0.25f;
        [Header("Night fade")]
        public float nightFade = 1.5f, blackHold = 0.8f;
        public bool startOnPlay = true;

        public State Current { get; private set; }
        public int Attempts { get; private set; }
        public int Failures { get; private set; }
        public float Elapsed { get; private set; }
        /// <summary>The limit with the hidden assist applied.</summary>
        public float EffectiveLimit => timeLimit * (1f + Assist);
        public float Assist => Failures < assistAfterFailures ? 0f : Mathf.Min(maxAssist, assistPerFailure * (Failures - assistAfterFailures + 1));

        public event Action<Medal, float> Passed;
        public event Action Failed;

        readonly Dictionary<KayakController, float> finished = new Dictionary<KayakController, float>();
        float black;

        void Start()
        {
            if (!river) river = FindAnyObjectByType<RiverPath>();
            if (!day) day = FindAnyObjectByType<DayCycle>();
            if (startOnPlay) Begin();
        }

        /// <summary>A new attempt: everyone back at the start line, afternoon light, the clock at zero.</summary>
        public void Begin()
        {
            Attempts++;
            Elapsed = 0f;
            finished.Clear();
            PlaceGroupAtStart();
            if (day) day.progress = startProgress;
            Current = State.Running;
            Debug.Log($"[River] {riverId}: attempt {Attempts} (limit {EffectiveLimit:0.0} s{(Assist > 0f ? $", assist +{Assist * 100f:0}%" : "")})");
        }

        void PlaceGroupAtStart()
        {
            if (!river) return;
            var s = river.GetPointAtDistance(startAlong);
            int i = 0;
            foreach (var k in KayakRegistry.All)
            {
                float side = ((i % 2 == 0) ? 1f : -1f) * 2.2f * ((i + 1) / 2);
                var pose = new Pose(s.point + s.right * side, Quaternion.LookRotation(s.direction));
                k.SetStart(pose.position, pose.rotation);
                k.Teleport(pose.position, pose.rotation, Vector3.zero);
                i++;
            }
        }

        void FixedUpdate()
        {
            if (Current != State.Running) return;
            Elapsed += Time.fixedDeltaTime;
            if (day) day.progress = Mathf.Lerp(startProgress, sunsetProgress, Elapsed / EffectiveLimit);

            int group = 0;
            foreach (var k in KayakRegistry.All)
            {
                group++;
                if (!finished.ContainsKey(k) && k.RiverSample.distanceAlong >= finishAlong) finished[k] = Elapsed;
            }
            if (group > 0 && finished.Count * 2 >= group) { Pass(); return; }
            if (Elapsed >= EffectiveLimit) StartCoroutine(Fail());
        }

        void Pass()
        {
            Current = State.Passed;
            float share = Elapsed / timeLimit; // medals against the real limit (the assist never buys a medal)
            var medal = share <= goldShare ? Medal.Gold : share <= silverShare ? Medal.Silver : share <= 1f ? Medal.Bronze : Medal.None;
            Debug.Log($"[River] {riverId}: PASSED in {Elapsed:0.0} s ({finished.Count} finished), medal {medal}, day progress {(day ? day.progress : 0f):0.00}");
            CampaignState.Current?.RecordRiver(riverId, true, Elapsed, medal);
            Passed?.Invoke(medal, Elapsed);
        }

        IEnumerator Fail()
        {
            Current = State.Failed;
            Failures++;
            Debug.Log($"[River] {riverId}: FAILED at sunset ({finished.Count}/{KayakRegistry.All.Count} finished), failures {Failures}");
            CampaignState.Current?.RecordRiver(riverId, false, Elapsed, Medal.None);
            Failed?.Invoke();
            // Night falls and the screen goes black (nobody paddles in the dark)
            float from = day ? day.progress : 0f;
            for (float t = 0f; t < nightFade; t += Time.unscaledDeltaTime)
            {
                float k = t / nightFade;
                if (day) day.progress = Mathf.Lerp(from, DayCycle.Night, k);
                black = k;
                yield return null;
            }
            black = 1f;
            yield return new WaitForSecondsRealtime(blackHold);
            Begin(); // back at the river start, afternoon again
            for (float t = 0f; t < nightFade * 0.6f; t += Time.unscaledDeltaTime) { black = 1f - t / (nightFade * 0.6f); yield return null; }
            black = 0f;
        }

        void OnGUI()
        {
            if (black <= 0f) return;
            GUI.color = new Color(0f, 0f, 0.02f, black);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
