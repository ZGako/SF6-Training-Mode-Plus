using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

// basically just the caller of the randomizer stuff. Possesses its own UI elements

public class RandomizerManager : TrainingFeatureBase<RandomizerManager>
{
    private static readonly Guid RESTART_AND_RANDOMIZE_BUTTON = Guid.NewGuid();

    private readonly List<IRandomizableFeatureBase> randomizableFeatures = [];

    private bool _isRandomizerEnabled;

    private UICustomElementRequestNode? _randomizerButton;

    private RandomizerManager()
    {
    }

    private void LoadFeatures()
    {
        // add all the features to the Features list here
        randomizableFeatures.Add(HealthBar.Instance);
    }

    public override void Init()
    {
        // add the randomized features here
        LoadFeatures();

        _isRandomizerEnabled = false;

        foreach (var feature in randomizableFeatures)
        {
            feature.Init();
            feature.OnEnabledStateChanged += UpdateIsRandomizerEnabled;

            _isRandomizerEnabled |= feature.IsRandomizedEnabled;
        }

        using var _ = LocalizationManager.UseFileScope("TrainingModePlus");

        _randomizerButton = new UICustomElementRequestNode(RESTART_AND_RANDOMIZE_BUTTON,
            new RowButtonInitializer(CustomMessage.FromKey("RandomizerButtonTitle"),
                CustomMessage.FromKey("RandomizerButtonDescription"),
                _isRandomizerEnabled));


        _randomizerButton.AddHook(new FunctionHookRequest((_, _, _) =>
        {
            foreach (var feature in randomizableFeatures) feature.Randomize();

            GameSingletonManager.TrainingManager!._IsReqRefresh = true;

            return true;
        }));

        var appendButtonReq = new PauseMenuAppendElementsRequest([(1, _randomizerButton)]);

        try
        {
            PauseMenuManager.ModifyUI(() => { PauseMenuManager.RegisterModification([1], appendButtonReq); });
        }
        catch (Exception ex)
        {
            API.LogError($"Error registering modifications: {ex.Message}");
        }

        ModificationRequests.Add(appendButtonReq);
    }

    private void UpdateIsRandomizerEnabled()
    {
        _isRandomizerEnabled = randomizableFeatures.Any(f => f.IsRandomizedEnabled);

        var menuData = _randomizerButton?.GetBuiltElement() ??
                       throw new InvalidOperationException("Couldn't retrieve the randomizer button UI element");

        menuData.IsEnabled = _isRandomizerEnabled;
    }

    public override void Unload()
    {
        base.Unload();
        foreach (var feature in randomizableFeatures)
        {
            feature.OnEnabledStateChanged -= UpdateIsRandomizerEnabled;
            feature.Unload();
        }

        randomizableFeatures.Clear();
    }
}