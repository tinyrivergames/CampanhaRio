using System;
using CampanhaRio.Campaign;
using CampanhaRio.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CampanhaRio.Jobs
{
    /// <summary>The rating of a finished job (PLANO_CAMPANHA 4): up to 3 stars.</summary>
    [Serializable]
    public struct JobResult : INetworkSerializable
    {
        public bool completed;
        [Tooltip("Before sunset (always, when completed) + cargo/passenger + time.")]
        public bool sunsetStar, conditionStar, timeStar;
        public float time, condition;
        public int pay, reputation;
        public int Stars => (sunsetStar ? 1 : 0) + (conditionStar ? 1 : 0) + (timeStar ? 1 : 0);

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref completed); s.SerializeValue(ref sunsetStar); s.SerializeValue(ref conditionStar); s.SerializeValue(ref timeStar);
            s.SerializeValue(ref time); s.SerializeValue(ref condition); s.SerializeValue(ref pay); s.SerializeValue(ref reputation);
        }

        public static JobResult Rate(JobDefinition job, float time, float condition)
        {
            var r = new JobResult { completed = true, sunsetStar = true, time = time, condition = condition };
            r.conditionStar = condition >= job.conditionStar;
            r.timeStar = time <= job.TimeStar;
            r.pay = job.pay + job.payPerStar * r.Stars;
            r.reputation = job.reputation + (r.Stars == 3 ? 1 : 0);
            return r;
        }
    }

    /// <summary>
    /// Plays one job on a river: puts the job's deadline on the river's sunset rule (RiverChallenge), adds the job's rule
    /// (<see cref="JobModifier"/>), and when at least half the group arrives before sunset rates it (stars, pay),
    /// writes it to the campaign save and shows the result. A failed attempt (night) just tries again, as the river rule
    /// does. Host (or solo) only; the HUD is a graybox (OnGUI) until the real UI.
    /// </summary>
    [RequireComponent(typeof(RiverChallenge))]
    public class JobRun : MonoBehaviour
    {
        public static JobRun Current { get; private set; }

        [Tooltip("Seconds the result panel stays up.")]
        public float resultTime = 8f;

        public JobDefinition Job { get; private set; }
        public JobModifier Rule { get; private set; }
        public RiverChallenge Challenge { get; private set; }
        public JobResult? Result { get; private set; }

        /// <summary>The job is done (rated and saved).</summary>
        public event Action<JobResult> Finished;

        float resultShownAt;

        void Awake() => Challenge = GetComponent<RiverChallenge>();
        void OnDestroy() { if (Current == this) Current = null; }

        /// <summary>Start this job on this river (the first attempt starts at once).</summary>
        public void Play(JobDefinition job)
        {
            if (Rule) Destroy(Rule);
            Job = job;
            Result = null;
            Current = this;
            Challenge.timeLimit = job.timeLimit;
            Challenge.startOnPlay = false;
            Rule = JobModifier.Add(gameObject, job.rule);
            Rule.Setup(job, Challenge);
            Challenge.FinishAllowed = () => !Rule || Rule.CanFinish;
            Challenge.AttemptBegan -= OnAttempt;
            Challenge.AttemptBegan += OnAttempt;
            Challenge.Passed -= OnPassed;
            Challenge.Passed += OnPassed;
            Challenge.Failed -= OnFailed;
            Challenge.Failed += OnFailed;
            Debug.Log($"[Job] {job.id}: '{job.title}' on {Challenge.riverId}, rule {job.rule}, limit {job.timeLimit:0} s");
            Challenge.Begin();
        }

        /// <summary>The job is over (the group went ashore): no rule, no HUD.</summary>
        public void End()
        {
            if (Rule) Destroy(Rule);
            Rule = null;
            Job = null;
            Challenge.FinishAllowed = null;
        }

        void OnAttempt() { if (Rule) Rule.OnAttemptBegin(); }

        void FixedUpdate()
        {
            if (Job && Rule && Challenge.Current == RiverChallenge.State.Running) Rule.Tick(Time.fixedDeltaTime);
        }

        void OnPassed(Medal medal, float time)
        {
            var result = JobResult.Rate(Job, time, Rule ? Rule.Condition : 1f);
            Result = result;
            resultShownAt = Time.unscaledTime;
            Debug.Log($"[Job] {Job.id}: DONE in {time:0.0} s, condition {result.condition:0.00} -> {result.Stars} star(s)" +
                      $" (sunset {result.sunsetStar}, cargo {result.conditionStar}, time {result.timeStar}), pay {result.pay}, rep +{result.reputation}");
            CampaignState.Current?.RecordJob(Job.id, true, time, result.Stars, result.pay, result.reputation);
            Finished?.Invoke(result);
        }

        void OnFailed()
        {
            Debug.Log($"[Job] {Job.id}: night fell, trying again");
            CampaignState.Current?.RecordJob(Job.id, false, Challenge.Elapsed, 0, 0, 0);
        }

        // ---------------------------------------------------------------- HUD

        [Tooltip("Draw the HUD here (off when AgencyFlow draws it for everyone).")]
        public bool drawHud = true;

        /// <summary>What the HUD shows now (AgencyFlow sends it to the clients).</summary>
        public JobHudState Hud()
        {
            if (!Job) return default;
            return new JobHudState
            {
                active = true,
                title = new FixedString64Bytes(Job.title),
                label = new FixedString64Bytes(Rule ? Rule.Label : ""),
                state = new FixedString64Bytes(Rule ? Rule.State : ""),
                meterLabel = new FixedString64Bytes(Rule ? Rule.MeterLabel : ""),
                meter = Rule ? Rule.Meter : -1f,
                condition = Rule ? Rule.Condition : 1f,
                timeLeft = Mathf.Max(0f, Challenge.EffectiveLimit - Challenge.Elapsed),
                timeStar = Job.TimeStar,
                passenger = Job.kind == JobKind.Passenger,
            };
        }

        public bool ShowingResult => Result.HasValue && Time.unscaledTime - resultShownAt < resultTime;

        void OnGUI()
        {
            if (drawHud && Job) JobHud.Draw(Hud(), ShowingResult ? Result : null);
        }
    }
}
