using UnityEngine;

namespace CampanhaRio.Jobs
{
    public enum JobKind { Delivery, Passenger }

    /// <summary>The rule a job puts on top of the river (see <see cref="JobModifier"/>).</summary>
    public enum JobRule { None, UrgentLetter, ScaredGoat }

    /// <summary>
    /// One order on the agency's board (PLANO_CAMPANHA 3 and 4): a river, the job's rule on top of the same kayak physics,
    /// the deadline (the sunset), Grandma's time carved at the finish, and what it pays. The same river with another job
    /// is another descent. Data only: <see cref="JobRun"/> plays it. Assets: Assets/_Project/Data/Jobs (JobsBuilder).
    /// </summary>
    [CreateAssetMenu(menuName = "CampanhaRio/Job", fileName = "Job")]
    public class JobDefinition : ScriptableObject
    {
        [Tooltip("Stable id (save files use it).")]
        public string id = "job";
        [Tooltip("Board title and text, in English (the key for Loc).")]
        public string title = "A job";
        [TextArea] public string description = "";
        [Tooltip("Who asked for it (shown on the board).")]
        public string client = "";
        public JobKind kind = JobKind.Delivery;
        public JobRule rule = JobRule.None;
        [Tooltip("The river (the river scene's RiverChallenge.riverId).")]
        public string riverId = "Rio_Moinho";

        [Header("Time")]
        [Tooltip("Seconds until sunset.")]
        public float timeLimit = 300f;
        [Tooltip("The time star: arrive within this share of the limit.")]
        [Range(0.3f, 1f)] public float timeStarShare = 0.8f;
        [Tooltip("Grandma's time (s), carved on the finish post: beating it gives the golden star.")]
        public float grandmaTime = 180f;

        [Header("Cargo or passenger")]
        [Tooltip("The cargo star: the rule's condition (1 = perfect) must end at or above this.")]
        [Range(0f, 1f)] public float conditionStar = 0.6f;

        [Header("Reward")]
        public int pay = 40;
        [Tooltip("Pay added per star.")]
        public int payPerStar = 10;
        public int reputation = 1;

        public float TimeStar => timeLimit * timeStarShare;
    }
}
