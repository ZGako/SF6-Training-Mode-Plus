using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer;

public class TrainingParametersAndRandomizer : ITrainingModePlusModule
{
    private readonly List<ITrainingFeatureBase> _features = [];
    private bool _isInitialized;

    private TrainingParametersAndRandomizer()
    {
        _features.Add(RandomizerManager.Instance);
        _features.Add(RandomBlockChance.Instance);
    }

    internal static ConfigSection ConfigSection =>
        ModConfig.Instance.GetOrCreateSection(nameof(TrainingParametersAndRandomizer));

    public static TrainingParametersAndRandomizer Instance { get; private set; } = new();

    public string ConfigKey => nameof(TrainingParametersAndRandomizer);
    public string DisplayName => "Training Parameters & Randomizer";
    public string Description => "Enable or disable parameter tuning and randomization features.";

    public void Init()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        API.LogInfo("TrainingParametersAndRandomizer module initialized!");
        foreach (var feature in _features) feature.Init();
    }

    public void Unload()
    {
        if (!_isInitialized) return;
        _isInitialized = false;

        API.LogInfo("TrainingParametersAndRandomizer module unloaded!");
        foreach (var feature in _features) feature.Unload();
    }

    public System.Collections.Generic.IReadOnlyList<UICustomElementRequestNode> CreateSettingsUINodes(
        bool isModuleEnabled)
    {
        var nodes = new List<UICustomElementRequestNode>();
        foreach (var feature in _features)
            nodes.AddRange(feature.CreateSettingsUINodes(isModuleEnabled));

        return nodes;
    }
}