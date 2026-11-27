using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

namespace SF6_TMP.Modules.HitboxViewer;

/// <summary>
///     Training Mode module hosting a <see cref="HitboxViewer" /> instance, adding Hitbox Display and Preset spinners
///     to the Screen Display Settings tab, and registering a Training Mode toggle shortcut.
/// </summary>
public sealed partial class HitboxViewerModule : ITrainingModePlusModule, IDisposable
{
    private static readonly List<int> PathToScreenDisplayTab = [6];

    // Vanilla message GUIDs for Off / P1 / P2 / P1&P2
    private static readonly Guid OffMessageGuid = new("5f2d2cf0-6a3a-42f9-9503-86e6a036951e");
    private static readonly Guid P1MessageGuid = new("ebffd0e7-a6bd-40d7-a54b-55f5fd4622ba");
    private static readonly Guid P2MessageGuid = new("71abe1f1-f0a0-4e84-9f75-2c464e7269d5");
    private static readonly Guid P1P2MessageGuid = new("646421cc-d6bf-46fd-afa9-cf8cf8c375e4");

    // Custom function GUIDs for Display spinner & items
    private static readonly Guid HITBOX_DISPLAY_SPINNER = Guid.NewGuid();
    private static readonly Guid HITBOX_DISPLAY_OFF = Guid.NewGuid();
    private static readonly Guid HITBOX_DISPLAY_P1 = Guid.NewGuid();
    private static readonly Guid HITBOX_DISPLAY_P2 = Guid.NewGuid();
    private static readonly Guid HITBOX_DISPLAY_P1P2 = Guid.NewGuid();

    // Custom function GUIDs for Preset spinner & items
    private static readonly Guid HITBOX_PRESET_SPINNER = Guid.NewGuid();
    private static readonly Guid HITBOX_PRESET_DEFAULT = Guid.NewGuid();
    private static readonly Guid HITBOX_PRESET_OUTLINE = Guid.NewGuid();
    private static readonly Guid HITBOX_PRESET_CUSTOM = Guid.NewGuid();

    private static Config _config = new();
    private readonly List<IUIModificationRequest> _modificationRequests = [];

    private readonly HitboxViewer _viewer;
    private bool _isInitialized;
    private Guid? _toggleShortcutId;

    private HitboxViewerModule()
    {
        _viewer = new HitboxViewer();
        _viewer.AllowPauseType(app.PauseManager.PauseType.BATTLE_TRAINING_PAUSE);

        HitboxViewer.ConfigSection.BindSection(
            "TrainingMode",
            () => _config,
            cfg => _config = cfg,
            HitboxViewerModuleJsonContext.Default.Config);
    }

    public static HitboxViewerModule Instance { get; } = new();

    public void Dispose()
    {
        Unload();
        _viewer.Dispose();
    }

    public string ConfigKey => nameof(HitboxViewerModule);
    public string DisplayName => "Hitbox Viewer";
    public string Description => "Enable or disable the hitbox viewer overlay and its Screen Display settings.";

    public void Init()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _viewer.IsActive = true;
        _viewer.OnConfigChanged += HandleViewerConfigChanged;

