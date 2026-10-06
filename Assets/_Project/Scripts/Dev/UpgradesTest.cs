using System.Collections;
using System.IO;
using CampanhaRio.Campaign;
using CampanhaRio.Jobs;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// -cc-script upgrades [-cc-savedir dir]: solo, at the agency. Gives the test campaign coins and reputation, buys the
    /// three upgrades (the panel's own Buy), then checks their effects: the board's slots, the van's speed on the road
    /// (measured), the new sign (a screenshot), and that the save still holds them when opened again.
    /// Logs "[Test] upgrades ..." lines and quits.
    /// </summary>
    public class UpgradesTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (TestSwitches.Script != "upgrades") return;
            DontDestroyOnLoad(new GameObject("Upgrades Test").AddComponent<UpgradesTest>().gameObject);
        }

        IEnumerator Start()
        {
            while (!AgencyFlow.Instance || !AgencyFlow.Instance.IsSpawned || AgencyFlow.Instance.Current != AgencyFlow.Phase.Hub) yield return null;
            var flow = AgencyFlow.Instance;
            var board = flow.GetComponent<JobBoard>();
            var save = CampaignState.Current.Save;
            Debug.Log($"[Test] upgrades: before -> {save.money} coins, rep {save.reputation}, board slots {board.MaxOffers}, owned [{string.Join(", ", save.upgrades)}]");
            foreach (var u in AgencyUpgrades.All) Debug.Log($"[Test] upgrades: {u.id} -> {AgencyUpgrades.CanBuy(u)}");
            save.money = 500; save.reputation = 5; // (test money)
            foreach (var u in AgencyUpgrades.All) Debug.Log($"[Test] upgrades: buy {u.id} ({u.cost}) -> {AgencyUpgrades.Buy(u)}, then {AgencyUpgrades.CanBuy(u)}");
            Debug.Log($"[Test] upgrades: after -> {save.money} coins, board slots {board.MaxOffers}, van speed x{1f + AgencyUpgrades.Value(AgencyUpgrades.Effect.VanSpeed):0.00}, sign {flow.UpgradeOwned(AgencyUpgrades.Effect.AgencySign)}");
            yield return new WaitForSeconds(1f);
            var cam = Camera.main;
            AgencyFlow.CameraOverride = true;
            foreach (var mb in cam.GetComponents<MonoBehaviour>()) mb.enabled = false;
            cam.transform.position = new Vector3(-18f, 41f, -322f);
            cam.transform.LookAt(new Vector3(-18f, 42f, -340f));
            yield return new WaitForSeconds(0.3f);
            ScreenCapture.CaptureScreenshot(Path.Combine(SaveSystem.Root, "upgrades_placa.png"));
            yield return new WaitForSeconds(0.5f);
            AgencyFlow.CameraOverride = false;

            // The van on the road with the roof rack
            flow.Accept(board.catalog.Find("carta_urgente"));
            yield return new WaitForSeconds(0.5f);
            flow.BoardEveryone();
            var van = ValleyVan.Instance;
            while (!van.Driving) yield return null;
            Vector3 last = van.transform.position; float dist = 0f, driveTime = 0f;
            while (van.Driving || flow.Current == AgencyFlow.Phase.Driving && Vector3.Distance(last, van.transform.position) < 50f)
            {
                dist += Vector3.Distance(new Vector3(last.x, 0f, last.z), new Vector3(van.transform.position.x, 0f, van.transform.position.z));
                last = van.transform.position;
                if (van.Driving) driveTime += Time.deltaTime; // (not the wait for the river to stream in)
                yield return null;
            }
            float time = driveTime;
            Debug.Log($"[Test] upgrades: the drive -> {dist:0} m in {time:0.0} s = {dist / Mathf.Max(time, 0.1f):0.0} m/s average (base speed {van.speed} m/s x{1f + AgencyUpgrades.Value(AgencyUpgrades.Effect.VanSpeed):0.00})");

            var again = SaveSystem.Load<CampaignSave>(SaveSystem.CampaignPath());
            Debug.Log($"[Test] upgrades: save read again -> owned [{string.Join(", ", again.upgrades)}], {again.money} coins");
            Application.Quit();
        }
    }
}
