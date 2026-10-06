using System;
using CampanhaRio.Campaign;
using CampanhaRio.Core;
using UnityEngine;

namespace CampanhaRio.Jobs
{
    /// <summary>The rating of a finished job (PLANO_CAMPANHA 4): up to 3 stars, plus gold for Grandma's time.</summary>
    [Serializable]
    public struct JobResult
    {
        public bool completed;
        [Tooltip("Before sunset (always, when completed) + cargo/passenger + time.")]
        public bool sunsetStar, conditionStar, timeStar;
        public bool golden;
        public float time, condition;
        public int pay, reputation;
        public int Stars => (sunsetStar ? 1 : 0) + (conditionStar ? 1 : 0) + (timeStar ? 1 : 0);

        public static JobResult Rate(JobDefinition job, float time, float condition)
        {
            var r = new JobResult { completed = true, sunsetStar = true, time = time, condition = condition };
            r.conditionStar = condition >= job.conditionStar;
            r.timeStar = time <= job.TimeStar;
            r.golden = time <= job.grandmaTime;
            r.pay = job.pay + job.payPerStar * (r.Stars + (r.golden ? 1 : 0));
            r.reputation = job.reputation + (r.Stars == 3 ? 1 : 0);
            return r;
        }
    }

    /// <summary>
    /// Plays one job on a river: puts the job's deadline on the river's sunset rule (RiverChallenge), adds the job's rule
    /// (<see cref="JobModifier"/>), and when at least half the group arrives before sunset rates it (stars, gold, pay),
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
            Challenge.AttemptBegan -= OnAttempt;
            Challenge.AttemptBegan += OnAttempt;
            Challenge.Passed -= OnPassed;
            Challenge.Passed += OnPassed;
            Challenge.Failed -= OnFailed;
            Challenge.Failed += OnFailed;
            Debug.Log($"[Job] {job.id}: '{job.title}' on {Challenge.riverId}, rule {job.rule}, limit {job.timeLimit:0} s, Grandma's time {job.grandmaTime:0} s");
            Challenge.Begin();
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
                      $" (sunset {result.sunsetStar}, cargo {result.conditionStar}, time {result.timeStar}){(result.golden ? " + GOLD (Grandma's time)" : "")}, pay {result.pay}, rep +{result.reputation}");
            CampaignState.Current?.RecordJob(Job.id, true, time, result.Stars, result.golden, result.pay, result.reputation);
            Finished?.Invoke(result);
        }

        void OnFailed()
        {
            Debug.Log($"[Job] {Job.id}: night fell, trying again");
            CampaignState.Current?.RecordJob(Job.id, false, Challenge.Elapsed, 0, false, 0, 0);
        }

        // ---------------------------------------------------------------- graybox HUD
        GUIStyle big, small;

        void OnGUI()
        {
            if (!Job) return;
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            float s = Screen.height / 1080f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

            GUILayout.BeginArea(new Rect(24, 24, 420, 190), GUI.skin.box);
            GUILayout.Label(Loc.T(Job.title), big);
            float left = Mathf.Max(0f, Challenge.EffectiveLimit - Challenge.Elapsed);
            GUILayout.Label(Loc.F("Sunset in {0}", Clock(left)), small);
            GUILayout.Label(Loc.F("Grandma's time: {0}", Clock(Job.grandmaTime)), small);
            if (Rule && !string.IsNullOrEmpty(Rule.Label))
            {
                GUILayout.Label($"{Loc.T(Rule.Label)}: {Loc.T(Rule.State)}", small);
                var bar = GUILayoutUtility.GetRect(380, 14);
                GUI.Box(bar, GUIContent.none);
                GUI.color = Color.Lerp(new Color(0.9f, 0.35f, 0.25f), new Color(0.45f, 0.85f, 0.4f), Rule.Condition);
                GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * Rule.Condition, bar.height - 4), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            GUILayout.EndArea();

            if (Result.HasValue && Time.unscaledTime - resultShownAt < resultTime)
            {
                var r = Result.Value;
                GUILayout.BeginArea(new Rect(1920 / 2 - 260, 300, 520, 260), GUI.skin.box);
                GUILayout.Label(Loc.T("Delivered!"), big);
                GUILayout.Label(new string('★', r.Stars) + new string('☆', 3 - r.Stars) + (r.golden ? "  ✦" : ""), new GUIStyle(big) { fontSize = 44 });
                GUILayout.Label((r.sunsetStar ? "★ " : "☆ ") + Loc.T("Before sunset"), small);
                GUILayout.Label((r.conditionStar ? "★ " : "☆ ") + Loc.T(Job.kind == JobKind.Passenger ? "Happy passenger" : "Cargo in one piece"), small);
                GUILayout.Label((r.timeStar ? "★ " : "☆ ") + Loc.F("Within {0}", Clock(Job.TimeStar)), small);
                if (r.golden) GUILayout.Label("✦ " + Loc.T("Faster than Grandma!"), small);
                GUILayout.Label(Loc.F("+{0} coins, +{1} reputation", r.pay, r.reputation), small);
                GUILayout.EndArea();
            }
            GUI.matrix = m;
        }

        static string Clock(float seconds) => $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";
    }
}
