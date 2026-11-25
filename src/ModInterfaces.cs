using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;

namespace SF6_TMP;

/// <summary>
///     Interface for modules that can be added to the TrainingModePlus plugin. Each module must implement the Init and
///     Unload methods to handle its own initialization and cleanup logic.
/// </summary>
public interface ITrainingModePlusModule
{
    string ConfigKey { get; }
    string DisplayName { get; }
    string Description { get; }

    void Init();

    void Unload();

    System.Collections.Generic.IReadOnlyList<UICustomElementRequestNode> CreateSettingsUINodes(bool isModuleEnabled)
    {
        return [];
    }
}