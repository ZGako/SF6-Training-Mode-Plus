using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer;

public class TrainingParametersAndRandomizer : ITrainingModePlusModule
{
    private static readonly List<ITrainingFeature> Features = [];

    private TrainingParametersAndRandomizer()
    {
        Features.Add(RandomBlockChance.Instance);
        // Features.Add(RandomizerManager.Instance);
        // Features.Add(HealthBar.Instance);
    }

    public static TrainingParametersAndRandomizer Instance { get; private set; } = new();

    public void Init()
    {
        API.LogInfo("TrainingParametersAndRandomizer module initialized!");

        foreach (var feature in Features) feature.Init();
    }

    public void Unload()
    {
        API.LogInfo("TrainingParametersAndRandomizer module unloaded!");
        foreach (var feature in Features) feature.Unload();
        Features.Clear();
    }
}