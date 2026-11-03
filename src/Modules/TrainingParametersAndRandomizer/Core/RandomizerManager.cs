namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

// basically just the caller of the randomizer stuff. Possesses its own UI elements

public class RandomizerManager : ITrainingFeature
{
    private readonly List<IRandomizableFeature> randomizableFeatures = [];

    private bool _isRandomizerEnabled;

    private RandomizerManager()
    {
    }

    public static RandomizerManager Instance { get; private set; } = new();

    public void Init()
    {
        throw new NotImplementedException();
    }

    public void Unload()
    {
        throw new NotImplementedException();
    }

    public void UpdateRandomizedFeatures()
    {
        _isRandomizerEnabled = false;
        if (!randomizableFeatures.Any(feature => feature.IsRandomizedEnabled)) return;
        _isRandomizerEnabled = true;
    }
}