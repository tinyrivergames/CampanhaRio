using CampanhaRio.Core;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// The agency's upgrades board (graybox panel, OnGUI): the host buys with the coins from the jobs. Opened by
    /// AgencyFlow when the host presses E at the UpgradeBoard marker (next to the shed's door).
    /// </summary>
    public class UpgradesPanel : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public event System.Action<string> Bought;

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;

        GUIStyle title, text;

        void OnGUI()
        {
            if (!IsOpen) return;
            var save = CampaignState.Current?.Save;
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            text ??= new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            float s = Screen.height / 1080f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = 1000f, h = 160f + 120f * AgencyUpgrades.All.Count;
            GUILayout.BeginArea(new Rect(1920 / 2 - w / 2, 1080 / 2 - h / 2, w, h), GUI.skin.box);
            GUILayout.Label(Loc.T("Upgrades"), title);
            if (save != null) GUILayout.Label(Loc.F("{0} coins · reputation {1}", save.money, save.reputation), text);
            foreach (var u in AgencyUpgrades.All)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label(Loc.T(u.title), title);
                GUILayout.Label(Loc.T(u.description), text);
                GUILayout.EndVertical();
                var can = AgencyUpgrades.CanBuy(u);
                GUI.enabled = can == AgencyUpgrades.Can.Yes;
                if (GUILayout.Button(can == AgencyUpgrades.Can.Yes ? Loc.F("Buy ({0})", u.cost) : AgencyUpgrades.Status(u), GUILayout.Width(200), GUILayout.Height(56))
                    && AgencyUpgrades.Buy(u)) Bought?.Invoke(u.id);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            if (GUILayout.Button(Loc.T("Close"), GUILayout.Height(36))) Close();
            GUILayout.EndArea();
            GUI.matrix = m;
        }
    }
}
