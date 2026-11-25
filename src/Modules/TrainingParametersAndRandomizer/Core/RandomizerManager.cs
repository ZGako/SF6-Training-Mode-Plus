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
    private const string EnabledOptionLabel = "<COLOR preset=\"FGM_Accent\">Enabled</COLOR>";

    private static readonly Guid RESTART_AND_RANDOMIZE_BUTTON = Guid.NewGuid();
    private static readonly Guid ENABLE_RANDOMIZED_SETTINGS_SPINNER = Guid.NewGuid();
    private static readonly Guid ENABLE_RANDOMIZED_SETTINGS_OFF = Guid.NewGuid();
    private static readonly Guid ENABLE_RANDOMIZED_SETTINGS_ON = Guid.NewGuid();

    private static Config _config = new();

    private readonly List<IRandomizableFeatureBase> randomizableFeatures = [];

    private bool _isInitialized;
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

        LoadFeatures();
    }

    private void LoadFeatures()
    {
        // add all the features to the Features list here
        randomizableFeatures.Add(HealthBar.Instance);
        randomizableFeatures.Add(DriveGauge.Instance);
        randomizableFeatures.Add(SuperGauge.Instance);
        randomizableFeatures.Add(PositionalSettings.Instance);
        randomizableFeatures.Add(UniqueParameters.Instance);
    }

    public override System.Collections.Generic.IReadOnlyList<UICustomElementRequestNode> CreateSettingsUINodes(
        bool isParentEnabled)
    {
        var spinner = new UICustomElementRequestNode(
            ENABLE_RANDOMIZED_SETTINGS_SPINNER,
            new SpinnerInitializer(
                new CustomMessage("Enable Randomized Settings"),
                new CustomMessage(
                    "Enable or disable randomized training parameters, randomizer submenus, and the randomize shortcut."),
                isParentEnabled));

        spinner.AddHook(new SpinBoxHookRequest(_ =>
            _config.RandomizerEnabled ? ENABLE_RANDOMIZED_SETTINGS_ON : ENABLE_RANDOMIZED_SETTINGS_OFF));

        var disableItem = new UICustomElementRequestNode(
            ENABLE_RANDOMIZED_SETTINGS_OFF,
            new SpinnerItemTextInitializer(new CustomMessage("Disabled")));
        disableItem.AddHook(new FunctionHookRequest((_, _, _) =>
        {
            SetRandomizerEnabled(false);
            ModConfig.Save();
            return false;
        }));

        var enableItem = new UICustomElementRequestNode(
            ENABLE_RANDOMIZED_SETTINGS_ON,
            new SpinnerItemTextInitializer(new CustomMessage(EnabledOptionLabel)));
        enableItem.AddHook(new FunctionHookRequest((_, _, _) =>
        {
            SetRandomizerEnabled(true);
            ModConfig.Save();
            return false;
        }));

        spinner.AddChildren([disableItem, enableItem]);
        return [spinner];
    }

    public override void Init()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _isRandomizerEnabled = false;

        foreach (var feature in randomizableFeatures)
        {
            feature.SetRandomizerEnabled(_config.RandomizerEnabled);
            feature.Init();
            feature.OnEnabledStateChanged += UpdateIsRandomizerEnabled;

            _isRandomizerEnabled |= feature.IsRandomizedEnabled;
        }

        _isRandomizerEnabled &= _config.RandomizerEnabled;

        if (_config.RandomizerEnabled)
            RegisterRandomizerButtonAndShortcut();
    }

    private void RegisterRandomizerButtonAndShortcut()
    {
        if (_randomizerButton != null) return;

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

                GameSingletonManager.TrainingManager!.MenuFunc.RestartGame(false);
                GameSingletonManager.TrainingManager.Apply(true);
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
            ModificationRequests.Add(appendButtonReq);
        }
        catch (Exception ex)
        {
            API.LogError($"Error registering modifications: {ex.Message}");
        }
    }

    private void UnregisterRandomizerButtonAndShortcut()
    {
        base.Unload();
        _randomizerButton = null;

        if (_randomizerResetShortcutId != null)
        {
            ShortcutManager.UnregisterShortcut(_randomizerResetShortcutId.Value);
            _randomizerResetShortcutId = null;
        }
    }

    private void SetRandomizerEnabled(bool enabled)
    {
        if (_config.RandomizerEnabled == enabled) return;
        _config.RandomizerEnabled = enabled;

        if (!_isInitialized) return;

        foreach (var feature in randomizableFeatures)
            feature.SetRandomizerEnabled(enabled);

        _isRandomizerEnabled = enabled && randomizableFeatures.Any(f => f.IsRandomizedEnabled);

        if (enabled)
            RegisterRandomizerButtonAndShortcut();
        else
            UnregisterRandomizerButtonAndShortcut();
    }

    private void RandomizeFeatures()
    {
        if (!_config.RandomizerEnabled) return;
        foreach (var feature in randomizableFeatures) feature.Randomize();
    }

    private void UpdateIsRandomizerEnabled()
    {
        _isRandomizerEnabled = _config.RandomizerEnabled && randomizableFeatures.Any(f => f.IsRandomizedEnabled);

        if (_randomizerButton == null) return;

        try
        {
            var menuData = _randomizerButton.GetBuiltElement();
            menuData.IsEnabled = _isRandomizerEnabled;
        }
        catch (Exception ex)
        {
            API.LogWarning($"Couldn't update the randomizer button enabled state: {ex.Message}");
        }
    }

    public override void Unload()
    {
        if (!_isInitialized) return;
        _isInitialized = false;

        UnregisterRandomizerButtonAndShortcut();

        foreach (var feature in randomizableFeatures)
        {
            feature.OnEnabledStateChanged -= UpdateIsRandomizerEnabled;
            feature.Unload();
        }
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
        public bool RandomizerEnabled { get; set; } = true;
        public uint Button { get; set; } = (uint)via.hid.GamePadButton.None;
        public uint Key { get; set; } = (uint)via.hid.KeyboardKey.None;
        public bool IsFunc { get; set; }
        public bool IsActive { get; set; }
    }
}