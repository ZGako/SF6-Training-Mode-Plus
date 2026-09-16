

using SF6_TMP.Core;

namespace SF6_TMP.TrainingModePlus.Modules;

public class TrainingParametersAndRandomizer : ITrainingModePlusModule
{
    public static TrainingParametersAndRandomizer Instance { get; private set; } = new TrainingParametersAndRandomizer();

    private TrainingParametersAndRandomizer() { }

    public void Init()
    {
        API.LogInfo("TrainingParametersAndRandomizer module initialized!");
    }

    public void Unload()
    {
        API.LogInfo("TrainingParametersAndRandomizer module unloaded!");
    }
}