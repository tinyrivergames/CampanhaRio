using System.Collections;
using System.IO;
using CampanhaRio.Campaign;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-script map [-cc-savedir dir]: solo, at the agency. The valley map on a fresh save (a screenshot), then as if
    /// the urgent letter were done and the agency had reputation 4: the village and Rio das Pedras show up, announced once
    /// (a second screenshot). Logs "[Test] map ..." lines and quits.
    /// </summary>
    public class MapTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "map") return;
            DontDestroyOnLoad(new GameObject("Map Test").AddComponent<MapTest>().gameObject);
        }

        IEnumerator Start()
        {
            while (!AgencyFlow.Instance || !AgencyFlow.Instance.IsSpawned || AgencyFlow.Instance.Current != AgencyFlow.Phase.Hub) yield return null;
            var flow = AgencyFlow.Instance;
            var map = flow.GetComponent<ValleyMap>();
            yield return new WaitForSeconds(1f);
            Debug.Log($"[Test] map: fresh save -> {Names(flow.MapRevealed)}");
            map.Toggle();
            yield return new WaitForSeconds(0.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(SaveSystem.Root, "mapa_inicio.png"));
            yield return new WaitForSeconds(0.5f);

            var save = CampaignState.Current.Save;
            save.Job("carta_urgente").completions = 1;
            save.reputation = 4;
            yield return new WaitForSeconds(1f);
            Debug.Log($"[Test] map: letter done, reputation 4 -> {Names(flow.MapRevealed)} (announced mask {save.mapSeen})");
            ScreenCapture.CaptureScreenshot(Path.Combine(SaveSystem.Root, "mapa_revelado.png"));
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        static string Names(int mask)
        {
            var list = new System.Collections.Generic.List<string>();
            for (int i = 0; i < ValleyMap.Places.Count; i++) if ((mask & (1 << i)) != 0) list.Add(ValleyMap.Places[i].id);
            return "[" + string.Join(", ", list) + "]";
        }
    }
}
