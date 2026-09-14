namespace SF6_TMP.TrainingModePlus;

/// <summary>
/// Interface for modules that can be added to the TrainingModePlus plugin. Each module must implement the Init and Unload methods to handle its own initialization and cleanup logic.
/// </summary>
public interface ITrainingModePlusModule
{
    void Init();

    void Unload();
}

