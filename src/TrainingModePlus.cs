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
using SF6_Plugin_Core.UI.TrainingPauseMenu;

// TrainingModePlus usings
using SF6_TMP.Modules;
using System.Reflection;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Helpers;

namespace SF6_TMP;

// FIXME: refactor


public static class TrainingModePlus
{
    private static readonly List<ITrainingModePlusModule> Modules = [];


    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializePlugin()
    {
        // Logging stuff
        API.LogLevel = 0;
        API.LogWarning("Loading TrainingModePlus C# plugin...");

        // Register the TrainingManager singleton with the GameSingletonRegistry
        GameSingletonManager.RegisterTrainingManager(
            onReady: () =>
            {
                API.LogInfo("TrainingManager initialized!");

                foreach (var module in Modules)
                {
                    module.Init();
                }
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
        // Modules.Add(TrainingParametersAndRandomizer.Instance);
    }

    public static void UnloadPlugin()
    {
        API.LogInfo("Unloading TrainingModePlus C# plugin...");
        try
        {
            CleanUpTrainingState();
        }
        catch (Exception ex)
        {
            API.LogError($"Exception during UnloadPlugin: {ex}");
        }
    }

    private static void CleanUpTrainingState()
    {
        foreach (var module in Modules)
        {
            module.Unload();
        }

        UIHelpers.ClearTrainingPauseMenuDispatchers();
    }


    ///
    ///  Default plugin entry and exit points
    /// 

    [PluginEntryPoint]
    public static void PluginEntryPoint()
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

    [PluginExitPoint]
    public static void PluginExitPoint()
    {
        try
        {
            UnloadPlugin();
        }
        catch (Exception ex)
        {
            API.LogError($"Exception during PluginExitPoint: {ex}");
        }

        try
        {
            var currentALC = AssemblyLoadContext.GetLoadContext(Assembly.GetExecutingAssembly());

            currentALC?.Resolving -= OnResolvingCore;
        }
        catch (Exception ex)
        {
            API.LogError($"Exception during AssemblyLoadContext cleanup: {ex}");
        }
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
                        return loadedAssembly;
                    }
                }
            }
        }
        API.LogError($"Failed to resolve assembly: {assemblyName.Name}. Ensure that the SF6_Plugin_Core is loaded and accessible.");
#pragma warning disable CS8603 // Possible null reference return.
        return null; // Let default resolution fail if not found
#pragma warning restore CS8603 // Possible null reference return.
    }

}
