using System.Collections;
using System.IO;
using CampanhaRio.Campaign;
using CampanhaRio.Net;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-script streamwalk: the streaming test, run as release builds (one host, one or more clients):
    ///   host:   CampanhaRio.exe -cc-host -cc-script streamwalk -cc-players 2 -cc-savedir &lt;dir&gt;
    ///   client: CampanhaRio.exe -cc-join -cc-script streamwalk -cc-savedir &lt;dir2&gt;
    /// Once -cc-players bodies exist, every player walks forward (+Z) at -cc-speed m/s (default 8) through Test_A, B, C.
    /// The log shows each load/unload, its frame-time window ([Frames]), the checkpoint saves, and a final summary.
    /// Each instance quits a few seconds after its player passes the end (-cc-endz, default 350 m).
    /// </summary>
    public class StreamWalkTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "streamwalk") return;
            DontDestroyOnLoad(new GameObject("Stream Walk Test").AddComponent<StreamWalkTest>().gameObject);
        }

        IEnumerator Start()
        {
            int players = Mathf.RoundToInt(TestSwitches.Number("-cc-players", 1f));
            float speed = TestSwitches.Number("-cc-speed", 8f);
            float endZ = TestSwitches.Number("-cc-endz", 350f);
            float started = Time.realtimeSinceStartup;

            while (!NetworkPlayer.Local || NetworkPlayer.All.Count < players)
            {
                if (Time.realtimeSinceStartup - started > 60f) { Debug.LogError("[Test] streamwalk: players never arrived"); Application.Quit(1); yield break; }
                yield return null;
            }
            Debug.Log($"[Test] streamwalk: {NetworkPlayer.All.Count} player(s) at {NetworkPlayer.Local.transform.position}, walking at {speed} m/s");
            yield return new WaitForSeconds(1f);
            NetworkPlayer.ScriptedWalk = speed;
            float walkStart = Time.realtimeSinceStartup;
            while (NetworkPlayer.Local && NetworkPlayer.Local.transform.position.z < endZ)
            {
                if (Time.realtimeSinceStartup - walkStart > 120f) { Debug.LogError("[Test] streamwalk: never reached the end"); break; }
                yield return null;
            }
            NetworkPlayer.ScriptedWalk = 0f;
            yield return new WaitForSeconds(NetSession.Instance && NetSession.Instance.IsHost ? 5f : 2f);

            string segments = SegmentStreamer.Instance ? string.Join(", ", SegmentStreamer.Instance.LoadedSegments) : "-";
            string scenes = "";
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                scenes += UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name + " ";
            Debug.Log($"[Test] streamwalk done in {Time.realtimeSinceStartup - walkStart:0.0} s: player at {(NetworkPlayer.Local ? NetworkPlayer.Local.transform.position.ToString() : "-")}, scenes loaded here: {scenes}(streamer: {segments})");
            if (CampaignState.Current != null)
            {
                var reread = SaveSystem.Load<CampaignSave>(SaveSystem.CampaignPath(CampaignState.Current.Slot));
                Debug.Log($"[Test] save on disk: checkpoint '{reread.checkpoint}', file {SaveSystem.CampaignPath(CampaignState.Current.Slot)} ({new FileInfo(SaveSystem.CampaignPath(CampaignState.Current.Slot)).Length} bytes)");
            }
            Application.Quit();
        }
    }
}
