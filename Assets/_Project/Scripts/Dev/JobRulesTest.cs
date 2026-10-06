using System.Collections;
using CampanhaRio.Campaign;
using CampanhaRio.Jobs;
using CampanhaRio.Kayak;
using CampanhaRio.River;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-scene KayakTest -cc-script jobrules [-cc-savedir dir]: tests the two jobs' rules on the KayakTest river, the
    /// player's kayak on the autopilot (finish at 300 m):
    ///   1. the urgent letter: a capsize and two hard hits soak it -> delivered, but without the cargo star;
    ///   2. the scared goat: paddling fast scares him until he jumps out (the speeds are logged, to tune his fear); with
    ///      the goat in the water the finish doesn't count; once fetched (the kayak next to him) it does.
    /// Runs at 2x time scale. Logs "[Test] jobrules ..." lines and quits.
    /// </summary>
    public class JobRulesTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "jobrules") return;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) =>
            {
                var challenge = FindAnyObjectByType<RiverChallenge>();
                if (challenge && !FindAnyObjectByType<JobRulesTest>()) challenge.gameObject.AddComponent<JobRulesTest>();
            };
        }

        void Awake() => GetComponent<RiverChallenge>().startOnPlay = false;

        IEnumerator Start()
        {
            var challenge = GetComponent<RiverChallenge>();
            var river = FindAnyObjectByType<RiverPath>();
            CampaignState.Open();
            var run = gameObject.AddComponent<JobRun>();
            var catalog = JobCatalog.Load();
            yield return null;
            Time.timeScale = 2f;
            challenge.finishAlong = 300f;
            var kayak = KayakRegistry.Local;
            var auto = kayak.GetComponent<KayakAutopilot>();
            JobResult? result = null;
            run.Finished += r => result = r;

            // 1. The letter
            var letter = Instantiate(catalog.Find("carta_urgente"));
            letter.timeLimit = 200f;
            run.Play(letter);
            auto.SetActive(true);
            yield return new WaitForSeconds(3f);
            kayak.RaiseCapsize();
            kayak.RaiseImpact(ImpactLevel.SpinOut, 1f, kayak.transform.position, Vector3.up);
            kayak.RaiseImpact(ImpactLevel.SpinOut, 1f, kayak.transform.position, Vector3.up);
            Debug.Log($"[Test] jobrules: letter after a capsize and 2 spin-outs: condition {run.Rule.Condition:0.00} ({run.Rule.State}), star needs {letter.conditionStar:0.00}");
            yield return Wait(() => result.HasValue, 150f);
            Log("letter", result);

            // 2. The goat
            result = null;
            var goat = Instantiate(catalog.Find("bode_medroso"));
            goat.timeLimit = 200f;
            run.Play(goat);
            auto.SetActive(true);
            var rule = (ScaredGoatRule)run.Rule;
            float maxSpeed = 0f, sum = 0f; int n = 0;
            float t0 = Time.time;
            while (!rule.InWater && Time.time - t0 < 60f && !result.HasValue)
            {
                maxSpeed = Mathf.Max(maxSpeed, kayak.Speed); sum += kayak.Speed; n++;
                yield return new WaitForSeconds(0.5f);
            }
            Debug.Log($"[Test] jobrules: goat after {Time.time - t0:0.0} s of autopilot: in water {rule.InWater}, panics {rule.Panics}, fear {rule.Fear:0.00}, happiness {rule.Condition:0.00}; kayak speed avg {(n > 0 ? sum / n : 0f):0.0} max {maxSpeed:0.0} m/s (calm below {rule.calmSpeed} m/s)");
            if (!rule.InWater) { kayak.RaiseCapsize(); yield return null; Debug.Log("[Test] jobrules: (no natural panic: a capsize made him jump)"); }

            // Past the finish with the goat in the water: it must not count
            auto.SetActive(false);
            var past = river.GetPointAtDistance(challenge.finishAlong + 20f);
            kayak.Teleport(past.point, Quaternion.LookRotation(past.direction), Vector3.zero);
            kayak.Held = true;
            yield return new WaitForSeconds(3f);
            Debug.Log($"[Test] jobrules: past the finish with the goat in the water -> challenge {challenge.Current}, finished {result.HasValue} (must be Running / False)");

            // Fetch him, then the finish counts
            kayak.Held = false;
            var at = rule.GoatPosition;
            kayak.Teleport(at + Vector3.right * 1f, kayak.transform.rotation, Vector3.zero);
            yield return Wait(() => !rule.InWater, 5f);
            Debug.Log($"[Test] jobrules: fetched: in water {rule.InWater}, carrier {(rule.Carrier ? rule.Carrier.name : "none")}, fear {rule.Fear:0.00}");
            kayak.Teleport(past.point, Quaternion.LookRotation(past.direction), Vector3.zero);
            yield return Wait(() => result.HasValue, 10f);
            Log("goat", result);

            Time.timeScale = 1f;
            Application.Quit();
        }

        static IEnumerator Wait(System.Func<bool> done, float realSeconds)
        {
            float t = Time.realtimeSinceStartup;
            while (!done() && Time.realtimeSinceStartup - t < realSeconds) yield return null;
        }

        static void Log(string what, JobResult? r)
        {
            if (!r.HasValue) { Debug.Log($"[Test] jobrules: {what} did NOT finish"); return; }
            var v = r.Value;
            Debug.Log($"[Test] jobrules: {what} DONE in {v.time:0.0} s: {v.Stars} star(s) [sunset {v.sunsetStar}, cargo/passenger {v.conditionStar} ({v.condition:0.00}), time {v.timeStar}], pay {v.pay}");
        }
    }
}
