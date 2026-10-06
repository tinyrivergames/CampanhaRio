using System;
using System.Collections.Generic;
using CampanhaRio.Campaign;
using CampanhaRio.Core;
using UnityEngine;

namespace CampanhaRio.Jobs
{
    /// <summary>
    /// The agency's board of orders (PLANO_CAMPANHA 4): 2 to 4 jobs at a time, the ones whose requirements the group has
    /// done, new ones first. The HOST chooses; the choice starts the trip to the river. The board is a graybox panel
    /// (OnGUI) until the real UI: <see cref="Open"/> shows it, a click on "Accept" fires <see cref="Accepted"/>.
    /// </summary>
    public class JobBoard : MonoBehaviour
    {
        public JobCatalog catalog;
        [Range(2, 4)] public int maxOffers = 4;

        public bool IsOpen { get; private set; }

        void Awake() { if (!catalog) catalog = JobCatalog.Load(); }
        public event Action<JobDefinition> Accepted;

        /// <summary>The jobs on the board now.</summary>
        public List<JobDefinition> Offers()
        {
            var save = CampaignState.Current?.Save;
            bool Done(JobDefinition j) => save != null && save.jobs.Exists(r => r.jobId == j.id && r.completions > 0);
            var fresh = new List<JobDefinition>();
            var again = new List<JobDefinition>();
            foreach (var e in catalog ? catalog.entries : new List<JobCatalog.Entry>())
            {
                if (!e.job || !e.requires.TrueForAll(Done)) continue;
                (Done(e.job) ? again : fresh).Add(e.job);
            }
            fresh.AddRange(again); // new orders first, then the ones to play again (for better stars)
            if (fresh.Count > maxOffers) fresh.RemoveRange(maxOffers, fresh.Count - maxOffers);
            return fresh;
        }

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;

        public void Accept(JobDefinition job)
        {
            IsOpen = false;
            Debug.Log($"[Job] board: accepted {job.id}");
            Accepted?.Invoke(job);
        }

        // ---------------------------------------------------------------- graybox panel
        GUIStyle title, text;

        void OnGUI()
        {
            if (!IsOpen) return;
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            text ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            float s = Screen.height / 1080f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            var offers = Offers();
            float w = 1100f, h = 140f + 150f * offers.Count;
            GUILayout.BeginArea(new Rect(1920 / 2 - w / 2, 1080 / 2 - h / 2, w, h), GUI.skin.box);
            GUILayout.Label(Loc.T("Orders board"), title);
            var save = CampaignState.Current?.Save;
            if (save != null) GUILayout.Label(Loc.F("{0} coins · reputation {1}", save.money, save.reputation), text);
            foreach (var job in offers)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label(Loc.T(job.title), title);
                GUILayout.Label($"{Loc.T(job.client)} — {Loc.T(job.description)}", text);
                var rec = save?.jobs.Find(r => r.jobId == job.id);
                string best = rec != null && rec.completions > 0 ? "  " + new string('★', rec.bestStars) : "  " + Loc.T("New!");
                GUILayout.Label(Loc.F("{0} · pays {1}", Loc.T(job.riverId.Replace('_', ' ')), job.pay) + best, text);
                GUILayout.EndVertical();
                if (GUILayout.Button(Loc.T("Accept"), GUILayout.Width(160), GUILayout.Height(60))) Accept(job);
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button(Loc.T("Close"), GUILayout.Height(36))) Close();
            GUILayout.EndArea();
            GUI.matrix = m;
        }
    }
}
