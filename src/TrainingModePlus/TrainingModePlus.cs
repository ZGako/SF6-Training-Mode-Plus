// Core usings
using SF6_TMP.Core;
using SF6_TMP.Core.UI;
using SF6_TMP.Core.UI.TrainingPauseMenu;

// TrainingModePlus usings
using SF6_TMP.TrainingModePlus.Modules;

namespace SF6_TMP.TrainingModePlus;

/// <summary>
/// The main entry point for the TrainingModePlus plugin. This class is responsible for initializing the plugin, managing its modules, and handling the lifecycle of the TrainingManager.
/// </summary>
public static class TrainingModePlus
{
    private static readonly List<ITrainingModePlusModule> Modules = [];

    [PluginEntryPoint]
    private static void PluginEntryPoint()
    {
        // Logging stuff
        API.LogLevel = 0;
        API.LogWarning("Loading TrainingModePlus C# plugin...");

        // Register the TrainingManager singleton with the GameSingletonRegistry
        GameSingletonRegistry.RegisterTrainingManager(
            onReady: () =>
            {
                API.LogInfo("TrainingManager initialized!");

                foreach (var module in Modules)
                {
                    module.Init();
                }

                PauseMenuManager.RebuildUI();
            },
            onRelease: () =>
            {
                CleanUpTrainingState();
                API.LogInfo("TrainingManager released. Mod state cleaned up.");
            }
        );

        // Register modules
        Modules.Add(TestingRefactor.Instance);
        Modules.Add(GameSpeedPlus.Instance);
    }

    [PluginExitPoint]
    private static void PluginExitPoint()
    {
        // Clean up static states
        CleanUpTrainingState();
        GameSingletonRegistry.Clear();
        Modules.Clear();

        API.LogInfo("Unloading TrainingModePlus C# plugin...");
    }

    /// <summary>
    /// Cleans up the training state by unloading all registered modules, resetting the TrainingManager and its initialization state, and clearing any UI dispatchers related to the training pause menu. 
    /// This method is called when the plugin is unloaded or when the TrainingManager is released.
    /// </summary>
    private static void CleanUpTrainingState()
    {
        foreach (var module in Modules)
        {
            module.Unload();
        }

        PauseMenuManager.RebuildUI();

        UIHelpers.ClearTrainingPauseMenuDispatchers();
    }

}
