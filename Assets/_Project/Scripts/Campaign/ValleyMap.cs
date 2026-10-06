using System.Collections.Generic;
using CampanhaRio.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// The valley's map (PLANO_CAMPANHA 4, Phase 7): it reveals rivers, villages and roads as the agency grows (a job done,
    /// reputation). The places and their rules are a FIRST DRAFT (Docs/PENDENTES.md). What is revealed follows from the
    /// host's save; AgencyFlow sends it to everyone as a bit mask, and announces each new place once ("New on the map").
    /// M opens the map (a graybox panel) on any machine.
    /// </summary>
    public class ValleyMap : MonoBehaviour
    {
        public enum PlaceKind { Agency, Road, River, Village }

        public class Place
        {
            public string id, name;
            public PlaceKind kind;
            [Tooltip("Where on the map (0..1 from the top left).")]
            public Vector2 at;
            public string afterJob;
            public int reputation;
            public bool comingSoon;
            public string[] linksTo = new string[0];
        }

        public static readonly List<Place> Places = new List<Place>
        {
            new Place { id = "agencia", name = "Grandma Nina's agency", kind = PlaceKind.Agency, at = new Vector2(0.18f, 0.2f), linksTo = new[] { "estrada" } },
            new Place { id = "estrada", name = "The valley road", kind = PlaceKind.Road, at = new Vector2(0.3f, 0.34f), linksTo = new[] { "rio_moinho", "rio_pedras" } },
            new Place { id = "rio_moinho", name = "Rio do Moinho", kind = PlaceKind.River, at = new Vector2(0.42f, 0.55f), linksTo = new[] { "vila_moinho" } },
            new Place { id = "vila_moinho", name = "Vila do Moinho", kind = PlaceKind.Village, at = new Vector2(0.5f, 0.8f), afterJob = "carta_urgente" },
            new Place { id = "rio_pedras", name = "Rio das Pedras", kind = PlaceKind.River, at = new Vector2(0.7f, 0.45f), reputation = 4, comingSoon = true, linksTo = new[] { "vila_pedras" } },
            new Place { id = "vila_pedras", name = "Vila das Pedras", kind = PlaceKind.Village, at = new Vector2(0.84f, 0.7f), reputation = 6, comingSoon = true },
        };

        public static Place Find(string id) => Places.Find(p => p.id == id);

        /// <summary>Host: what the save has revealed so far (one bit per place).</summary>
        public static int RevealedMask(CampaignSave save)
        {
            int mask = 0;
            for (int i = 0; i < Places.Count; i++)
            {
                var p = Places[i];
                bool job = string.IsNullOrEmpty(p.afterJob) || (save != null && save.jobs.Exists(j => j.jobId == p.afterJob && j.completions > 0));
                bool rep = p.reputation <= 0 || (save != null && save.reputation >= p.reputation);
                if (job && rep) mask |= 1 << i;
            }
            return mask;
        }

        public bool IsOpen { get; private set; }
        public void Toggle() => IsOpen = !IsOpen;

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) Toggle();
        }

        GUIStyle title, label, small;

        void OnGUI()
        {
            var flow = AgencyFlow.Instance;
            if (!IsOpen || !flow) return;
            int mask = flow.MapRevealed;
            var ink = new Color(0.2f, 0.15f, 0.1f);
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = ink } };
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.UpperCenter, normal = { textColor = ink } };
            float s = Screen.height / 1080f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            var area = new Rect(260, 110, 1400, 860);
            GUI.color = new Color(0.93f, 0.87f, 0.72f, 0.97f); // the old paper map
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(area.x + 30, area.y + 20, 800, 40), Loc.T("The valley"), title);
            GUI.Label(new Rect(area.x + 30, area.y + 58, 300, 30), Loc.T("(M closes the map)"), new GUIStyle(small) { alignment = TextAnchor.UpperLeft });

            Vector2 P(Place p) => new Vector2(area.x + p.at.x * area.width, area.y + p.at.y * area.height);
            bool Shown(int i) => (mask & (1 << i)) != 0;
            // The roads and rivers between revealed places (a dotted line toward a hidden one)
            for (int i = 0; i < Places.Count; i++)
            {
                if (!Shown(i)) continue;
                foreach (string to in Places[i].linksTo)
                {
                    int j = Places.FindIndex(p => p.id == to);
                    if (j < 0) continue;
                    Line(P(Places[i]), P(Places[j]), Shown(j) ? new Color(0.45f, 0.33f, 0.22f) : new Color(0.45f, 0.33f, 0.22f, 0.35f), Shown(j) ? 1f : 0.5f);
                }
            }
            for (int i = 0; i < Places.Count; i++)
            {
                var p = Places[i];
                var c = P(p);
                if (Shown(i))
                {
                    GUI.color = p.kind switch
                    {
                        PlaceKind.River => new Color(0.25f, 0.6f, 0.75f),
                        PlaceKind.Village => new Color(0.75f, 0.35f, 0.25f),
                        PlaceKind.Agency => new Color(0.4f, 0.08f, 0.13f),
                        _ => new Color(0.55f, 0.45f, 0.3f),
                    };
                    GUI.DrawTexture(new Rect(c.x - 14, c.y - 14, 28, 28), Texture2D.whiteTexture);
                    GUI.color = new Color(0.2f, 0.15f, 0.1f);
                    GUI.Label(new Rect(c.x - 150, c.y + 18, 300, 26), Loc.T(p.name), label);
                    if (p.comingSoon) GUI.Label(new Rect(c.x - 150, c.y + 42, 300, 22), Loc.T("(coming soon)"), small);
                }
                else
                {
                    GUI.color = new Color(0.6f, 0.55f, 0.45f, 0.6f); // the fog
                    GUI.DrawTexture(new Rect(c.x - 60, c.y - 40, 120, 80), Texture2D.whiteTexture);
                    GUI.color = new Color(0.3f, 0.25f, 0.2f);
                    GUI.Label(new Rect(c.x - 50, c.y - 14, 100, 30), "?", label);
                }
            }
            GUI.color = Color.white;
            GUI.matrix = m;
        }

        static void Line(Vector2 a, Vector2 b, Color color, float dash)
        {
            GUI.color = color;
            float len = Vector2.Distance(a, b);
            int steps = Mathf.CeilToInt(len / 10f);
            for (int k = 0; k < steps; k++)
            {
                if (dash < 1f && k % 2 == 1) continue;
                var p = Vector2.Lerp(a, b, (k + 0.5f) / steps);
                GUI.DrawTexture(new Rect(p.x - 3, p.y - 3, 6, 6), Texture2D.whiteTexture);
            }
        }
    }
}
