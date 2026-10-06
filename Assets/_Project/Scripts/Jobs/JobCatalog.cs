using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampanhaRio.Jobs
{
    /// <summary>Every job of the game, with what each needs before it shows up on the board.</summary>
    [CreateAssetMenu(menuName = "CampanhaRio/Job Catalog", fileName = "JobCatalog")]
    public class JobCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public JobDefinition job;
            [Tooltip("Jobs that must be completed first (empty = from the start).")]
            public List<JobDefinition> requires = new List<JobDefinition>();
        }

        public List<Entry> entries = new List<Entry>();

        public JobDefinition Find(string id) => entries.Find(e => e.job && e.job.id == id)?.job;

        /// <summary>The game's catalog (Assets/_Project/Resources/JobCatalog.asset, made by JobsBuilder).</summary>
        public static JobCatalog Load() => Resources.Load<JobCatalog>("JobCatalog");
    }
}
