using System.Collections.Generic;
using CampanhaRio.Kayak;
using UnityEngine;

namespace CampanhaRio.Dev
{
    /// <summary>
    /// DEBUG (and for filling a room in Etapa 8): F3 spawns bot kayaks next to the local player's start, F3 again
    /// removes them. Bots are the same prefab as the player; the only difference is the input source (the autopilot
    /// instead of the devices), so there are no special code paths.
    /// </summary>
    public class KayakBotSpawner : MonoBehaviour
    {
        [Tooltip("The kayak prefab (the same one the player uses).")]
        public KayakController prefab;
        [Tooltip("Sideways spacing between kayaks at the start (m).")]
        public float spacing = 3.2f;
        [Tooltip("Bots start this far behind the player's start (m), so they don't sit on top of each other.")]
        public float behind = 3f;
        [Tooltip("Each further bot starts this much further back (m).")]
        public float stagger = 3f;
        [Min(1)] public int count = 3;
        [Tooltip("Personality and hull colour of each bot, in spawn order (repeats if there are more bots).")]
        public AutopilotProfile[] profiles = { AutopilotProfile.Careful, AutopilotProfile.Normal, AutopilotProfile.Risky };
        public Color[] hullColors = { new Color(0.25f, 0.62f, 0.66f), new Color(0.96f, 0.76f, 0.22f), new Color(0.58f, 0.34f, 0.74f) };

        readonly List<KayakController> bots = new List<KayakController>();
        public IReadOnlyList<KayakController> Bots => bots;

        void Update()
        {
            var local = KayakRegistry.Local;
            if (local && local.input && local.input.SpawnBotsPressed)
            {
                if (bots.Count > 0) Despawn();
                else Spawn();
            }
        }

        /// <summary>Spawns the bots behind the local kayak's start line (or behind this object when there is none).</summary>
        public void Spawn()
        {
            if (!prefab) { Debug.LogWarning("[Campanha] KayakBotSpawner has no prefab."); return; }
            var local = KayakRegistry.Local;
            Transform anchor = local ? local.transform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            // Instantiate under an inactive parent, so nothing wakes up before it's turned into a bot
            var holder = new GameObject("Bot Holder");
            holder.SetActive(false);
            for (int i = 0; i < count; i++)
            {
                float side = (i - (count - 1) * 0.5f) * spacing;
                Vector3 position = anchor.position - forward * (behind + (count - 1 - i) * stagger) + right * side; // staggered; the last (fastest) bot starts in front
                if (local && local.river) position.y = local.river.GetWaterHeight(position);
                var bot = Instantiate(prefab, position, Quaternion.LookRotation(forward, Vector3.up), holder.transform);

                MakeBot(bot, i);
                bot.transform.SetParent(null, true);
                bot.SetStart(position, bot.transform.rotation);
                bots.Add(bot);
            }
            Destroy(holder);
            Debug.Log($"[Campanha] Spawned {count} bots.");
        }

        /// <summary>The bot differences: no device input, no LocalPlayer marker, the autopilot drives.</summary>
        protected virtual void MakeBot(KayakController bot, int index)
        {
            var profile = profiles.Length > 0 ? profiles[index % profiles.Length] : AutopilotProfile.Normal;
            ConfigureAsBot(bot, profile);
            bot.name = $"Kayak Bot {index + 1} ({profile})";
            if (hullColors.Length > 0) KayakPaint.SetHullColor(bot, hullColors[index % hullColors.Length]);
        }

        /// <summary>
        /// Turns a kayak into a bot (also used by the multiplayer host for its bots): the device input and the LocalPlayer
        /// marker go, the autopilot with this profile drives.
        /// </summary>
        public static KayakAutopilot ConfigureAsBot(KayakController bot, AutopilotProfile profile)
        {
            var marker = bot.GetComponent<LocalPlayer>();
            if (marker) DestroyImmediate(marker);
            var input = bot.GetComponent<KayakInput>();
            if (input) DestroyImmediate(input);
            bot.input = null;
            bot.ResetInputSource();
            var autopilot = bot.GetComponent<KayakAutopilot>();
            if (!autopilot) autopilot = bot.gameObject.AddComponent<KayakAutopilot>();
            autopilot.enableOnStart = true;
            autopilot.profile = profile;
            return autopilot;
        }

        public void Despawn()
        {
            foreach (var bot in bots) if (bot) Destroy(bot.gameObject);
            bots.Clear();
        }
    }
}
