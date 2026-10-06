using CampanhaRio.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CampanhaRio.Jobs
{
    /// <summary>What the job HUD shows (the host fills it from its JobRun; AgencyFlow sends it to everyone).</summary>
    public struct JobHudState : INetworkSerializable
    {
        public bool active;
        public FixedString64Bytes title, label, state, meterLabel;
        public float timeLeft, condition, meter, timeStar;
        public bool passenger;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref active);
            s.SerializeValue(ref title); s.SerializeValue(ref label); s.SerializeValue(ref state); s.SerializeValue(ref meterLabel);
            s.SerializeValue(ref timeLeft); s.SerializeValue(ref condition); s.SerializeValue(ref meter); s.SerializeValue(ref timeStar);
            s.SerializeValue(ref passenger);
        }
    }

    /// <summary>The graybox job HUD (OnGUI) and the result panel, the same on every machine.</summary>
    public static class JobHud
    {
        static GUIStyle big, small, stars;

        public static void Draw(in JobHudState h, JobResult? result)
        {
            if (!h.active && !result.HasValue) return;
            big ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            small ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            stars ??= new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold };
            float sc = Screen.height / 1080f;
            var m = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));

            if (h.active)
            {
                GUILayout.BeginArea(new Rect(24, 60, 420, 170), GUI.skin.box);
                GUILayout.Label(Loc.T(h.title.ToString()), big);
                GUILayout.Label(Loc.F("Sunset in {0}", Clock(h.timeLeft)), small);
                if (h.label.Length > 0)
                {
                    GUILayout.Label($"{Loc.T(h.label.ToString())}: {Loc.T(h.state.ToString())}", small);
                    Bar(h.condition, new Color(0.9f, 0.35f, 0.25f), new Color(0.45f, 0.85f, 0.4f));
                }
                if (h.meter >= 0f)
                {
                    GUILayout.Label(Loc.T(h.meterLabel.ToString()), small);
                    Bar(h.meter, new Color(0.95f, 0.85f, 0.3f), new Color(0.95f, 0.3f, 0.2f));
                }
                GUILayout.EndArea();
            }

            if (result.HasValue)
            {
                var r = result.Value;
                GUILayout.BeginArea(new Rect(1920 / 2 - 260, 300, 520, 240), GUI.skin.box);
                GUILayout.Label(Loc.T("Delivered!"), big);
                GUILayout.Label(new string('★', r.Stars) + new string('☆', 3 - r.Stars), stars);
                GUILayout.Label((r.sunsetStar ? "★ " : "☆ ") + Loc.T("Before sunset"), small);
                GUILayout.Label((r.conditionStar ? "★ " : "☆ ") + Loc.T(h.passenger ? "Happy passenger" : "Cargo in one piece"), small);
                GUILayout.Label((r.timeStar ? "★ " : "☆ ") + Loc.F("Within {0}", Clock(h.timeStar)), small);
                GUILayout.Label(Loc.F("+{0} coins, +{1} reputation", r.pay, r.reputation), small);
                GUILayout.EndArea();
            }
            GUI.matrix = m;
        }

        static void Bar(float v, Color low, Color high)
        {
            var bar = GUILayoutUtility.GetRect(380, 14);
            GUI.Box(bar, GUIContent.none);
            GUI.color = Color.Lerp(low, high, v);
            GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * Mathf.Clamp01(v), bar.height - 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        public static string Clock(float seconds) => $"{(int)(seconds / 60f)}:{(int)(seconds % 60f):00}";
    }
}
