// InitMain.cs
using MelonLoader;
using S1API.GameTime;
using UnityEngine.SceneManagement;
using S1API.Logging;

namespace PaxStacks
{
    /// <summary>
    /// Main entry point for the PaxStacks mod.
    /// Handles game startup and initializes subsystems like MrStacks and DeadDrop management.
    /// </summary>
    public class InitMain : MelonMod
    {
        // PaxStacks-specific logger instance
        private static Log _log;

        /// <summary>
        /// Called when MelonLoader initializes the mod.
        /// Sets up logging and hooks into scene loading to wait for the main game scene.
        /// </summary>
        public override void OnInitializeMelon()
        {
            _log = new Log("PaxStacks");
            _log.Msg(">> PaxStacks initialized. Awaiting scene 'Main'...");

            // Wait for the actual game scene to load before starting mod logic
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// Triggered when any scene is loaded.
        /// Only proceeds if the "Main" game scene is loaded.
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Main") return;

            // Unhook to avoid duplicate calls
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _log.Msg(">> Scene 'Main' detected. Starting PaxStacks...");

            // Initialize subcomponents
            PaxDeadDrop.Initialize(_log);   // Handles loot logic and automatic drop timing
            MrStacks.Initialize(_log);      // Sets up Mr. Stacks contact and tier handling

            // Hook time-based game events
            TimeManager.OnDayPass += PaxDeadDrop.HandleDayPass;
            TimeManager.OnWeekPass += PaxDeadDrop.HandleWeekPass;
            TimeManager.OnSleepStart += PaxDeadDrop.HandleSleepStart;

            // Removed: PaxDeadDrop.ScheduleNextWeeklyDrop(); — handled inside HandleDayPass
        }
    }
}