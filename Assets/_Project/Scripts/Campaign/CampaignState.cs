using System;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// The running campaign on the HOST: holds the CampaignSave, changes it (checkpoint, rivers, rewards) and writes it
    /// at once. Clients never write it (their own progress is the PlayerProfile).
    /// </summary>
    public class CampaignState
    {
        public static CampaignState Current { get; private set; }

        public CampaignSave Save { get; private set; }
        public int Slot { get; }
        public event Action Changed;

        CampaignState(int slot) { Slot = slot; }

        /// <summary>Host start: load (or create) the slot's campaign.</summary>
        public static CampaignState Open(int slot = 0)
        {
            var state = new CampaignState(slot) { Save = SaveSystem.Load<CampaignSave>(SaveSystem.CampaignPath(slot)) };
            Current = state;
            Debug.Log($"[Save] campaign slot {slot}: region {state.Save.region}, checkpoint '{state.Save.checkpoint}', {state.Save.rivers.Count} river(s)");
            return state;
        }

        public static void Close() => Current = null;

        /// <summary>The group is all in this segment: it becomes the checkpoint if it is further along.</summary>
        public void ReachCheckpoint(string segment, int segmentIndex, int checkpointIndex)
        {
            if (segment == Save.checkpoint || segmentIndex <= checkpointIndex) return;
            Save.checkpoint = segment;
            Write($"checkpoint {segment}");
        }

        /// <summary>A river attempt ended: passed (with time and medal) or failed.</summary>
        public void RecordRiver(string riverId, bool passed, float time, Medal medal)
        {
            var r = Save.River(riverId);
            r.attempts++;
            if (!passed) r.failures++;
            else
            {
                r.completed = true;
                if (r.bestTime <= 0f || time < r.bestTime) r.bestTime = time;
                if (medal > r.bestMedal) r.bestMedal = medal;
            }
            Write($"river {riverId}: {(passed ? $"passed {time:0.0} s {medal}" : "failed")}");
        }

        /// <summary>A job attempt ended. A completed one pays (money and reputation) every time, the best rating is kept.</summary>
        public void RecordJob(string jobId, bool completed, float time, int stars, int pay, int reputation)
        {
            var j = Save.Job(jobId);
            j.attempts++;
            if (completed)
            {
                j.completions++;
                if (stars > j.bestStars) j.bestStars = stars;
                if (j.bestTime <= 0f || time < j.bestTime) j.bestTime = time;
                Save.money += pay;
                Save.reputation += reputation;
            }
            Write($"job {jobId}: {(completed ? $"done {time:0.0} s, {stars} star(s), +{pay} money, +{reputation} rep" : "failed")}");
        }

        /// <summary>The group arrived at the end of a river: one more for Seu Alce's jokes. Returns the count.</summary>
        public int CountArrival()
        {
            Save.arrivals++;
            Write("arrival " + Save.arrivals);
            return Save.arrivals;
        }

        public void SetMapSeen(int mask)
        {
            if (Save.mapSeen == mask) return;
            Save.mapSeen = mask;
            Write("map " + mask);
        }

        public void BuyUpgrade(string id, int cost)
        {
            if (Save.upgrades.Contains(id)) return;
            Save.money -= cost;
            Save.upgrades.Add(id);
            Write($"upgrade {id} (-{cost} money)");
        }

        public void AddSticker(string id) { if (!Save.vanStickers.Contains(id)) { Save.vanStickers.Add(id); Write("sticker " + id); } }
        public void AddTrophy(string id) { if (!Save.vanTrophies.Contains(id)) { Save.vanTrophies.Add(id); Write("trophy " + id); } }

        void Write(string why)
        {
            Save.updatedUtc = DateTime.UtcNow.ToString("o");
            SaveSystem.Save(SaveSystem.CampaignPath(Slot), Save);
            Debug.Log($"[Save] campaign written ({why}) -> {SaveSystem.CampaignPath(Slot)}");
            Changed?.Invoke();
        }
    }
}
