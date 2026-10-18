namespace SF6_TMP.Modules.TrainingParametersAndRandomizer;

// TODO: Implement

public class TrainingParametersAndRandomizer : ITrainingModePlusModule
{
    private TrainingParametersAndRandomizer()
    {
    }

    public static TrainingParametersAndRandomizer Instance { get; private set; } = new();

    public void Init()
    {
        API.LogInfo("TrainingParametersAndRandomizer module initialized!");
    }

    public void Unload()
    {
        API.LogInfo("TrainingParametersAndRandomizer module unloaded!");
    }
}