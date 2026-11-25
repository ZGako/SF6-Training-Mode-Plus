using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

namespace SF6_TMP;

/// <summary>
///     Manages the top-level "Training Mode Plus" pause menu settings tab, per-module enable toggles,
///     and module-contributed settings rows.
/// </summary>
internal static partial class SettingsTabManager
{
    private const string EnabledOptionLabel = "<COLOR preset=\"FGM_Accent\">Enabled</COLOR>";

    private static readonly Guid SettingsTabGuid = Guid.NewGuid();

    private static readonly Dictionary<string, (Guid Spinner, Guid Disabled, Guid Enabled)> ModuleToggleGuids =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, List<UICustomElementRequestNode>> ModuleDependentNodes =
        new(StringComparer.OrdinalIgnoreCase);

    private static Config _config = new();
    private static bool _isConfigBound;
    private static PauseMenuAppendElementsRequest? _tabAppendRequest;

    public static void EnsureConfigBound()
    {
        if (_isConfigBound) return;

        ModConfig.Instance.BindSection(
            "Settings",
            () => _config,
            cfg => _config = cfg,
            SettingsTabConfigJsonContext.Default.Config);
        _isConfigBound = true;
    }

    public static bool IsModuleEnabled(ITrainingModePlusModule module)
    {
        return !_config.ModuleEnabled.TryGetValue(module.ConfigKey, out var enabled) || enabled;
    }

    private static void SetModuleEnabled(ITrainingModePlusModule module, bool enabled)
    {
        _config.ModuleEnabled[module.ConfigKey] = enabled;
    }

    private static (Guid Spinner, Guid Disabled, Guid Enabled) GetOrCreateToggleGuids(string configKey)
    {
        if (!ModuleToggleGuids.TryGetValue(configKey, out var guids))
        {
            guids = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            ModuleToggleGuids[configKey] = guids;
        }

        return guids;
    }

    public static void Init(System.Collections.Generic.IReadOnlyList<ITrainingModePlusModule> modules)
    {
        if (_tabAppendRequest != null) Unload();

        var tabNode = new UICustomElementRequestNode(
            SettingsTabGuid,
            new RowButtonInitializer(
                new CustomMessage("Training Mode Plus"),
                new CustomMessage("Configure Training Mode Plus modules and feature settings.")));

        var children = new List<UICustomElementRequestNode>();

        // 1. Place module enable/disable toggles at the top of the settings tab
        foreach (var module in modules)
        {
            var capturedModule = module;
            var (spinnerGuid, disabledGuid, enabledGuid) = GetOrCreateToggleGuids(capturedModule.ConfigKey);

            var spinner = new UICustomElementRequestNode(
                spinnerGuid,
                new SpinnerInitializer(
                    new CustomMessage(capturedModule.DisplayName),
                    new CustomMessage(capturedModule.Description)));

            spinner.AddHook(new SpinBoxHookRequest(_ =>
                IsModuleEnabled(capturedModule) ? enabledGuid : disabledGuid));

            var disableItem = new UICustomElementRequestNode(
                disabledGuid,
                new SpinnerItemTextInitializer(new CustomMessage("Disabled")));
            disableItem.AddHook(new FunctionHookRequest((baseParam, _, _) =>
            {
                HandleModuleToggle(capturedModule, false, baseParam);
                return false;
            }));

            var enableItem = new UICustomElementRequestNode(
                enabledGuid,
                new SpinnerItemTextInitializer(new CustomMessage(EnabledOptionLabel)));
            enableItem.AddHook(new FunctionHookRequest((baseParam, _, _) =>
            {
                HandleModuleToggle(capturedModule, true, baseParam);
                return false;
            }));

            spinner.AddChildren([disableItem, enableItem]);
            children.Add(spinner);
        }

        // 2. Append module-specific settings rows after the module toggles
        ModuleDependentNodes.Clear();
        foreach (var module in modules)
        {
            var nodes = module.CreateSettingsUINodes(IsModuleEnabled(module));
            var dependentList = new List<UICustomElementRequestNode>(nodes.Count);
            dependentList.AddRange(nodes);
            ModuleDependentNodes[module.ConfigKey] = dependentList;
            children.AddRange(dependentList);
        }

        tabNode.AddChildren([.. children]);

        var appendReq = new PauseMenuAppendElementsRequest([(-1, tabNode)]);
        try
        {
            PauseMenuManager.RegisterModification([], appendReq);
            _tabAppendRequest = appendReq;
        }
        catch (Exception ex)
        {
            API.LogError($"[SettingsTabManager] Failed to register settings tab: {ex}");
        }
    }

    private static void HandleModuleToggle(ITrainingModePlusModule module, bool enabled, app.training.BaseParam baseParam)
    {
        if (IsModuleEnabled(module) == enabled) return;

        SetModuleEnabled(module, enabled);

        try
        {
            if (enabled)
                module.Init();
            else
                module.Unload();
        }
        catch (Exception ex)
        {
            API.LogError(
                $"[SettingsTabManager] Exception toggling module '{module.ConfigKey}' to {enabled}: {ex}");
        }

        if (ModuleDependentNodes.TryGetValue(module.ConfigKey, out var dependentNodes) && dependentNodes.Count > 0)
        {
            var dependentFuncTypes = new HashSet<app.training.TrainingFuncType>();

            foreach (var node in dependentNodes)
                try
                {
                    var builtElement = node.GetBuiltElement();
                    builtElement.IsEnabled = enabled;
                    dependentFuncTypes.Add(builtElement.FuncType);
                }
                catch (Exception ex)
                {
                    API.LogWarning($"[SettingsTabManager] Failed updating dependent settings node state: {ex.Message}");
                }

            var uiFlowParam = ManagedProxy<app.training.UIFlowTrainingMenu.Param>.Create(baseParam);
            var viewDataList = uiFlowParam?.ViewDataList;
            if (viewDataList != null)
                for (var i = 0; i < viewDataList.Count; i++)
                {
                    var vd = viewDataList[i];
                    if (vd?.Data != null && dependentFuncTypes.Contains(vd.Data.FuncType))
                        vd.IsDisable = !enabled;
                }
        }

        ModConfig.Save();
    }

    public static void Unload()
    {
        if (_tabAppendRequest != null)
        {
            try
            {
                PauseMenuManager.UnregisterModification(_tabAppendRequest);
            }
            catch (Exception ex)
            {
                API.LogError($"[SettingsTabManager] Failed to unregister settings tab: {ex.Message}");
            }

            _tabAppendRequest = null;
        }

        ModuleDependentNodes.Clear();
    }

    public static void Clear()
    {
        Unload();
        _isConfigBound = false;
        _config = new Config();
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Config))]
    private partial class SettingsTabConfigJsonContext : JsonSerializerContext;

    private sealed class Config
    {
        public Dictionary<string, bool> ModuleEnabled { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
