using System;
using REFrameworkNET;
using REFrameworkNET.Callbacks;
using REFrameworkNET.Attributes;
using REFrameworkNET.Collections;
using System.Collections.Generic;

using SF6_Training_Mode_Plus.TrainingModePlus.Modules;
using SF6_Training_Mode_Plus.Core;
using SF6_Training_Mode_Plus.Core.UI;

namespace SF6_Training_Mode_Plus.TrainingModePlus;

public class TrainingModePlus
{

    private static readonly List<ITrainingModePlusModule> Modules = [];

    public static bool IsTrainingManagerInitialized { get; private set; } = false;

    public static app.training.TrainingManager? TrainingManager { get; private set; }

    [PluginEntryPoint]
    private static void Main()
    {
        // Logging stuff
        API.LogLevel = 0;
        API.LogInfo("Loading TrainingModePlus C# plugin...");
        // API.LogLevel = (LogLevel)1;

        // Register modules
        Modules.Add(new GameSpeedPlus());
    }

    [PluginExitPoint]
    private static void OnUnload()
    {
        API.LogInfo("Unloading TrainingModePlus C# plugin...");

        // Clean up static states
        CleanUpTrainingState();
        Modules.Clear();
    }

    // Code to run every frame before the game updates
    [Callback(typeof(UpdateBehavior), CallbackType.Pre)]
    private static void OnUpdate()
    {

        if (IsTrainingManagerInitialized) return;

        TrainingManager = API.GetManagedSingletonT<app.training.TrainingManager>();

        if (TrainingManager == null) return;

        if (!TrainingManager.IsInit) return;

        API.LogInfo("TrainingManager initialized!");

        IsTrainingManagerInitialized = true;

        // Initialize all modules
        foreach (var module in Modules)
        {
            module.Init();
        }
    }

    [MethodHook(typeof(app.training.TrainingManager), "Release", MethodHookType.Pre)]
    private static PreHookResult OnTrainingManagerReleasePre(Span<ulong> args)
    {
        if (IsTrainingManagerInitialized)
        {
            API.LogInfo("TrainingManager released. Cleaning up mod state...");
            CleanUpTrainingState();
        }
        return PreHookResult.Continue;
    }

    // Helper method to avoid repeating cleanup code in OnUnload and OnTrainingManagerReleasePre
    private static void CleanUpTrainingState()
    {
        foreach (var module in Modules)
        {
            module.Unload();
        }

        IsTrainingManagerInitialized = false;
        TrainingManager = null;

        UIHelpers.ClearTrainingPauseMenuDispatchers();
    }

}
