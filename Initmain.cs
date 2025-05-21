// InitMain.cs
using MelonLoader;
using S1API.DeadDrops;     // Access to dead drop locations and instances
using S1API.GameTime;      // Hooks for day/week/sleep events
using S1API.Internal.Utils;// Extensions like PickOne/PickMany
using S1API.Items;         // Item definitions and creation
using S1API.Logging;       // Logging utility
using S1API.Storages;      // Inventory/Storage container abstraction
using S1API.Entities;      // Access to NPCs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PaxStacks
{
    public class InitMain : MelonMod
    {
        // Dead drop lifecycle states
        private enum DropState { None, Waiting, Spawned }

        private bool _started = false; // Has mod fully started?
        private Log _log;              // PaxStacks-specific logger
        private DropState _state = DropState.None;
        private DeadDropInstance _activeDrop; // Current active drop this day

        // Manually curated list of item definitions to be used as drop loot
        private List<ItemDefinition> _allItems = new List<ItemDefinition>();

        // Week tracker — determines scaling
        private int _currentWeek => TimeManager.ElapsedDays / 7;

        // Called when the mod is initialized, but before game scene is ready
        public override void OnInitializeMelon()
        {
            _log = new Log("PaxStacks");
            _log.Msg(">> PaxStacks initialized. Awaiting scene 'Main'...");
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Triggers once a new scene is loaded — only proceed on game scene "Main"
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_started || scene.name != "Main")
            {
                _log.Msg($".. Scene '{scene.name}' loaded — still waiting for 'Main'.");
                return;
            }

            _log.Msg(">> Scene 'Main' detected. Starting PaxStacks...");
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StartMod();
            _started = true;
        }

        // One-time startup logic once scene is "Main"
        private void StartMod()
        {
            _log.Msg("=== PaxStacks START ===");

            // Hook up to game time events
            TimeManager.OnDayPass += HandleDayPass;
            TimeManager.OnWeekPass += HandleWeekPass;
            TimeManager.OnSleepStart += HandleSleepStart;

            // Load a safe list of items to use as drop contents
            _allItems = LoadValidItems();
            _log.Msg($"-- Loaded {_allItems.Count} usable items (manual list)");

            if (_allItems.Count == 0)
                _log.Warning("!! No items loaded — drops will be empty.");

            // Spawn the initial dead drop
            SpawnNewDeadDrop();
            _state = DropState.Spawned;
            _log.Msg("## State set to Spawned (Initial drop)");
        }

        /// <summary>
        /// Manually retrieves a known list of safe and spawnable item definitions.
        /// These are expected to always exist in the game.
        /// </summary>
        private List<ItemDefinition> LoadValidItems()
        {
            var ids = new[]
            {
                "cash",
                "soda",
                "weed_bag",
                "fertilizer",
                "clippers",
                "energy_drink"
            };

            var list = new List<ItemDefinition>();
            foreach (var id in ids)
            {
                var def = ItemManager.GetItemDefinition(id);
                if (def != null)
                {
                    list.Add(def);
                    _log.Msg($"-- Loaded item: {def.ID}");
                }
                else
                {
                    _log.Warning($"!! Item ID not found: {id}");
                }
            }

            return list;
        }

        /// <summary>
        /// Called when a new in-game day passes. Triggers a new drop.
        /// </summary>
        private void HandleDayPass()
        {
            _log.Msg(">> New in-game day started (OnDayPass)");

            if (_state == DropState.Spawned)
                _log.Msg("!! Previous drop still active — will be cleaned on sleep.");

            _state = DropState.Waiting;
            _log.Msg("## State set to Waiting");

            SpawnNewDeadDrop();
            _state = DropState.Spawned;
            _log.Msg("## State set to Spawned (New daily drop)");
        }

        /// <summary>
        /// Called when a new in-game week passes. Logs week info.
        /// </summary>
        private void HandleWeekPass()
        {
            _log.Msg($"-- Week {_currentWeek} began (ElapsedDays={TimeManager.ElapsedDays})");
        }

        /// <summary>
        /// Called when player sleeps — clears the previous day’s drop
        /// </summary>
        private void HandleSleepStart()
        {
            _log.Msg(">> Player started sleeping (OnSleepStart)");

            if (_state == DropState.Spawned && _activeDrop != null)
            {
                _log.Msg($"!! Cleaning up drop (GUID={_activeDrop.GUID})");
                _activeDrop = null;
            }

            _state = DropState.None;
            _log.Msg("## State set to None");
        }

        /// <summary>
        /// Main logic to pick a random dead drop and fill it with loot
        /// </summary>
        private void SpawnNewDeadDrop()
        {
            _log.Msg(">> Spawning new dead drop...");

            var drops = DeadDropManager.All.ToArray();
            if (drops.Length == 0)
            {
                _log.Warning("!! No dead drops found in the scene. Aborting spawn.");
                return;
            }

            _activeDrop = drops.PickOne(); // Random drop location
            Vector3 pos = _activeDrop.Position;
            string posStr = $"({pos.x:F1}, {pos.y:F1}, {pos.z:F1})";

            _log.Msg($"-- Drop selected:\n    GUID = {_activeDrop.GUID}\n    Location = {posStr}");

            FillDeadDropStorage(_activeDrop.Storage, posStr);
            NotifyPlayer(posStr);

            _log.Msg(">> SpawnNewDeadDrop() complete.");
        }

        /// <summary>
        /// Populates the drop storage with a number of items that scale with time
        /// </summary>
        private void FillDeadDropStorage(StorageInstance storage, string position)
        {
            _log.Msg(">> Filling drop storage...");

            if (_allItems.Count == 0)
            {
                _log.Warning("!! No items available to add to drop.");
                return;
            }

            int count = Mathf.Clamp(_currentWeek + 1, 2, 5); // More items each week
            var chosenItems = _allItems.PickMany(count);

            foreach (var def in chosenItems)
            {
                try
                {
                    int amount = Random.Range(1, def.StackLimit > 0 ? def.StackLimit + 1 : 2);
                    var instance = def.CreateInstance(amount);
                    if (instance != null)
                    {
                        storage.AddItem(instance);
                        _log.Msg($"-- Added: {def.ID} x{amount}");
                    }
                    else
                    {
                        _log.Warning($"!! Failed to create instance: {def.ID}");
                    }
                }
                catch (System.Exception ex)
                {
                    _log.Warning($"!! Error adding item {def.ID}: {ex.Message}");
                }
            }

            _log.Msg($">> Fill complete: {count} items at {position}");
        }

        /// <summary>
        /// Notifies the player of the drop location via NPC message
        /// </summary>
        private void NotifyPlayer(string position)
        {
            var contact = NPC.All.FirstOrDefault(n => n.ID == "DeadDropContact");
            if (contact == null)
            {
                contact = new DeadDropContact("DeadDropContact", "Mysterious", "Stranger");
                _log.Msg("-- Created NPC contact: DeadDropContact");
            }

            contact.SendTextMessage($"<b>PaxStacks</b>: Drop is at {position}. Don’t waste time. 🛰️");
            _log.Msg($"-- Message sent to player with drop location {position}");
        }

        /// <summary>
        /// Custom lightweight contact that handles sending player notifications
        /// </summary>
        private class DeadDropContact : NPC
        {
            public DeadDropContact(string id, string first, string last)
                : base(id, first, last) { }
        }
    }
}
