using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

// basically just the caller of the randomizer stuff. Possesses its own UI elements

public partial class RandomizerManager : TrainingFeatureBase<RandomizerManager>
{
    private static readonly Guid RESTART_AND_RANDOMIZE_BUTTON = Guid.NewGuid();

    private static Config _config = new();

    private readonly List<IRandomizableFeatureBase> randomizableFeatures = [];

    private bool _isRandomizerEnabled;

    private UICustomElementRequestNode? _randomizerButton;

    private Guid? _randomizerResetShortcutId;

    private RandomizerManager()
    {
        TrainingParametersAndRandomizer.ConfigSection.BindSection(
            nameof(RandomizerManager),
            () => _config,
            cfg => _config = cfg,
            RandomizerManagerConfigJsonContext.Default.Config);
    }

    private void LoadFeatures()
    {
        // add all the features to the Features list here
        randomizableFeatures.Add(HealthBar.Instance);
        randomizableFeatures.Add(PositionalSettings.Instance);
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
            RandomizeFeatures();

            GameSingletonManager.TrainingManager!.MenuFunc.RestartGame(false);

            return true;
        }));

        var appendButtonReq = new PauseMenuAppendElementsRequest([(1, _randomizerButton)]);

        _randomizerResetShortcutId = ShortcutManager.RegisterShortcut(
            new ShortcutManager.CustomShortcutNode
            {
                ItemMessage = CustomMessage.FromKey("RandomizerButtonTitle"),
                GuideMessage = CustomMessage.FromKey("RandomizerButtonDescription"),
                IsKeyInput = true,
                DefaultButton = via.hid.GamePadButton.CLeft,
                IsDefaultFunc = true,
                // get from the config
                Button = (via.hid.GamePadButton)_config.Button,
                Key = (via.hid.KeyboardKey)_config.Key,
                IsFunc = _config.IsFunc,
                IsActive = _config.IsActive
                // ShortcutType = existing type → adds a duplicate entry instead of a new type
            },
            () =>
            {
                RandomizeFeatures();
                GameSingletonManager.TrainingManager!.IsReqRefresh = true;
            }, () => { }, () => { },
            liveNode =>
            {
                API.LogInfo("Unregistering shortcut and saving");
                _config.Button = (uint)liveNode.Button;
                _config.Key = (uint)liveNode.Key;
                _config.IsActive = liveNode.IsActive;
                _config.IsFunc = liveNode.IsFunc;
            });

        try
        {
            PauseMenuManager.RegisterModification([1], appendButtonReq);
        }
        catch (Exception ex)
        {
            API.LogError($"Error registering modifications: {ex.Message}");
        }

        ModificationRequests.Add(appendButtonReq);
    }

    private void RandomizeFeatures()
    {
        foreach (var feature in randomizableFeatures) feature.Randomize();
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

        if (_randomizerResetShortcutId != null)
            ShortcutManager.UnregisterShortcut(_randomizerResetShortcutId.Value);

        randomizableFeatures.Clear();
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Config))]
    private partial class RandomizerManagerConfigJsonContext : JsonSerializerContext;

    private class Config
    {
        public uint Button { get; set; } = (uint)via.hid.GamePadButton.None;
        public uint Key { get; set; } = (uint)via.hid.KeyboardKey.None;
        public bool IsFunc { get; set; }
        public bool IsActive { get; set; }
    }
}