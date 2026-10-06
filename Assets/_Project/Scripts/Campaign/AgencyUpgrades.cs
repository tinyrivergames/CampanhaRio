using System.Collections.Generic;
using CampanhaRio.Core;
using UnityEngine;

namespace CampanhaRio.Campaign
{
    /// <summary>
    /// The agency's and the van's upgrades (PLANO_CAMPANHA 4, Phase 7): bought with the jobs' coins, opened by reputation,
    /// kept in the host's save. The list is a FIRST DRAFT for the developer to approve (Docs/PENDENTES.md); each upgrade
    /// has one effect the game reads through <see cref="Has"/> / <see cref="Value"/>.
    /// </summary>
    public static class AgencyUpgrades
    {
        public enum Effect { VanSpeed, BoardSlot, AgencySign }

        public class Upgrade
        {
            public string id, title, description;
            public int cost, reputation;
            public Effect effect;
            public float amount;
        }

        public static readonly List<Upgrade> All = new List<Upgrade>
        {
            new Upgrade { id = "van_bagageiro", title = "Roof rack for the van", description = "Seu Alce drives a little faster with everything strapped on top.",
                          cost = 60, reputation = 2, effect = Effect.VanSpeed, amount = 0.25f },
            new Upgrade { id = "quadro_maior", title = "A bigger orders board", description = "Room for one more order on the board.",
                          cost = 90, reputation = 3, effect = Effect.BoardSlot, amount = 1f },
            new Upgrade { id = "placa_nova", title = "A new sign for the agency", description = "Grandma Nina's old sign, repainted. The village notices.",
                          cost = 40, reputation = 1, effect = Effect.AgencySign, amount = 1f },
        };

        public static Upgrade Find(string id) => All.Find(u => u.id == id);

        static CampaignSave Save => CampaignState.Current?.Save;

        public static bool Owned(string id) => Save != null && Save.upgrades.Contains(id);
        public static bool Has(Effect e) => All.Exists(u => u.effect == e && Owned(u.id));
        /// <summary>The sum of the owned upgrades' amounts for this effect.</summary>
        public static float Value(Effect e)
        {
            float v = 0f;
            foreach (var u in All) if (u.effect == e && Owned(u.id)) v += u.amount;
            return v;
        }

        public enum Can { Yes, Owned, NeedReputation, NeedCoins, NoCampaign }

        public static Can CanBuy(Upgrade u)
        {
            var s = Save;
            if (s == null) return Can.NoCampaign;
            if (s.upgrades.Contains(u.id)) return Can.Owned;
            if (s.reputation < u.reputation) return Can.NeedReputation;
            if (s.money < u.cost) return Can.NeedCoins;
            return Can.Yes;
        }

        /// <summary>Host: buy it (coins out, written to the save at once).</summary>
        public static bool Buy(Upgrade u)
        {
            if (CanBuy(u) != Can.Yes) return false;
            CampaignState.Current.BuyUpgrade(u.id, u.cost);
            Debug.Log($"[Upgrades] bought {u.id} for {u.cost} (left {Save.money} coins)");
            return true;
        }

        public static string Status(Upgrade u) => CanBuy(u) switch
        {
            Can.Owned => Loc.T("Owned"),
            Can.NeedReputation => Loc.F("Needs reputation {0}", u.reputation),
            Can.NeedCoins => Loc.F("{0} coins", u.cost),
            _ => Loc.F("{0} coins", u.cost),
        };
    }
}
