using REFrameworkNET;
using REFrameworkNET.Callbacks;
using REFrameworkNET.Attributes;
using REFrameworkNET.Collections;
using System.Collections.Generic;

using SF6_Training_Mode_Plus.Modules;

namespace SF6_Training_Mode_Plus.Core;

public interface ITrainingModePlusModule
{
    void Init();
}

public class TrainingModePlus
{

    private static readonly List<ITrainingModePlusModule> _modules = [];

    public static bool IsTrainingManagerInitialized { get; private set; } = false;

    public static app.training.TrainingManager? TrainingManager { get; private set; }

    [PluginEntryPoint]
    public static void Main()
    {
        // Logging stuff
        API.LogLevel = 0;
        API.LogInfo("Loading TrainingModePlus C# plugin...");
        // API.LogLevel = (LogLevel)1;

        // Register modules
        _modules.Add(new GameSpeedPlus());
    }

    [PluginExitPoint]
    public static void OnUnload()
    {
        API.LogInfo("Unloading TrainingModePlus C# plugin...");

        // Clean up static states
        IsTrainingManagerInitialized = false;
        TrainingManager = null;
        _modules.Clear();

        // Cleanup added UI and stuff like that
    }



    // Code to run every frame before the game updates
    [Callback(typeof(UpdateBehavior), CallbackType.Pre)]
    public static void OnUpdate()
    {

        if (IsTrainingManagerInitialized) return;

        TrainingManager = API.GetManagedSingletonT<app.training.TrainingManager>();

        if (TrainingManager == null) return;

        if (!TrainingManager.IsInit) return;

        API.LogInfo("TrainingManager initialized!");

        IsTrainingManagerInitialized = true;

        // Initialize all modules
        foreach (var module in _modules)
        {
            module.Init();
        }

    }

}