        RegisterPauseMenuItems();
        RegisterToggleShortcut();
    }

    public void Unload()
    {
        if (!_isInitialized) return;
        _isInitialized = false;

        _viewer.OnConfigChanged -= HandleViewerConfigChanged;
        _viewer.IsActive = false;
        _viewer.ResetRuntimeState();

        foreach (var req in _modificationRequests)
            PauseMenuManager.UnregisterModification(req);
        _modificationRequests.Clear();

        if (_toggleShortcutId != null)
        {
            ShortcutManager.UnregisterShortcut(_toggleShortcutId.Value);
            _toggleShortcutId = null;
        }
    }

    private void HandleViewerConfigChanged()
    {
        if (_viewer.Config.DisplayTarget != HitboxDisplayTarget.Off)
            _config.LastNonOffTarget = _viewer.Config.DisplayTarget;
    }

    private void SetDisplayTarget(HitboxDisplayTarget target)
    {
        _viewer.Config.DisplayTarget = target;
        if (target != HitboxDisplayTarget.Off)
            _config.LastNonOffTarget = target;

        ModConfig.Save();
    }

    private void RegisterPauseMenuItems()
    {
        var displaySpinner = new UICustomElementRequestNode(
            HITBOX_DISPLAY_SPINNER,
            new SpinnerInitializer(
                new CustomMessage("Hitbox Display"),
                new CustomMessage("Choose which players have collision boxes displayed.")));

        displaySpinner.AddHook(new SpinBoxHookRequest(_ => _viewer.Config.DisplayTarget switch
        {
            HitboxDisplayTarget.P1 => HITBOX_DISPLAY_P1,
            HitboxDisplayTarget.P2 => HITBOX_DISPLAY_P2,
            HitboxDisplayTarget.P1AndP2 => HITBOX_DISPLAY_P1P2,
            _ => HITBOX_DISPLAY_OFF
        }));

        var offItem = CreateDisplayTargetItem(
            HITBOX_DISPLAY_OFF,
            new CustomMessage(OffMessageGuid),
            HitboxDisplayTarget.Off);
        var p1Item = CreateDisplayTargetItem(
            HITBOX_DISPLAY_P1,
            CreateAccentGameMessage(P1MessageGuid, "P1"),
            HitboxDisplayTarget.P1);
        var p2Item = CreateDisplayTargetItem(
            HITBOX_DISPLAY_P2,
            CreateAccentGameMessage(P2MessageGuid, "P2"),
            HitboxDisplayTarget.P2);
        var p1P2Item = CreateDisplayTargetItem(
            HITBOX_DISPLAY_P1P2,
            CreateAccentGameMessage(P1P2MessageGuid, "P1/P2"),
            HitboxDisplayTarget.P1AndP2);

        displaySpinner.AddChildren([offItem, p1Item, p2Item, p1P2Item]);

        var presetSpinner = new UICustomElementRequestNode(
            HITBOX_PRESET_SPINNER,
            new SpinnerInitializer(
                new CustomMessage("Hitbox Preset"),
                new CustomMessage("Select a visual style preset for the hitbox viewer.")));

        presetSpinner.AddHook(new SpinBoxHookRequest(_ => _viewer.Config.DetectPreset() switch
        {
            HitboxPresetKind.Default => HITBOX_PRESET_DEFAULT,
            HitboxPresetKind.Outline => HITBOX_PRESET_OUTLINE,
            _ => HITBOX_PRESET_CUSTOM
        }));

        var defaultPresetItem = CreatePresetItem(HITBOX_PRESET_DEFAULT, "Default", HitboxPresetKind.Default);
        var outlinePresetItem = CreatePresetItem(HITBOX_PRESET_OUTLINE, "Outline", HitboxPresetKind.Outline);
        var customPresetItem = CreatePresetItem(HITBOX_PRESET_CUSTOM, "Custom", HitboxPresetKind.Custom);

        presetSpinner.AddChildren([defaultPresetItem, outlinePresetItem, customPresetItem]);

        var appendReq = new PauseMenuAppendElementsRequest([(7, displaySpinner), (7, presetSpinner)]);

        try
        {
            PauseMenuManager.RegisterModification(PathToScreenDisplayTab, appendReq);
            _modificationRequests.Add(appendReq);
        }
        catch (Exception ex)
        {
            API.LogError($"[HitboxViewerModule] Failed to register Screen Display menu items: {ex.Message}");
        }
    }

    private UICustomElementRequestNode CreateDisplayTargetItem(
        Guid functionId,
        CustomMessage message,
        HitboxDisplayTarget target)
    {
        var node = new UICustomElementRequestNode(functionId, new SpinnerItemTextInitializer(message));
        node.AddHook(new FunctionHookRequest((_, _, _) =>
        {
            SetDisplayTarget(target);
            return false;
        }));
        return node;
    }

    private UICustomElementRequestNode CreatePresetItem(
        Guid functionId,
        string label,
        HitboxPresetKind preset)
    {
        var node = new UICustomElementRequestNode(
            functionId,
            new SpinnerItemTextInitializer(new CustomMessage(label)));

        node.AddHook(new FunctionHookRequest((_, _, _) =>
        {
            if (preset != HitboxPresetKind.Custom)
            {
                _viewer.Config.ApplyPreset(preset);
                ModConfig.Save();
            }

            return false;
        }));

        return node;
    }

    private void RegisterToggleShortcut()
    {
        _toggleShortcutId = ShortcutManager.RegisterShortcut(
            new ShortcutManager.CustomShortcutNode
            {
                ItemMessage = new CustomMessage("Toggle Hitbox Display"),
                GuideMessage = new CustomMessage("Toggle the hitbox viewer overlay on or off."),
                IsKeyInput = true,
                DefaultButton = via.hid.GamePadButton.None,
                DefaultKey = via.hid.KeyboardKey.None,
                IsDefaultFunc = false,
                IsDefaultActive = false,
                Button = (via.hid.GamePadButton)_config.ShortcutButton,
                Key = (via.hid.KeyboardKey)_config.ShortcutKey,
                IsFunc = _config.ShortcutIsFunc,
                IsActive = _config.ShortcutIsActive
            },
            () =>
            {
                var nextTarget = _viewer.Config.DisplayTarget == HitboxDisplayTarget.Off
                    ? _config.LastNonOffTarget == HitboxDisplayTarget.Off
                        ? HitboxDisplayTarget.P1AndP2
                        : _config.LastNonOffTarget
                    : HitboxDisplayTarget.Off;

                SetDisplayTarget(nextTarget);
            },
            null,
            null,
            liveNode =>
            {
                _config.ShortcutButton = (uint)liveNode.Button;
                _config.ShortcutKey = (uint)liveNode.Key;
                _config.ShortcutIsActive = liveNode.IsActive;
                _config.ShortcutIsFunc = liveNode.IsFunc;
            });
    }

    private static CustomMessage CreateAccentGameMessage(Guid gameMessageGuid, string fallbackText)
    {
        var msg = new CustomMessage(fallbackText);
        var guidText = gameMessageGuid.ToString();

        for (var langIdx = 0; langIdx < (int)via.Language.Max; langIdx++)
        {
            var lang = (via.Language)langIdx;
            var localized = fallbackText;

            try
            {
                var engineGuid = _System.Guid.Parse(guidText);
                var gameText = app.helper.hLocalize.getMessage(engineGuid, lang);
                if (!string.IsNullOrEmpty(gameText))
                    localized = gameText;
            }
            catch
            {
                // Keep fallback string when localization service isn't available yet
            }

            if (!localized.StartsWith("<COLOR", StringComparison.OrdinalIgnoreCase))
                localized = $"<COLOR preset=\"FGM_Accent\">{localized}</COLOR>";

            msg.AddTranslation(lang, localized);
        }

        return msg;
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Config))]
    private partial class HitboxViewerModuleJsonContext : JsonSerializerContext;

    private sealed class Config
    {
        public HitboxDisplayTarget LastNonOffTarget { get; set; } = HitboxDisplayTarget.P1AndP2;

        public uint ShortcutButton { get; set; }
        public uint ShortcutKey { get; set; }
        public bool ShortcutIsFunc { get; set; }
        public bool ShortcutIsActive { get; set; }
    }
}