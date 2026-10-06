using System.Collections;
using System.IO;
using CampanhaRio.Campaign;
using CampanhaRio.Jobs;
using CampanhaRio.Kayak;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-script slice (with -cc-host on one instance and -cc-join on others, or solo): plays the vertical slice's loop
    /// end to end, twice (the urgent letter, then the scared goat):
    ///   host: accepts the job at the board, everyone boards the van, the van drives to the put-in, the kayaks run the
    ///         river on the autopilot (the finish moved to 300 m, to keep the test short), the rating, ashore, back in the
    ///         van, back at the agency; then the second job.
    ///   every instance: turns its kayak's autopilot on, logs what it sees (phase, HUD, result, goat) and saves
    ///         screenshots into -cc-savedir (or the save root): hub, van, river, result, village.
    /// Logs "[Test] slice ..." lines and quits after the second job.
    /// </summary>
    public class SliceTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "slice") return;
            DontDestroyOnLoad(new GameObject("Slice Test").AddComponent<SliceTest>().gameObject);
        }

        string who;

        IEnumerator Start()
        {
            while (!AgencyFlow.Instance || !AgencyFlow.Instance.IsSpawned) yield return null;
            var flow = AgencyFlow.Instance;
            who = flow.IsServer ? "host" : "client";
            Debug.Log($"[Test] slice ({who}): flow is up");
            string[] jobs = { "carta_urgente", "bode_medroso" };
            for (int n = 0; n < jobs.Length; n++)
            {
                yield return Until(() => flow.Current == AgencyFlow.Phase.Hub, 120f, "hub");
                if (n == 0) { yield return Until(() => NetworkPlayer.Local, 60f, "my body"); yield return new WaitForSeconds(1f); yield return Shot("1_agencia"); }
                if (flow.IsServer)
                {
                    int players = int.TryParse(TestSwitches.Value("-cc-players", "1"), out int np) ? np : 1;
                    yield return Until(() => NetworkPlayer.All.Count >= players, 90f, $"{players} player(s)");
                    yield return new WaitForSeconds(2f); // (clients settle)
                    var board = flow.GetComponent<JobBoard>();
                    var job = board.catalog.Find(jobs[n]);
                    Debug.Log($"[Test] slice: board offers [{string.Join(", ", board.Offers().ConvertAll(j => j.id))}], taking {job.id}");
                    flow.Accept(job);
                    yield return new WaitForSeconds(1f);
                    flow.BoardEveryone();
                }
                yield return Until(() => flow.Current == AgencyFlow.Phase.Driving, 60f, "driving");
                yield return new WaitForSeconds(4f);
                if (n == 0) yield return Shot("2_van_estrada");
                yield return Until(() => flow.Current == AgencyFlow.Phase.Kayaking, 180f, "kayaking");
                if (flow.IsServer)
                {
                    var challenge = FindAnyObjectByType<RiverChallenge>();
                    challenge.finishAlong = 300f; // a short descent for the test
                }
                yield return Until(() => KayakRegistry.Local, 20f, "my kayak");
                var auto = KayakRegistry.Local.GetComponent<KayakAutopilot>();
                if (auto) auto.SetActive(true);
                Debug.Log($"[Test] slice ({who}): on the river with {KayakRegistry.All.Count} kayak(s), autopilot {(auto ? "on" : "missing")}");
                yield return new WaitForSeconds(12f);
                yield return Shot($"3_rio_{jobs[n]}");
                yield return Until(() => flow.Current == AgencyFlow.Phase.Arrived, 300f, "arrived");
                yield return new WaitForSeconds(1f);
                yield return Shot($"4_resultado_{jobs[n]}");
                Debug.Log($"[Test] slice ({who}): {jobs[n]} arrived");
                yield return Until(() => flow.Current == AgencyFlow.Phase.ReturnBoarding, 30f, "ashore");
                yield return new WaitForSeconds(1.5f);
                Debug.Log($"[Test] slice ({who}): arrival {flow.Arrivals}, Seu Alce: \"{Core.Loc.T(SeuAlce.JokeFor(flow.Arrivals - 1))}\"");
                yield return Shot($"5_vila_chegada_{jobs[n]}");
                if (flow.IsServer) flow.BoardEveryone();
                yield return Until(() => flow.Current == AgencyFlow.Phase.Hub, 90f, "back at the agency");
                Debug.Log($"[Test] slice ({who}): back at the agency after {jobs[n]}");
            }
            if (flow.IsServer)
            {
                var save = CampaignState.Current.Save;
                Debug.Log($"[Test] slice: save -> money {save.money}, reputation {save.reputation}, jobs [{string.Join(", ", save.jobs.ConvertAll(j => $"{j.jobId} x{j.completions} best {j.bestStars}*"))}]");
                yield return new WaitForSeconds(5f); // the clients finish first
            }
            Debug.Log($"[Test] slice ({who}): done");
            Application.Quit();
        }

        IEnumerator Until(System.Func<bool> ok, float seconds, string what)
        {
            float t = Time.realtimeSinceStartup;
            while (!ok())
            {
                if (Time.realtimeSinceStartup - t > seconds)
                {
                    Debug.Log($"[Test] slice ({who}): TIMEOUT waiting for {what} (phase {AgencyFlow.Instance?.Current}, '{AgencyFlow.Instance?.Message}')");
                    Application.Quit();
                    yield break;
                }
                yield return null;
            }
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(SaveSystem.Root, $"slice_{who}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[Test] slice ({who}): shot {path}");
        }
    }
}
