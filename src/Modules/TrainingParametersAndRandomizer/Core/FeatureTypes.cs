using System.Reflection;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

public interface ITrainingFeatureBase
{
    public void Init();

    public void Unload();
}

public interface IRandomizableFeatureBase : ITrainingFeatureBase
{
    public bool IsRandomizedEnabled { get; }
    public void Randomize();
    public event Action? OnEnabledStateChanged;
}

public abstract class TrainingFeatureBase<T> : ITrainingFeatureBase
    where T : TrainingFeatureBase<T>
{
    private static readonly Lazy<T> LazyInstance = new(CreateInstance);

    protected readonly List<IUIModificationRequest> ModificationRequests = [];

    public static T Instance => LazyInstance.Value;

    public abstract void Init();

    public virtual void Unload()
    {
        try
        {
            if (ModificationRequests.Count <= 0) return;
            PauseMenuManager.ModifyUI(() =>
            {
                foreach (var request in ModificationRequests)
                    PauseMenuManager.UnregisterModification(request);
            });
            ModificationRequests.Clear();
        }
        catch (Exception ex)
        {
            API.LogError($"Error rebuilding UI during unload: {ex.Message}");
        }
    }

    private static T CreateInstance()
    {
        var type = typeof(T);

        // Look for a parameterless constructor that is NOT public
        var constructor = type.GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);

        // Enforce the private constructor rule at runtime
        if (constructor == null || !constructor.IsPrivate)
            throw new InvalidOperationException(
                $"Singleton failure: {type.Name} MUST have a private parameterless constructor.");

        // Invoke the private constructor to create the instance
        return (T)constructor.Invoke(null);
    }
}

public abstract class RandomizableFeatureBase<T> : TrainingFeatureBase<T>, IRandomizableFeatureBase
    where T : RandomizableFeatureBase<T>
{
    public bool IsRandomizedEnabled
    {
        get;
        protected set
        {
            if (field == value) return;
            field = value;
            OnEnabledStateChanged?.Invoke();
        }
    }

    public abstract void Randomize();

    public event Action? OnEnabledStateChanged;
}