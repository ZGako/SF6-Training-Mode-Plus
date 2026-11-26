global using System;
global using System.Collections.Generic;
global using System.Runtime.InteropServices;
global using System.Linq;
global using REFrameworkNET;
global using REFrameworkNET.Attributes;
global using REFrameworkNET.Collections;
global using REFrameworkNET.Callbacks;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using SF6_Plugin_Core;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Localization;
using SF6_TMP.Modules;
using SF6_TMP.Modules.TrainingParametersAndRandomizer;

// Core usings

// TrainingModePlus usings

namespace SF6_TMP;

public static class TrainingModePlus
{
    public static readonly string DirectoryPath = Path.Combine("reframework", "data", "SF6_TrainingModePlus");

    private static readonly string LocalizationFilePath =
        Path.Combine(DirectoryPath, "SF6_TrainingModePlus_Localization.json");

    private static readonly List<ITrainingModePlusModule> Modules = [];
    private static bool _isTrainingSessionActive;
    private static bool _isUnloaded;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InitializePlugin()
    {
        _isUnloaded = false;
        API.LogLevel = 0;
        API.LogWarning("Loading TrainingModePlus C# plugin...");

        LocalizationManager.LoadTranslations(LocalizationFilePath, "TrainingModePlus");

        Modules.Add(GameSpeedPlus.Instance);
        Modules.Add(TrainingParametersAndRandomizer.Instance);

        GameSingletonManager.RegisterTrainingManager(
            OnTrainingManagerReady,
            OnTrainingManagerRelease
        );

        CoreLifecycle.OnCoreUnloading += PluginExitPoint;

        SettingsTabManager.EnsureConfigBound();
        ModConfig.Load();
    }

    private static void OnTrainingManagerReady()
    {
        var tm = GameSingletonManager.TrainingManager;
        if (tm == null) return;

        var gameMode = tm.GetGameMode();
        if (gameMode != app.training.TrainingManager.GameMode.TRAINING)
        {
            API.LogInfo($"TrainingManager initialized in {gameMode} mode; skipping training-only modules.");
            return;
        }

        API.LogInfo("TrainingManager initialized!");
        InitTrainingMode();
    }

    private static void InitTrainingMode()
    {
        _isTrainingSessionActive = true;
        ModConfig.Load();
        foreach (var module in Modules)
            if (SettingsTabManager.IsModuleEnabled(module))
                module.Init();

        SettingsTabManager.Init(Modules);
    }

    private static void OnTrainingManagerRelease()
    {
        if (!_isTrainingSessionActive) return;

        CleanUpTrainingState();
        ModConfig.Save();
        API.LogInfo("TrainingManager released. Mod state cleaned up.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnloadPlugin()
    {
        if (_isUnloaded) return;
        _isUnloaded = true;

        API.LogInfo("Unloading TrainingModePlus C# plugin...");

        // Unsubscribe from Core events/singletons so Core holds zero references to this assembly
        CoreLifecycle.OnCoreUnloading -= PluginExitPoint;
        GameSingletonManager.UnregisterTrainingManager(OnTrainingManagerReady, OnTrainingManagerRelease);

        // Clean up active module state
        try
        {
            CleanUpTrainingState();
            SettingsTabManager.Clear();
            ModConfig.Clear();
        }
        catch (Exception ex)
        {
            API.LogError($"Exception during UnloadPlugin: {ex}");
        }
        finally
        {
            Modules.Clear();
        }
    }

    private static void CleanUpTrainingState()
    {
        if (!_isTrainingSessionActive) return;
        _isTrainingSessionActive = false;

        SettingsTabManager.Unload();

        foreach (var module in Modules)
            try
            {
                module.Unload();
            }
            catch (Exception ex)
            {
                API.LogError($"Exception unloading module {module.GetType().Name}: {ex}");
            }
    }

    /// Default plugin entry and exit points
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
            // Search all AssemblyLoadContexts to find where REFramework loaded the Core plugin
            foreach (var alc in AssemblyLoadContext.All)
            foreach (var loadedAssembly in alc.Assemblies)
                if (loadedAssembly.GetName().Name == "00_SF6PluginCore")
                    return loadedAssembly;

        API.LogError(
            $"Failed to resolve assembly: {assemblyName.Name}. Ensure that the SF6_Plugin_Core is loaded and accessible.");
#pragma warning disable CS8603 // Possible null reference return.
        return null; // Let default resolution fail if not found
#pragma warning restore CS8603 // Possible null reference return.
    }
}