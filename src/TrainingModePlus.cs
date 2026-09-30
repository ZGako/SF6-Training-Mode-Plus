global using System;
global using System.Collections.Generic;
global using System.Runtime.InteropServices;
global using System.Linq;

global using REFrameworkNET;
global using REFrameworkNET.Attributes;
global using REFrameworkNET.Collections;
global using REFrameworkNET.Callbacks;


using System.Runtime.CompilerServices;
using System.Runtime.Loader;
// Core usings
using SF6_Plugin_Core;
using SF6_Plugin_Core.UI;
using SF6_Plugin_Core.UI.TrainingPauseMenu;

// TrainingModePlus usings
using SF6_TMP.Modules;
using System.Reflection;

namespace SF6_TMP;

/// <summary>
/// The main entry point for the TrainingModePlus plugin. This class is responsible for initializing the plugin, managing its modules, and handling the lifecycle of the TrainingManager.
/// </summary>
public static class TrainingModePlus
{
    private static readonly List<ITrainingModePlusModule> Modules = [];

    [PluginEntryPoint]
    private static void PluginEntryPoint()
    {
        var currentALC = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());
        if (currentALC == null)
        {
            API.LogError("Failed to get the current AssemblyLoadContext. Plugin initialization aborted.");
            return;
        }

        currentALC.Resolving += OnResolvingCore;


        InitializePlugin();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializePlugin()
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

        GameSingletonRegistry.RegisterUIAgentManager(
            onReady: () => { TestingPrefab.Instance.Init(); }
        );

        GameSingletonRegistry.RegisterUIPrefabManager(
            onReady: () => { }
        );

        // Register modules
        Modules.Add(TestingRefactor.Instance);
        Modules.Add(GameSpeedPlus.Instance);
        // Modules.Add(TrainingParametersAndRandomizer.Instance);
    }

    [PluginExitPoint]
    private static void PluginExitPoint()
    {
        // Clean up static states
        CleanUpTrainingState();

        // unload the input history thing
        TestingPrefab.Instance.Unload();

        // Clear the singleton registry and module list
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

    private static Assembly OnResolvingCore(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        if (assemblyName.Name == "00_SF6PluginCore")
        {
            // Search all AssemblyLoadContexts to find where REFramework loaded the Core plugin
            foreach (var alc in AssemblyLoadContext.All)
            {
                foreach (var loadedAssembly in alc.Assemblies)
                {
                    if (loadedAssembly.GetName().Name == "00_SF6PluginCore")
                    {
                        return loadedAssembly; // We found it in memory! Hand it back to the runtime.
                    }
                }
            }
        }
        return null; // Let default resolution fail if not found
    }

}
