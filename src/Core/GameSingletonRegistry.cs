using System.Threading;


namespace SF6_TMP.Core;

public static class GameSingletonRegistry
{
    // A dedicated lock object to synchronize cross-thread dictionary access
    private static readonly Lock RegistryLock = new();

    public enum GameSingletonTypes
    {
        TrainingManager,
        UIAgentManager,
        // Add others here later
    }

    // Publicly accessible singleton instances
    public static app.training.TrainingManager? TrainingManager { get; private set; }

    public static app.UIAgentManager? UIAgentManager { get; private set; }

    private static readonly Dictionary<GameSingletonTypes, bool> SingletonReadyStates = [];
    private static readonly Dictionary<GameSingletonTypes, Tuple<Func<bool>, Action, Action?>> RegisteredSingletons = [];

    [Callback(typeof(UpdateBehavior), CallbackType.Pre)]
    private static void OnUpdateBehaviorCallback()
    {
        // Safe iteration: We lock the registry while checking/updating states
        lock (RegistryLock)
        {
            foreach (GameSingletonTypes singletonType in Enum.GetValues<GameSingletonTypes>())
            {
                if (SingletonReadyStates.TryGetValue(singletonType, out bool isReady) && isReady)
                {
                    continue; // Skip if already ready
                }

                if (RegisteredSingletons.TryGetValue(singletonType, out var actions))
                {
                    var (checkIfReady, onReady, _) = actions;

                    if (checkIfReady())
                    {
                        SingletonReadyStates[singletonType] = true;
                        onReady.Invoke();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Registers the TrainingManager singleton with the GameSingletonRegistry.
    /// </summary>
    public static void RegisterTrainingManager(Action onReady, Action? onRelease = null)
    {
        lock (RegistryLock)
        {
            RegisteredSingletons[GameSingletonTypes.TrainingManager] = new Tuple<Func<bool>, Action, Action?>(() =>
            {
                // Leveraging typed proxies for compile-time safety[cite: 4]
                var tm = API.GetManagedSingletonT<app.training.TrainingManager>();
                if (tm == null) return false;
                if (!tm.IsInit) return false;

                TrainingManager = tm;
                return true;
            }, onReady, onRelease);
        }
    }

    [MethodHook(typeof(app.training.TrainingManager), "Release", MethodHookType.Pre)]
    private static PreHookResult OnTrainingManagerReleasePre(Span<ulong> args)
    {
        // Thread-safe state cleanup
        lock (RegistryLock)
        {
            if (SingletonReadyStates.TryGetValue(GameSingletonTypes.TrainingManager, out bool isReady) && isReady)
            {
                API.LogInfo("TrainingManager released. Cleaning up mod state...");
                SingletonReadyStates[GameSingletonTypes.TrainingManager] = false;
                TrainingManager = null;

                if (RegisteredSingletons.TryGetValue(GameSingletonTypes.TrainingManager, out var actions))
                {
                    var (_, _, onRelease) = actions;
                    onRelease?.Invoke();
                }
            }
        }
        return PreHookResult.Continue;
    }

    public static void RegisterUIAgentManager(Action onReady, Action? onRelease = null)
    {
        lock (RegistryLock)
        {
            RegisteredSingletons[GameSingletonTypes.UIAgentManager] = new Tuple<Func<bool>, Action, Action?>(() =>
            {
                var uiAgentManager = API.GetManagedSingletonT<app.UIAgentManager>();
                if (uiAgentManager == null) return false;

                UIAgentManager = uiAgentManager;
                return true;
            }, onReady, onRelease);
        }
    }

    /// <summary>
    /// Call this from your [PluginExitPoint] to prevent memory leaks during hot-reloads.
    /// </summary>
    public static void Clear()
    {
        lock (RegistryLock)
        {
            SingletonReadyStates.Clear();
            RegisteredSingletons.Clear();
            TrainingManager = null;
        }
    }
}