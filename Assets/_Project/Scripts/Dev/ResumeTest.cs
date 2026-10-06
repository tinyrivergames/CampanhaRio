using System.Collections;
using CampanhaRio.Campaign;
using CampanhaRio.Jobs;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-script resume -cc-savedir &lt;a save from an earlier session&gt;: "close, come back and continue" (Phase 7).
    /// Opens the session again and logs what the agency remembers: coins, reputation, jobs and their stars, upgrades,
    /// the map (and that nothing is announced again), Seu Alce's arrivals, and the board's offers. Then quits.
    /// </summary>
    public class ResumeTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "resume") return;
            DontDestroyOnLoad(new GameObject("Resume Test").AddComponent<ResumeTest>().gameObject);
        }

        IEnumerator Start()
        {
            while (!AgencyFlow.Instance || !AgencyFlow.Instance.IsSpawned || AgencyFlow.Instance.Current != AgencyFlow.Phase.Hub) yield return null;
            yield return new WaitForSeconds(2f);
            var flow = AgencyFlow.Instance;
            var save = CampaignState.Current.Save;
            var board = flow.GetComponent<JobBoard>();
            Debug.Log($"[Test] resume: {save.money} coins, reputation {save.reputation}, arrivals {flow.Arrivals}, upgrades [{string.Join(", ", save.upgrades)}]");
            Debug.Log($"[Test] resume: jobs [{string.Join(", ", save.jobs.ConvertAll(j => $"{j.jobId} x{j.completions} best {j.bestStars}* in {j.bestTime:0.0} s"))}]");
            Debug.Log($"[Test] resume: map mask {flow.MapRevealed} (announced {save.mapSeen}), board offers [{string.Join(", ", board.Offers().ConvertAll(j => j.id))}], segment '{string.Join(", ", SegmentStreamer.Instance.LoadedSegments)}'");
            Application.Quit();
        }
    }
}
