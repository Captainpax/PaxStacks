// mrstacks.cs
using S1API.Entities;
using S1API.Logging;
using System.Linq;
using System.Collections.Generic;
using S1API.Items;

namespace PaxStacks
{
    /// <summary>
    /// MrStacks is the handler class for the 'Mr. Stacks' NPC, who manages
    /// tier-based loot distribution logic and custom drop ordering by the player.
    /// </summary>
    public static class MrStacks
    {
        private static Log _log;
        private static MrStacksContact _contact;

        /// <summary>
        /// Static tier-to-item ID mapping for loot generation.
        /// </summary>
        private static readonly Dictionary<int, List<string>> TierLoot = new Dictionary<int, List<string>>
        {
            { 1, new List<string> { "cash", "soda", "energy_drink" } },
            { 2, new List<string> { "weed_bag", "fertilizer", "clippers" } },
            { 3, new List<string> { "goldwatch", "m1911", "goldbar" } } // All validated item IDs
        };

        /// <summary>
        /// Initializes the Mr. Stacks system and NPC contact reference.
        /// </summary>
        public static void Initialize(Log logger)
        {
            _log = logger;
            _log.Msg(">> Mr. Stacks initializing...");
            SetupNpc();
        }

        /// <summary>
        /// Sets up the in-game NPC contact for Mr. Stacks.
        /// </summary>
        private static void SetupNpc()
        {
            var existing = NPC.All.FirstOrDefault(n => n.ID == "MrStacks");
            if (existing == null)
            {
                _contact = new MrStacksContact("MrStacks", "Mr.", "Stacks");
                _log.Msg("-- Created Mr. Stacks NPC contact");
            }
            else
            {
                _contact = (MrStacksContact)existing;
                _log.Msg("-- Reusing existing Mr. Stacks contact");
            }
        }

        /// <summary>
        /// Sends a daily drop tier announcement to the player.
        /// Used during weekly auto-drop for flavor.
        /// </summary>
        public static void SendDailyDropMessage(int week)
        {
            int tier = GetTierByWeek(week);
            string msg = "Today's drop tier: " + tier + ". Better loot awaits. 💼";
            _contact.SendTextMessage(msg);
        }

        /// <summary>
        /// Player-initiated request to spawn a tiered drop.
        /// Only triggers if the week allows it. Sends feedback via NPC message.
        /// </summary>
        public static void RequestCustomDrop(int tier)
        {
            if (!TierLoot.ContainsKey(tier))
            {
                _log.Warning($"-- Invalid tier requested: {tier}");
                _contact.SendTextMessage("I don't know what kind of drop you're asking for. ❌");
                return;
            }

            // Ask PaxDeadDrop to spawn the drop if conditions allow
            bool success = PaxDeadDrop.TryManualDrop(tier);

            if (success)
            {
                _contact.SendTextMessage($"You got it. Dropping tier {tier} supply now. 📦");
            }
            else
            {
                _contact.SendTextMessage($"You're not high enough in the game to get a tier {tier} drop yet. 📉");
            }
        }

        /// <summary>
        /// Sends the actual message when a drop is created.
        /// </summary>
        public static void NotifyPlayerDrop(string position, int tier)
        {
            string msg = $"Tier {tier} package is live. Go get it: {position} 📍";
            _contact.SendTextMessage(msg);
        }

        /// <summary>
        /// Gets the appropriate list of loot items for a given week's tier.
        /// </summary>
        public static List<ItemDefinition> GetLootForTier(int week)
        {
            int tier = GetTierByWeek(week);
            return GetLootByTier(tier);
        }

        /// <summary>
        /// Gets all valid items for a given tier ID.
        /// </summary>
        private static List<ItemDefinition> GetLootByTier(int tier)
        {
            var list = new List<ItemDefinition>();
            if (TierLoot.TryGetValue(tier, out List<string> ids))
            {
                foreach (var id in ids)
                {
                    var def = ItemManager.GetItemDefinition(id);
                    if (def != null)
                        list.Add(def);
                    else
                        _log.Warning("-- Item not found for ID: " + id);
                }
            }

            return list;
        }

        /// <summary>
        /// Maps current week to the highest unlockable drop tier.
        /// Week 0–1 = Tier 1, Week 2–3 = Tier 2, Week 4+ = Tier 3.
        /// </summary>
        private static int GetTierByWeek(int week)
        {
            if (week <= 1)
                return 1;
            else if (week <= 3)
                return 2;
            else
                return 3;
        }

        /// <summary>
        /// In-game NPC used for contact message delivery.
        /// </summary>
        private class MrStacksContact : NPC
        {
            public MrStacksContact(string id, string first, string last)
                : base(id, first, last) { }
        }
    }
}
