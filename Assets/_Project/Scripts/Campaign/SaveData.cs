using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    public enum Medal { None, Bronze, Silver, Gold }

    /// <summary>The HOST's campaign (shared progress of the group). See Docs/SAVE_MODEL.md.</summary>
    [Serializable]
    public class CampaignSave
    {
        public int version = 1;
        public string region = "Floresta";
        [Tooltip("The segment the group restarts from (a scene name).")]
        public string checkpoint = "";
        public List<RiverRecord> rivers = new List<RiverRecord>();
        public List<string> vanStickers = new List<string>();
        public List<string> vanTrophies = new List<string>();
        [Tooltip("The agency's money and reputation (from the jobs).")]
        public int money;
        public int reputation;
        public List<JobRecord> jobs = new List<JobRecord>();
        public string updatedUtc = "";

        public RiverRecord River(string id)
        {
            var r = rivers.Find(x => x.riverId == id);
            if (r == null) { r = new RiverRecord { riverId = id }; rivers.Add(r); }
            return r;
        }

        public JobRecord Job(string id)
        {
            var j = jobs.Find(x => x.jobId == id);
            if (j == null) { j = new JobRecord { jobId = id }; jobs.Add(j); }
            return j;
        }
    }

    /// <summary>One job of the board (a river with a rule): how it went so far.</summary>
    [Serializable]
    public class JobRecord
    {
        public string jobId;
        public int completions;
        [Tooltip("Best rating, 0 to 3 stars; golden = beat Grandma's time.")]
        public int bestStars;
        public bool golden;
        public float bestTime;
        public int attempts;
    }

    [Serializable]
    public class RiverRecord
    {
        public string riverId;
        public bool completed;
        public Medal bestMedal;
        [Tooltip("The group's best time (s), 0 = none yet.")]
        public float bestTime;
        public int attempts;
        public int failures;
    }

    /// <summary>EACH PLAYER's own profile (on their machine): looks and personal bests.</summary>
    [Serializable]
    public class PlayerProfile
    {
        public int version = 1;
        public string playerName = "";
        public int colorIndex;
        public List<string> unlockedCosmetics = new List<string>();
        public List<EquippedCosmetic> equipped = new List<EquippedCosmetic>();
        public List<PersonalBest> personalBests = new List<PersonalBest>();
    }

    [Serializable] public class EquippedCosmetic { public string slot, id; }
    [Serializable] public class PersonalBest { public string riverId; public float bestTime; public Medal bestMedal; }

    /// <summary>
    /// JSON files under persistentDataPath/Saves (or -cc-savedir &lt;dir&gt; for test instances). Writes are atomic: a
    /// temporary file is written and then swapped in, so a crash never leaves half a save.
    /// </summary>
    public static class SaveSystem
    {
        public static string Root
        {
            get
            {
                string dir = Dev.TestSwitches.Value("-cc-savedir");
                return string.IsNullOrEmpty(dir) ? Path.Combine(Application.persistentDataPath, "Saves") : dir;
            }
        }

        public static string CampaignPath(int slot = 0) => Path.Combine(Root, $"campaign_{slot}.json");
        public static string ProfilePath => Path.Combine(Root, "profile.json");

        public static T Load<T>(string path) where T : new()
        {
            try
            {
                if (File.Exists(path)) return JsonUtility.FromJson<T>(File.ReadAllText(path)) ?? new T();
            }
            catch (Exception e) { Debug.LogWarning($"[Save] {path} unreadable, starting fresh ({e.Message})"); }
            return new T();
        }

        public static void Save<T>(string path, T data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
