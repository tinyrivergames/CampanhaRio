using System.Collections;
using CampanhaRio.Campaign;
using CampanhaRio.Jobs;
using CampanhaRio.Kayak;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-scene KayakTest -cc-script jobrun [-cc-job carta_urgente] [-cc-savedir dir]: tests the job system on the
    /// KayakTest river (the Rio do Moinho comes in part C), with the player's kayak on the autopilot:
    ///   1. the board offers only the jobs whose requirements are done (a fresh save: the letter only);
    ///   2. the job is accepted; a first attempt with a 10 s limit fails at sunset (night, back to the start);
    ///   3. the next attempt (150 s limit) reaches the finish (300 m): rated, paid, saved;
    ///   4. the board again: the goat now shows up (it needs the letter), the letter stays (to play again).
    /// Runs at 2x time scale. Logs "[Test] jobrun ..." lines and quits.
    /// </summary>
    public class JobRunTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "jobrun") return;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) =>
            {
                var challenge = FindAnyObjectByType<RiverChallenge>();
                if (challenge && !FindAnyObjectByType<JobRunTest>()) challenge.gameObject.AddComponent<JobRunTest>();
            };
        }

        void Awake() => GetComponent<RiverChallenge>().startOnPlay = false; // the job starts the attempts

        IEnumerator Start()
        {
            var challenge = GetComponent<RiverChallenge>();
            CampaignState.Open();
            var board = gameObject.AddComponent<JobBoard>();
            var run = gameObject.AddComponent<JobRun>();
            yield return null;
            Debug.Log($"[Test] jobrun: board offers [{string.Join(", ", board.Offers().ConvertAll(j => j.id))}] (money {CampaignState.Current.Save.money})");

            var job = board.catalog.Find(TestSwitches.Value("-cc-job", "carta_urgente"));
            JobDefinition accepted = null;
            board.Accepted += j => accepted = j;
            board.Accept(job);
            // A test copy: a deadline the autopilot can just miss, then one it can make
            var test = Instantiate(accepted);
            test.timeLimit = 10f;

            Time.timeScale = 2f;
            challenge.finishAlong = 300f;
            var auto = KayakRegistry.Local.GetComponent<KayakAutopilot>();
            int fails = 0;
            challenge.Failed += () => fails++;
            JobResult? result = null;
            run.Finished += r => result = r;
            run.Play(test);
            auto.SetActive(true);
            while (fails < 1) yield return null;
            test.timeLimit = challenge.timeLimit = 150f; // the next attempt (it starts by itself after the night)
            while (challenge.Current != RiverChallenge.State.Running) yield return null;
            auto.SetActive(true);
            Debug.Log($"[Test] jobrun: attempt {challenge.Attempts} after {fails} sunset failure(s), limit {challenge.EffectiveLimit:0} s");
            float started = Time.realtimeSinceStartup;
            while (!result.HasValue && Time.realtimeSinceStartup - started < 180f) yield return null;
            if (result.HasValue)
            {
                var r = result.Value;
                Debug.Log($"[Test] jobrun: {job.id} DONE in {r.time:0.0} s: {r.Stars} star(s) [sunset {r.sunsetStar}, cargo {r.conditionStar} ({r.condition:0.00}), time {r.timeStar} (<= {test.TimeStar:0} s)], pay {r.pay}");
            }
            else Debug.Log("[Test] jobrun: the job did NOT finish");

            var save = CampaignState.Current.Save;
            var rec = save.Job(job.id);
            Debug.Log($"[Test] jobrun: save -> money {save.money}, reputation {save.reputation}, {job.id}: completions {rec.completions}, attempts {rec.attempts}, best {rec.bestStars} star(s), best time {rec.bestTime:0.0} s");
            Debug.Log($"[Test] jobrun: board now offers [{string.Join(", ", board.Offers().ConvertAll(j => j.id))}]");
            yield return new WaitForSecondsRealtime(1f);
            string shot = System.IO.Path.Combine(SaveSystem.Root, "jobrun_result.png");
            ScreenCapture.CaptureScreenshot(shot); // the result panel
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[Test] jobrun: result panel -> {shot}");
            Time.timeScale = 1f;
            Application.Quit();
        }
    }
}
