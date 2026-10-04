using System.Collections;
using CampanhaRio.Campaign;
using CampanhaRio.Kayak;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-scene KayakTest -cc-script riverrule [-cc-savedir dir]: tests the river rule with a group of two kayaks
    /// (the player's, driven by the autopilot, and a second one that never moves):
    ///   1. three attempts with a 12 s limit: each fails at sunset (night, black, back to the start);
    ///   2. after the 3rd failure the hidden assist has loosened the limit (+8%);
    ///   3. a real limit: the autopilot reaches the finish (300 m) = half the group -> passed, with a medal;
    ///   4. the campaign save holds the river's record.
    /// Runs at 2x time scale. Logs "[Test] riverrule ..." lines and quits.
    /// </summary>
    public class RiverRuleTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "riverrule") return;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) =>
            {
                var challenge = FindAnyObjectByType<RiverChallenge>();
                if (challenge && !FindAnyObjectByType<RiverRuleTest>()) challenge.gameObject.AddComponent<RiverRuleTest>();
            };
        }

        IEnumerator Start()
        {
            var challenge = GetComponent<RiverChallenge>();
            CampaignState.Open();
            yield return null;
            var player = KayakRegistry.Local;
            // The second member of the group: a copy that stays where it is put (it never finishes)
            player.gameObject.SetActive(false); // clone it inactive: the copy must never register as the local player
            var idle = Instantiate(player.gameObject).GetComponent<KayakController>();
            DestroyImmediate(idle.GetComponent<LocalPlayer>());
            DestroyImmediate(idle.GetComponent<KayakInput>());
            player.gameObject.SetActive(true);
            idle.gameObject.SetActive(true);
            idle.InputSource = null;
            idle.Held = true; // pinned where it is put: it never reaches the finish
            yield return null;

            Time.timeScale = 2f;
            var auto = player.GetComponent<KayakAutopilot>();
            challenge.finishAlong = 300f;
            challenge.timeLimit = 12f;
            int fails = 0;
            challenge.Failed += () => fails++;
            challenge.Begin();
            auto.SetActive(true);
            while (fails < 3) yield return null;
            while (challenge.Current != RiverChallenge.State.Running) yield return null;
            Debug.Log($"[Test] riverrule: {fails} failures, attempts {challenge.Attempts}, limit now {challenge.EffectiveLimit:0.00} s (base 12, assist +{challenge.Assist * 100f:0}%)");

            bool passed = false; Medal medal = Medal.None; float time = 0f;
            challenge.Passed += (m, t) => { passed = true; medal = m; time = t; };
            challenge.timeLimit = 150f;
            challenge.Begin();
            auto.SetActive(true);
            float started = Time.realtimeSinceStartup;
            while (!passed && challenge.Current != RiverChallenge.State.Failed && Time.realtimeSinceStartup - started < 180f) yield return null;
            Debug.Log($"[Test] riverrule: final attempt {(passed ? "PASSED" : "did not pass")} in {time:0.0} s, medal {medal}, group {KayakRegistry.All.Count}, day progress {challenge.day.progress:0.00}");

            var save = CampaignState.Current.Save.River(challenge.riverId);
            Debug.Log($"[Test] riverrule: save -> completed {save.completed}, attempts {save.attempts}, failures {save.failures}, best {save.bestTime:0.0} s, medal {save.bestMedal} ({SaveSystem.CampaignPath()})");
            Time.timeScale = 1f;
            Application.Quit();
        }
    }
}
