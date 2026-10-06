using System.Collections;
using System.IO;
using CampanhaRio.Campaign;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// The room panel. -cc-script room: a solo game opens the panel (Tab), waits for a second player and takes a
    /// screenshot (into -cc-savedir). -cc-script rejoin -cc-target &lt;ip&gt;: a solo game presses the panel's Join, which
    /// reopens the game joined to that address. Logs "[Test] room ..." lines.
    /// </summary>
    public class RoomTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "room" && TestSwitches.Script != "rejoin") return;
            DontDestroyOnLoad(new GameObject("Room Test").AddComponent<RoomTest>().gameObject);
        }

        IEnumerator Start()
        {
            if (TestSwitches.Script == "rejoin") // (on the same PC its own solo room can't open: the port is taken)
            {
                yield return new WaitForSeconds(3f);
                Debug.Log($"[Test] room: joining {TestSwitches.Value("-cc-target")} through the panel");
                RoomPanel.Rejoin(TestSwitches.Value("-cc-target", "127.0.0.1"));
                yield break;
            }
            while (!AgencyFlow.Instance || !AgencyFlow.Instance.IsSpawned) yield return null;
            yield return new WaitForSeconds(2f);
            var panel = AgencyFlow.Instance.GetComponent<RoomPanel>();
            if (!panel.IsOpen) typeof(RoomPanel).GetProperty("IsOpen").SetValue(panel, true);
            float t = Time.realtimeSinceStartup;
            while (NetworkPlayer.All.Count < 2 && Time.realtimeSinceStartup - t < 90f) yield return null;
            Debug.Log($"[Test] room: players {NetworkPlayer.All.Count} after {Time.realtimeSinceStartup - t:0} s");
            yield return new WaitForSeconds(2f);
            ScreenCapture.CaptureScreenshot(Path.Combine(SaveSystem.Root, "sala.png"));
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }
    }
}
