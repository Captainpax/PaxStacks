// deaddrop.cs
using S1API.DeadDrops;
using S1API.GameTime;
using S1API.Items;
using S1API.Logging;
using S1API.Storages;
using S1API.Internal.Utils;
using S1API.Entities;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PaxStacks
{
    /// <summary>
    /// Handles PaxStacks dead drop lifecycle, including weekly auto-drops and player-triggered manual drops.
    /// </summary>
    public static class PaxDeadDrop
    {
        /// <summary>
        /// Tracks the current spawn state.
        /// </summary>
        private enum DropState { None, Spawned }

        private static DropState _state = DropState.None;
        private static DeadDropInstance _activeDrop;
        private static Log _log;

        private static int _lastAutoDropWeek = -1;
        private static int _scheduledDay = -1;

        /// <summary>
        /// Current game week based on elapsed days.
        /// </summary>
        public static int CurrentWeek => TimeManager.ElapsedDays / 7;

        /// <summary>
        /// Initializes PaxDeadDrop and logs setup.
        /// </summary>
        public static void Initialize(Log logger)
        {
            _log = logger;
            _log.Msg(">> PaxDeadDrop initialized");
        }

        /// <summary>
        /// Called once per in-game day. Checks if today is the scheduled auto-drop day.
        /// </summary>
        public static void HandleDayPass()
        {
            _log.Msg(">> New in-game day (HandleDayPass)");

            int today = TimeManager.ElapsedDays % 7;

            // If we haven't spawned the auto-drop for this week yet
            if (_lastAutoDropWeek != CurrentWeek)
            {
                _log.Msg($"-- Auto-drop pending for week {CurrentWeek}, scheduled for day {_scheduledDay}");

                // If today matches the scheduled day, spawn it
                if (today == _scheduledDay)
                {
                    int tier = Random.Range(1, 4); // Tier 1–3
                    _log.Msg($"-- Auto-drop trigger matched today={today}, rolling tier={tier}");
                    SpawnNewDeadDrop(tier);
                    _lastAutoDropWeek = CurrentWeek;
                }
            }
        }

        /// <summary>
        /// Called at the start of a new in-game week. Schedules a new random drop day.
        /// </summary>
        public static void HandleWeekPass()
        {
            _scheduledDay = Random.Range(0, 7); // Schedule auto-drop for a random day (0–6)
            _log.Msg($"-- Week {CurrentWeek} began (ElapsedDays={TimeManager.ElapsedDays})");
            _log.Msg($"-- Scheduled auto-drop for day {_scheduledDay}");
        }

        /// <summary>
        /// Clears the drop when the player goes to sleep.
        /// </summary>
        public static void HandleSleepStart()
        {
            if (_state == DropState.Spawned && _activeDrop != null)
            {
                _log.Msg($"!! Cleaning up drop (GUID={_activeDrop.GUID})");
                _activeDrop = null;
            }

            _state = DropState.None;
        }

        /// <summary>
        /// Called externally (e.g. by MrStacks) to manually request a dead drop of a specific tier.
        /// Only allowed tiers based on week progression will succeed.
        /// </summary>
        public static bool TryManualDrop(int requestedTier)
        {
            if (requestedTier == 1)
                return SpawnNewDeadDrop(requestedTier);

            if (requestedTier == 2 && CurrentWeek >= 2)
                return SpawnNewDeadDrop(requestedTier);

            if (requestedTier == 3 && CurrentWeek >= 3)
                return SpawnNewDeadDrop(requestedTier);

            _log.Msg($"!! Manual drop for tier {requestedTier} blocked due to insufficient week ({CurrentWeek})");
            return false;
        }

        /// <summary>
        /// Spawns the drop and fills it using MrStacks tier logic.
        /// </summary>
        private static bool SpawnNewDeadDrop(int tier)
        {
            _log.Msg($">> Spawning dead drop for tier {tier}...");

            var drops = DeadDropManager.All.ToArray();
            if (drops.Length == 0)
            {
                _log.Warning("!! No dead drops found in the scene. Aborting spawn.");
                return false;
            }

            _activeDrop = drops.PickOne();
            Vector3 pos = _activeDrop.Position;
            string posStr = $"({pos.x:F1}, {pos.y:F1}, {pos.z:F1})";

            _log.Msg($"-- Drop selected:\n    GUID = {_activeDrop.GUID}\n    Location = {posStr}");

            FillDeadDropStorage(_activeDrop.Storage, posStr, tier);
            MrStacks.NotifyPlayerDrop(posStr, tier);

            _state = DropState.Spawned;
            return true;
        }

        /// <summary>
        /// Fills the drop location storage with a selection of tiered items.
        /// </summary>
        private static void FillDeadDropStorage(StorageInstance storage, string position, int tier)
        {
            _log.Msg($">> Filling drop with tier {tier} loot...");

            var items = MrStacks.GetLootForTier(tier);
            if (items.Count == 0)
            {
                _log.Warning("!! No items retrieved for tier, drop will be empty.");
                return;
            }

            int count = Random.Range(2, 6); // 2–5 items
            var selected = items.PickMany(count);

            foreach (var def in selected)
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
    }
}
