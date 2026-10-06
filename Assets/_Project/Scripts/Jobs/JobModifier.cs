using CampanhaRio.Campaign;
using UnityEngine;

namespace CampanhaRio.Jobs
{
    /// <summary>
    /// A job's rule on top of the river (the urgent letter, the scared goat...): it watches the descent and keeps a
    /// CONDITION from 1 (perfect: the letter dry, the goat calm) down to 0. The condition gives the cargo star. A rule can
    /// also hold the group back (the goat in the water: someone has to fetch it). Runs on the host, added by
    /// <see cref="JobRun"/> to the river's RiverChallenge for one job.
    /// </summary>
    public abstract class JobModifier : MonoBehaviour
    {
        public JobDefinition Job { get; private set; }
        public RiverChallenge Challenge { get; private set; }

        /// <summary>1 = perfect, 0 = ruined.</summary>
        public float Condition { get; protected set; } = 1f;
        /// <summary>The HUD line under the timer (English, the Loc key), e.g. "Letter".</summary>
        public abstract string Label { get; }
        /// <summary>A short state for the HUD, e.g. "dry" / "a bit wet" (English, the Loc key).</summary>
        public virtual string State => "";

        public void Setup(JobDefinition job, RiverChallenge challenge)
        {
            Job = job;
            Challenge = challenge;
        }

        /// <summary>A new attempt starts (everyone at the start line).</summary>
        public virtual void OnAttemptBegin() => Condition = 1f;
        /// <summary>Each physics step while the attempt runs.</summary>
        public virtual void Tick(float dt) { }

        public static JobModifier Add(GameObject on, JobRule rule)
        {
            switch (rule)
            {
                default: return on.AddComponent<PlainRule>();
            }
        }
    }

    /// <summary>No rule: the plain sunset race (the condition stays perfect).</summary>
    public class PlainRule : JobModifier
    {
        public override string Label => "";
    }
}
