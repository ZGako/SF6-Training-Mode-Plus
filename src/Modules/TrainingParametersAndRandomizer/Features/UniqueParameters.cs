using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Helpers;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

public partial class UniqueParameters : RandomizableFeatureBase<UniqueParameters>
{
    private const int MaxActiveGaugeSlots = 4;
    private const int DefaultActivationProbability = 50;
    private const string UniqueGaugeFuncTypePrefix = "PARAMETER_UNIQUE_GAUGE_TYPE_";
    private const string RandomizerEnabledLabel = "<COLOR preset=\"FGM_Accent\">Enabled</COLOR>";

    private static readonly List<int> PathToUniqueParamMenu = [1, 8];

    // Known game localization message GUIDs used to classify unique gauge options
    private static readonly Guid InfiniteMessageGuid = Guid.Parse("d6184f70-057a-42d7-b925-bd3a42d74970");
    private static readonly Guid StandardMessageGuid = Guid.Parse("471dda2a-0981-4c84-ab5f-8960ade5eae5");
    private static readonly Guid ActivatedMessageGuid = Guid.Parse("220ad173-a227-4561-9fa3-04e0f269f6a4");

    // Pre-allocated per-slot UI element GUIDs (max 4 active unique gauges across P1 and P2)
    private static readonly GaugeUiSlotGuids[] GaugeSlots =
        [.. Enumerable.Range(0, MaxActiveGaugeSlots).Select(_ => new GaugeUiSlotGuids())];

    private static readonly uint MessageIdFieldOffset =
        app.training.TrainingMenuData.REFType.GetField("_MessageID")?.OffsetFromBase
        ?? throw new MissingFieldException("app.training.TrainingMenuData", "_MessageID");

    private static Config _config = new();
    private static bool _isInitialized;
    private static int _pendingP1FighterId = -1;
    private static int _pendingP2FighterId = -1;

    // Tracks active mouse/controller spin input on a stock bound spinner so set_Num and SetSelectedIndex stay in sync
    [ThreadStatic] private static int s_spinInputDepth;
    [ThreadStatic] private static StockSpinContext s_spinContext;

    // Active gauges for the currently loaded P1/P2 characters (index maps to GaugeSlots[0..3])
    private readonly List<ParsedUniqueGaugeInfo> _activeGauges = [];

    // Parsed once per Init() from the unmodified _UIData._MenuData tree + TDB metadata
    private readonly Dictionary<int, List<ParsedUniqueGaugeInfo>> _parsedGaugesByFighter = [];

    // Per-slot UI nodes that should be disabled whenever that slot's randomizer is off
    private readonly List<UICustomElementRequestNode>[] _slotRandomizerDependentNodes =
        [.. Enumerable.Range(0, MaxActiveGaugeSlots).Select(_ => new List<UICustomElementRequestNode>())];

    private int _activeP1FighterId = -1;
    private int _activeP2FighterId = -1;

    private UniqueParameters()
    {
        TrainingParametersAndRandomizer.ConfigSection.BindSection(
            nameof(UniqueParameters),
            () => _config,
            cfg => _config = cfg,
            UniqueParametersConfigJsonContext.Default.Config);
    }


    private static app.training.TrainingMenuData_Array1D GetUniqueParamMenuRows(app.training.TrainingManager tm)
    {
        var currentChildren = tm._UIData._MenuData;
        foreach (var index in PathToUniqueParamMenu)
        {
            if (currentChildren == null || index < 0 || index >= currentChildren.Length)
                throw new InvalidOperationException(
                    $"Could not traverse PathToUniqueParamMenu [{string.Join(", ", PathToUniqueParamMenu)}] at index {index}.");

            currentChildren = currentChildren[index]._ChildData;
        }

        return currentChildren ?? throw new NullReferenceException("Unique parameter menu _ChildData is null.");
    }

    private static Dictionary<int, string> BuildUniqueGaugeFuncTypeNameMap()
    {
        var map = new Dictionary<int, string>();
        var funcTypeDef = API.GetTDB().FindType("app.training.TrainingFuncType");
        if (funcTypeDef == null)
        {
            API.LogError("[UniqueParameters] Could not find app.training.TrainingFuncType in TDB.");
            return map;
        }

        foreach (var field in funcTypeDef.Fields)
        {
            if (!field.IsStatic()) continue;

            var name = field.Name;
            if (!name.StartsWith(UniqueGaugeFuncTypePrefix, StringComparison.Ordinal)) continue;

            try
            {
                var value = Convert.ToInt32(field.GetDataBoxed(0, false));
                map[value] = name;
            }
            catch (Exception ex)
            {
                API.LogWarning($"[UniqueParameters] Failed to read TrainingFuncType.{name}: {ex.Message}");
            }
        }

        return map;
    }

    private static bool TryParseUniqueGaugeFuncTypeName(string name, out int fighterId, out int subIndex)
    {
        fighterId = 0;
        subIndex = 0;

        if (!name.StartsWith(UniqueGaugeFuncTypePrefix, StringComparison.Ordinal)) return false;

        var suffix = name[UniqueGaugeFuncTypePrefix.Length..];
        var parts = suffix.Split('_');
        if (parts.Length is < 1 or > 2) return false;
        if (!int.TryParse(parts[0], out fighterId)) return false;
        if (parts.Length == 2 && !int.TryParse(parts[1], out subIndex)) return false;

        return true;
    }

    private static int? TryGetUniqueTimerMaxValue(TypeDefinition? paramSettingTypeDef, int fighterId)
    {
        var field = paramSettingTypeDef?.GetField($"UNIQUE_TIMER_{fighterId:D3}");
        if (field == null) return null;

        try
        {
            return Convert.ToInt32(field.GetDataBoxed(0, false));
        }
        catch (Exception ex)
        {
            API.LogWarning($"[UniqueParameters] Failed to read UNIQUE_TIMER_{fighterId:D3}: {ex.Message}");
            return null;
        }
    }

    private static Guid ReadMessageGuid(app.training.TrainingMenuData item)
    {
        return item is IObject obj
            ? Marshal.PtrToStructure<Guid>((IntPtr)(obj.GetAddress() + MessageIdFieldOffset))
            : Guid.Empty;
    }

    private static (List<Guid> OptionMessageGuids, bool AllowsInfinite) ParseGaugeChildMessages(
        app.training.TrainingMenuData row)
    {
        var optionGuids = new List<Guid>();
        var allowsInfinite = false;

        var children = row._ChildData;
        if (children == null || children.Length == 0)
            return (optionGuids, allowsInfinite);

        var count = children.Length;
        for (var i = 0; i < count; i++)
        {
            var child = children[i];
            if (child == null) continue;

            var msgGuid = ReadMessageGuid(child);
            if (msgGuid == Guid.Empty) continue;

            if (i == count - 1 && msgGuid == InfiniteMessageGuid)
            {
                allowsInfinite = true;
                continue;
            }

            optionGuids.Add(msgGuid);
        }

        return (optionGuids, allowsInfinite);
    }

    private static bool IsStandardActivatedPowerup(System.Collections.Generic.IReadOnlyList<Guid> optionGuids)
    {
        return optionGuids.Count == 2 &&
               optionGuids[0] == StandardMessageGuid &&
               optionGuids[1] == ActivatedMessageGuid;
    }

    private bool TryEnsureUniqueGaugesParsed(app.training.TrainingManager tm, string source)
    {
        if (_parsedGaugesByFighter.Count > 0) return true;

        try
        {
            return TryParseAllUniqueGauges(tm, source);
        }
        catch (Exception ex)
        {
            API.LogWarning(
                $"[UniqueParameters] ({source}) Failed to parse unique gauges (will retry on ParameterSetting.Initialize): {ex.Message}");
            _parsedGaugesByFighter.Clear();
            return false;
        }
    }

    private bool TryParseAllUniqueGauges(app.training.TrainingManager tm, string source)
    {
        _parsedGaugesByFighter.Clear();

        var paramFunc = tm.ParamFunc;
        var gData = paramFunc?._gData;
        if (paramFunc == null || gData == null || (gData as IObject)?.GetAddress() is null or 0)
        {
            API.LogWarning(
                $"[UniqueParameters] ({source}) ParamFunc/_gData not ready yet; deferring unique gauge parsing.");
            return false;
        }

        var stockDict = gData.UniqueStockDict;
        var timerDict = gData.UniqueTimerDict;
        if (stockDict == null || timerDict == null)
        {
            API.LogWarning(
                $"[UniqueParameters] ({source}) UniqueStockDict/UniqueTimerDict not ready yet; deferring unique gauge parsing.");
            return false;
        }

        var funcTypeNames = BuildUniqueGaugeFuncTypeNameMap();
        var paramSettingTypeDef = API.GetTDB().FindType("app.training.tf_ParameterSetting");
        var rows = GetUniqueParamMenuRows(tm);

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row == null) continue;

            var funcTypeInt = (int)row.FuncType;
            if (!funcTypeNames.TryGetValue(funcTypeInt, out var funcTypeName)) continue;
            if (!TryParseUniqueGaugeFuncTypeName(funcTypeName, out var fighterId, out var subIndex)) continue;

            var hasStock = stockDict.ContainsKey(fighterId);
            var hasTimer = timerDict.ContainsKey(fighterId);

            UniqueGaugeBackingType backingType;
            if (hasStock && !hasTimer)
            {
                backingType = UniqueGaugeBackingType.Stock;
            }
            else if (!hasStock && hasTimer)
            {
                backingType = UniqueGaugeBackingType.Timer;
            }
            else if (hasStock && hasTimer)
            {
                // Dual-gauge characters consistently map _0 -> Stock and _1 -> Timer
                backingType = subIndex == 1 ? UniqueGaugeBackingType.Timer : UniqueGaugeBackingType.Stock;
            }
            else
            {
                API.LogWarning(
                    $"[UniqueParameters] Fighter {fighterId:D3} ({funcTypeName}) not found in UniqueStockDict or UniqueTimerDict; skipping.");
                continue;
            }

            var (optionGuids, allowsInfinite) = ParseGaugeChildMessages(row);

            int? maxTimerValue = null;
            UniqueGaugeCategory category;
            if (backingType == UniqueGaugeBackingType.Timer)
            {
                maxTimerValue = TryGetUniqueTimerMaxValue(paramSettingTypeDef, fighterId);
                category = maxTimerValue.HasValue
                    ? UniqueGaugeCategory.InstallTimer
                    : UniqueGaugeCategory.Powerup;
            }
            else
            {
                category = IsStandardActivatedPowerup(optionGuids)
                    ? UniqueGaugeCategory.Powerup
                    : UniqueGaugeCategory.Stock;
            }

            var info = new ParsedUniqueGaugeInfo
            {
                FighterId = fighterId,
                MenuRowIndex = rowIndex,
                SubIndex = subIndex,
                FuncTypeName = funcTypeName,
                BackingType = backingType,
                Category = category,
                MaxTimerValue = maxTimerValue,
                AllowsInfinite = allowsInfinite,
                OptionMessageGuids = optionGuids
            };

            if (!_parsedGaugesByFighter.TryGetValue(fighterId, out var list))
            {
                list = [];
                _parsedGaugesByFighter[fighterId] = list;
            }

            list.Add(info);
        }

        return _parsedGaugesByFighter.Count > 0;
    }

    private static int GetMaxBoundForGauge(ParsedUniqueGaugeInfo gauge)
    {
        return gauge.Category switch
        {
            UniqueGaugeCategory.InstallTimer => Math.Max(0, gauge.MaxTimerValue ?? 0),
            UniqueGaugeCategory.Stock => Math.Max(0, gauge.OptionMessageGuids.Count - 1),
            UniqueGaugeCategory.Powerup => 1,
            _ => 0
        };
    }

    private static void ClampGaugeConfig(ParsedUniqueGaugeInfo gauge, GaugeConfig cfg)
    {
        cfg.ActivationProbability = Math.Clamp(cfg.ActivationProbability, 0, 100);

        var maxBound = GetMaxBoundForGauge(gauge);
        cfg.InitialTimerValue = Math.Clamp(cfg.InitialTimerValue, 0, maxBound);
        cfg.LowerBound = Math.Clamp(cfg.LowerBound, 0, maxBound);
        cfg.UpperBound = Math.Clamp(cfg.UpperBound, cfg.LowerBound, maxBound);
    }

    private static GaugeConfig GetOrCreateGaugeConfig(ParsedUniqueGaugeInfo gauge)
    {
        _config.Gauges ??= [];

        if (!_config.Gauges.TryGetValue(gauge.FuncTypeName, out var cfg))
        {
            var maxBound = GetMaxBoundForGauge(gauge);
            cfg = new GaugeConfig
            {
                RandomizerEnabled = false,
                InitialTimerValue = gauge.Category == UniqueGaugeCategory.InstallTimer ? maxBound : 0,
                ActivationProbability = DefaultActivationProbability,
                LowerBound = 0,
                UpperBound = maxBound
            };
            _config.Gauges[gauge.FuncTypeName] = cfg;
        }
        else
        {
            ClampGaugeConfig(gauge, cfg);
        }

        return cfg;
    }

    private static bool IsGaugeRandomizerEnabled(ParsedUniqueGaugeInfo gauge)
    {
        return GetOrCreateGaugeConfig(gauge).RandomizerEnabled;
    }

    private void SetGaugeRandomizerEnabled(ParsedUniqueGaugeInfo gauge, bool enabled)
    {
        GetOrCreateGaugeConfig(gauge).RandomizerEnabled = enabled;
        UpdateRandomizedEnabledState();
    }

    private static int GetInitialTimerValue(ParsedUniqueGaugeInfo gauge)
    {
        return GetOrCreateGaugeConfig(gauge).InitialTimerValue;
    }

    private static void SetInitialTimerValue(ParsedUniqueGaugeInfo gauge, int value)
    {
        var cfg = GetOrCreateGaugeConfig(gauge);
        cfg.InitialTimerValue = Math.Clamp(value, 0, GetMaxBoundForGauge(gauge));
    }

    private static int GetActivationProbability(ParsedUniqueGaugeInfo gauge)
    {
        return GetOrCreateGaugeConfig(gauge).ActivationProbability;
    }

    private static void SetActivationProbability(ParsedUniqueGaugeInfo gauge, int value)
    {
        GetOrCreateGaugeConfig(gauge).ActivationProbability = Math.Clamp(value, 0, 100);
    }

    private static int GetLowerBound(ParsedUniqueGaugeInfo gauge)
    {
        return GetOrCreateGaugeConfig(gauge).LowerBound;
    }

    private static void SetLowerBound(ParsedUniqueGaugeInfo gauge, int value)
    {
        var cfg = GetOrCreateGaugeConfig(gauge);
        cfg.LowerBound = Math.Clamp(value, 0, cfg.UpperBound);
    }

    private static int GetUpperBound(ParsedUniqueGaugeInfo gauge)
    {
        return GetOrCreateGaugeConfig(gauge).UpperBound;
    }

    private static void SetUpperBound(ParsedUniqueGaugeInfo gauge, int value)
    {
        var cfg = GetOrCreateGaugeConfig(gauge);
        cfg.UpperBound = Math.Clamp(value, cfg.LowerBound, GetMaxBoundForGauge(gauge));
    }

    private void UpdateRandomizedEnabledState()
    {
        IsRandomizedEnabled = _activeGauges.Any(IsGaugeRandomizerEnabled);
    }

    private void ClearActiveUI()
    {
        base.Unload();
        _activeGauges.Clear();
        foreach (var list in _slotRandomizerDependentNodes)
            list.Clear();
    }

    private void ResolveActiveGauges(app.training.TrainingManager tm, int p1FighterId, int p2FighterId)
    {
        _activeGauges.Clear();

        var paramFunc = tm.ParamFunc;
        ReadOnlySpan<int> activeFighters = p1FighterId == p2FighterId
            ? [p1FighterId]
            : [p1FighterId, p2FighterId];

        foreach (var fighterId in activeFighters)
        {
            if (!paramFunc.IsUniqueData(fighterId)) continue;
            if (!_parsedGaugesByFighter.TryGetValue(fighterId, out var gauges)) continue;

            foreach (var gauge in gauges)
            {
                if (_activeGauges.Count >= MaxActiveGaugeSlots)
                {
                    API.LogWarning(
                        $"[UniqueParameters] Exceeded max active gauge slots ({MaxActiveGaugeSlots}); skipping {gauge.FuncTypeName}.");
                    break;
                }

                _activeGauges.Add(gauge);
            }
        }
    }

    private void HandleRandomizerToggle(int slot, bool enabled, app.training.BaseParam baseParam)
    {
        if (slot < 0 || slot >= _activeGauges.Count) return;

        var gauge = _activeGauges[slot];
        SetGaugeRandomizerEnabled(gauge, enabled);

        var dependentNodes = _slotRandomizerDependentNodes[slot];
        var dependentFuncTypes = new HashSet<app.training.TrainingFuncType>();

        foreach (var node in dependentNodes)
        {
            var builtElement = node.GetBuiltElement();
            builtElement.IsEnabled = enabled;
            dependentFuncTypes.Add(builtElement.FuncType);
        }

        var uiFlowParam = ManagedProxy<app.training.UIFlowTrainingMenu.Param>.Create(baseParam);
        if (uiFlowParam == null) return;

        var viewDataList = uiFlowParam.ViewDataList;
        if (viewDataList != null)
            for (var i = 0; i < viewDataList.Count; i++)
            {
                var vd = viewDataList[i];
                if (vd?.Data != null && dependentFuncTypes.Contains(vd.Data.FuncType))
                    vd.IsDisable = !enabled;
            }
    }

    private UICustomElementRequestNode CreateRandomizerToggleSpinner(
        int slot, ParsedUniqueGaugeInfo gauge, GaugeUiSlotGuids slotGuids)
    {
        var spinner = new UICustomElementRequestNode(slotGuids.RandomizerEnableSpinner,
            new SpinnerInitializer(
                new CustomMessage("Randomize Gauge"),
                new CustomMessage("Enable or disable randomization for this unique gauge.")));

        spinner.AddHook(new SpinBoxHookRequest(_ =>
            IsGaugeRandomizerEnabled(gauge)
                ? slotGuids.RandomizerEnableOn
                : slotGuids.RandomizerEnableOff));

        var disableItem = new UICustomElementRequestNode(slotGuids.RandomizerEnableOff,
            new SpinnerItemTextInitializer(new CustomMessage("Disabled")));
        disableItem.AddHook(new FunctionHookRequest((baseParam, _, _) =>
        {
            HandleRandomizerToggle(slot, false, baseParam);
            return false;
        }));

        var enableItem = new UICustomElementRequestNode(slotGuids.RandomizerEnableOn,
            new SpinnerItemTextInitializer(new CustomMessage(RandomizerEnabledLabel)));
        enableItem.AddHook(new FunctionHookRequest((baseParam, _, _) =>
        {
            HandleRandomizerToggle(slot, true, baseParam);
            return false;
        }));

        spinner.AddChildren([disableItem, enableItem]);
        return spinner;
    }

    private static List<UICustomElementRequestNode> CreateStockOptionItems(
        ParsedUniqueGaugeInfo gauge, GaugeUiSlotGuids slotGuids, bool isUpperBound)
    {
        var optionMessageGuids = gauge.OptionMessageGuids;
        var items = new List<UICustomElementRequestNode>(optionMessageGuids.Count);
        for (var i = 0; i < optionMessageGuids.Count; i++)
        {
            var optionIndex = i;
            var itemGuid = isUpperBound
                ? slotGuids.GetUpperBoundOptionGuid(optionIndex)
                : slotGuids.GetLowerBoundOptionGuid(optionIndex);

            var item = new UICustomElementRequestNode(
                itemGuid,
                new SpinnerItemTextInitializer(new CustomMessage(optionMessageGuids[optionIndex])));

            item.AddHook(new FunctionHookRequest((_, _, _) =>
            {
                if (isUpperBound)
                    SetUpperBound(gauge, optionIndex);
                else
                    SetLowerBound(gauge, optionIndex);

                // API.LogInfo(
                //     $"[UniqueParameters][StockSpin] FunctionHook ({gauge.FuncTypeName}, {(isUpperBound ? "Upper" : "Lower")}) -> optionIndex={optionIndex}, cfg=[{GetLowerBound(gauge)}..{GetUpperBound(gauge)}]");
                return false;
            }));

            items.Add(item);
        }

        return items;
    }

    private List<UICustomElementRequestNode> CreateNodesForGauge(
        int slot, ParsedUniqueGaugeInfo gauge, GaugeUiSlotGuids slotGuids)
    {
        var isRandomizerEnabled = IsGaugeRandomizerEnabled(gauge);
        var dependentNodes = _slotRandomizerDependentNodes[slot];
        dependentNodes.Clear();

        switch (gauge.Category)
        {
            case UniqueGaugeCategory.InstallTimer:
            {
                var maxTimer = gauge.MaxTimerValue ?? 0;

                var initialValueSlider = new UICustomElementRequestNode(slotGuids.TimerInitialValueSlider,
                    new VitalSliderInitializer(
                        new CustomMessage("Initial Timer Value"),
                        new CustomMessage("Set the starting timer value when this install is activated."),
                        (0, maxTimer)));

                var randomizerToggle = CreateRandomizerToggleSpinner(slot, gauge, slotGuids);

                var probabilitySlider = new UICustomElementRequestNode(slotGuids.ProbabilitySlider,
                    new SliderInitializer(
                        new CustomMessage("Activation Probability"),
                        new CustomMessage(
                            "Set the probability (%) of starting with this install activated when randomized."),
                        (0, 100),
                        1,
                        10,
                        isRandomizerEnabled));

                var upperBoundSlider = new UICustomElementRequestNode(slotGuids.UpperBoundElement,
                    new VitalSliderInitializer(
                        new CustomMessage("Randomizer Upper Bound"),
                        new CustomMessage("Set the upper bound for timer randomization."),
                        (0, maxTimer),
                        isRandomizerEnabled));

                var lowerBoundSlider = new UICustomElementRequestNode(slotGuids.LowerBoundElement,
                    new VitalSliderInitializer(
                        new CustomMessage("Randomizer Lower Bound"),
                        new CustomMessage("Set the lower bound for timer randomization."),
                        (0, maxTimer),
                        isRandomizerEnabled));

                dependentNodes.AddRange([probabilitySlider, upperBoundSlider, lowerBoundSlider]);
                return [initialValueSlider, randomizerToggle, probabilitySlider, upperBoundSlider, lowerBoundSlider];
            }

            case UniqueGaugeCategory.Powerup:
            {
                var randomizerToggle = CreateRandomizerToggleSpinner(slot, gauge, slotGuids);

                var probabilitySlider = new UICustomElementRequestNode(slotGuids.ProbabilitySlider,
                    new SliderInitializer(
                        new CustomMessage("Activation Probability"),
                        new CustomMessage(
                            "Set the probability (%) of starting with this powerup activated when randomized."),
                        (0, 100),
                        1,
                        10,
                        isRandomizerEnabled));

                dependentNodes.Add(probabilitySlider);
                return [randomizerToggle, probabilitySlider];
            }

            case UniqueGaugeCategory.Stock:
            {
                var randomizerToggle = CreateRandomizerToggleSpinner(slot, gauge, slotGuids);

                var upperBoundSpinner = new UICustomElementRequestNode(slotGuids.UpperBoundElement,
                    new SpinnerInitializer(
                        new CustomMessage("Randomizer Upper Bound"),
                        new CustomMessage("Set the upper bound for stock randomization."),
                        isRandomizerEnabled));
                upperBoundSpinner.AddHook(new SpinBoxHookRequest(_ =>
                {
                    var val = GetUpperBound(gauge);
                    // API.LogInfo(
                    //     $"[UniqueParameters][StockSpin] InitSpinBox ({gauge.FuncTypeName}, Upper) -> val={val}");
                    return slotGuids.GetUpperBoundOptionGuid(val);
                }));
                upperBoundSpinner.AddChildren([.. CreateStockOptionItems(gauge, slotGuids, true)]);

                var lowerBoundSpinner = new UICustomElementRequestNode(slotGuids.LowerBoundElement,
                    new SpinnerInitializer(
                        new CustomMessage("Randomizer Lower Bound"),
                        new CustomMessage("Set the lower bound for stock randomization."),
                        isRandomizerEnabled));
                lowerBoundSpinner.AddHook(new SpinBoxHookRequest(_ =>
                {
                    var val = GetLowerBound(gauge);
                    // API.LogInfo(
                    //     $"[UniqueParameters][StockSpin] InitSpinBox ({gauge.FuncTypeName}, Lower) -> val={val}");
                    return slotGuids.GetLowerBoundOptionGuid(val);
                }));
                lowerBoundSpinner.AddChildren([.. CreateStockOptionItems(gauge, slotGuids, false)]);

                dependentNodes.AddRange([upperBoundSpinner, lowerBoundSpinner]);
                return [randomizerToggle, upperBoundSpinner, lowerBoundSpinner];
            }

            default:
                return [];
        }
    }

    private void BuildAndRegisterActiveUI()
    {
        if (_activeGauges.Count == 0) return;

        var activeRowIndices = new HashSet<int>(_activeGauges.Select(g => g.MenuRowIndex));
        var indicesToRemove = _parsedGaugesByFighter.Values
            .SelectMany(gauges => gauges)
            .Where(g => !activeRowIndices.Contains(g.MenuRowIndex))
            .Select(g => g.MenuRowIndex)
            .ToList();

        var elementsToAppend = new List<(int, UICustomElementRequestNode)>();

        for (var slot = 0; slot < _activeGauges.Count; slot++)
        {
            var gauge = _activeGauges[slot];
            var slotGuids = GaugeSlots[slot];

            var nodes = CreateNodesForGauge(slot, gauge, slotGuids);
            foreach (var node in nodes)
                elementsToAppend.Add((gauge.AppendIndex, node));
        }

        try
        {
            if (indicesToRemove.Count > 0)
            {
                var removeReq = new RemoveElementsRequest(indicesToRemove);
                PauseMenuManager.RegisterModification(PathToUniqueParamMenu, removeReq);
                ModificationRequests.Add(removeReq);
            }

            if (elementsToAppend.Count > 0)
            {
                var appendReq = new PauseMenuAppendElementsRequest(elementsToAppend);
                PauseMenuManager.RegisterModification(PathToUniqueParamMenu, appendReq);
                ModificationRequests.Add(appendReq);
            }
        }
        catch (Exception ex)
        {
            API.LogError($"[UniqueParameters] Failed to register UI modifications: {ex}");
        }
    }

    private void RefreshActiveCharacterUI(app.training.TrainingManager tm)
    {
        var selectMenu = tm._tData.SelectMenu;
        var p1FighterId = selectMenu.PlayerDatas[0].FighterID;
        var p2FighterId = selectMenu.PlayerDatas[1].FighterID;

        // On first load, SelectMenu.PlayerDatas FighterIDs are still -1; fall back to the IDs captured from SetFighter
        if (p1FighterId < 0) p1FighterId = _pendingP1FighterId;
        if (p2FighterId < 0) p2FighterId = _pendingP2FighterId;

        if (p1FighterId < 0 || p2FighterId < 0) return;
        if (p1FighterId == _activeP1FighterId && p2FighterId == _activeP2FighterId) return;

        _activeP1FighterId = p1FighterId;
        _activeP2FighterId = p2FighterId;

        ClearActiveUI();
        ResolveActiveGauges(tm, p1FighterId, p2FighterId);
        BuildAndRegisterActiveUI();
        UpdateRandomizedEnabledState();
    }

    public override void Init()
    {
        var tm = GameSingletonManager.TrainingManager!;

        try
        {
            _activeP1FighterId = -1;
            _activeP2FighterId = -1;
            _isInitialized = true;

            if (TryEnsureUniqueGaugesParsed(tm, "Init"))
                RefreshActiveCharacterUI(tm);
        }
        catch (Exception ex)
        {
            API.LogError($"[UniqueParameters] Failed to initialize unique parameters: {ex}");
        }
    }

    public override void Unload()
    {
        _isInitialized = false;
        _activeP1FighterId = -1;
        _activeP2FighterId = -1;
        _pendingP1FighterId = -1;
        _pendingP2FighterId = -1;
        IsRandomizedEnabled = false;

        ClearActiveUI();
        _parsedGaugesByFighter.Clear();
    }

    public override void Randomize()
    {
        if (!IsRandomizedEnabled || _activeGauges.Count == 0) return;

        var paramFunc = GameSingletonManager.TrainingManager?.ParamFunc;
        if (paramFunc == null) return;

        foreach (var gauge in _activeGauges)
        {
            var cfg = GetOrCreateGaugeConfig(gauge);
            if (!cfg.RandomizerEnabled) continue;

            switch (gauge.Category)
            {
                case UniqueGaugeCategory.Stock:
                {
                    var randomStock = Random.Shared.Next(cfg.LowerBound, cfg.UpperBound + 1);
                    paramFunc.SetUnique((app.CHARA_ID)gauge.FighterId, (app.training.UNIQUE_STOCK_TYPE)randomStock);
                    break;
                }
            }
        }
    }

    [MethodHook(typeof(app.training.TrainingManager),
        "SetFighter(app.battle.FighterDesc, app.battle.FighterDesc)", MethodHookType.Pre)]
    public static PreHookResult OnSetFighterPre(Span<ulong> args)
    {
        try
        {
            if (args.Length >= 4 && args[2] != 0 && args[3] != 0)
            {
                var p1Desc = MemoryHelpers.GetAddressAs<app.battle.FighterDesc>(args[2]);
                var p2Desc = MemoryHelpers.GetAddressAs<app.battle.FighterDesc>(args[3]);
                _pendingP1FighterId = p1Desc.FighterId;
                _pendingP2FighterId = p2Desc.FighterId;
            }
        }
        catch (Exception ex)
        {
            API.LogWarning($"[UniqueParameters] Failed to read FighterDesc in SetFighter: {ex.Message}");
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.tf_ParameterSetting), "Initialize", MethodHookType.Post)]
    public static void OnParamSettingInitializePost(ref ulong retval)
    {
        if (!_isInitialized) return;

        var tm = GameSingletonManager.TrainingManager;
        if (tm == null || !tm.IsInit) return;

        try
        {
            if (Instance.TryEnsureUniqueGaugesParsed(tm, "ParamSettingInitialize"))
                Instance.RefreshActiveCharacterUI(tm);
        }
        catch (Exception ex)
        {
            API.LogError($"[UniqueParameters] Failed to refresh unique parameters on Initialize: {ex}");
        }
    }

    private bool TryGetStockBoundRange(Guid funcId, out int minBound, out int maxBound)
    {
        for (var slot = 0; slot < _activeGauges.Count; slot++)
        {
            var gauge = _activeGauges[slot];
            if (gauge.Category != UniqueGaugeCategory.Stock) continue;

            var slotGuids = GaugeSlots[slot];
            if (funcId == slotGuids.UpperBoundElement || slotGuids.IsUpperBoundOptionGuid(funcId))
            {
                minBound = GetLowerBound(gauge);
                maxBound = GetMaxBoundForGauge(gauge);
                return true;
            }

            if (funcId == slotGuids.LowerBoundElement || slotGuids.IsLowerBoundOptionGuid(funcId))
            {
                minBound = 0;
                maxBound = GetUpperBound(gauge);
                return true;
            }
        }

        minBound = 0;
        maxBound = 0;
        return false;
    }

    private static void TryBeginStockSpinContext(ulong spinAddress, int direction, bool allowWrap, string source)
    {
        if (!_isInitialized || spinAddress == 0) return;

        try
        {
            var tm = GameSingletonManager.TrainingManager;
            if (tm == null || !tm.IsInit || tm._ActiveMenuType != app.training.TrainingManager.MenuType.ALL)
                return;

            var menuAllObj = tm._UITrainingMenu_All as IObject;
            if (menuAllObj == null)
                // API.LogWarning($"[UniqueParameters][StockSpin] {source}: _UITrainingMenu_All is null.");
                return;

            var paramData = (menuAllObj.GetField("_ParamData") as ManagedObject)
                ?.As<app.training.UIFlowTrainingMenu.Param>();
            if (paramData == null)
                // API.LogWarning($"[UniqueParameters][StockSpin] {source}: _ParamData is null.");
                return;

            var currentData = paramData.CurrentData;
            if (currentData == null)
            {
                var secondaryIndex = paramData.SecondaryListIndex;
                if (secondaryIndex < 0 || secondaryIndex >= paramData.ViewDataList.Count)
                    // API.LogWarning(
                    //     $"[UniqueParameters][StockSpin] {source}: CurrentData null and SecondaryListIndex={secondaryIndex} out of range (count={paramData.ViewDataList.Count}).");
                    return;

                currentData = paramData.ViewDataList[secondaryIndex]?.Data;
            }

            if (currentData == null)
                // API.LogWarning($"[UniqueParameters][StockSpin] {source}: resolved currentData is null.");
                return;

            var funcTypeInt = (int)currentData.FuncType;
            if (!UIHelpers.TryGetFunctionId(funcTypeInt, out var funcId))
                // API.LogInfo(
                //     $"[UniqueParameters][StockSpin] {source}: funcType={funcTypeInt} (secIdx={paramData.SecondaryListIndex}) has no custom GUID.");
                return;

            if (!Instance.TryGetStockBoundRange(funcId, out var minBound, out var maxBound))
                // API.LogInfo(
                //     $"[UniqueParameters][StockSpin] {source}: funcId={funcId} (funcType={funcTypeInt}) is not a stock bound spinner.");
                return;

            var spin = MemoryHelpers.GetAddressAs<app.UIPartsSpin>(spinAddress);
            var child0 = spin?.GetChild(0);
            var scrollListAddress = (child0 as IObject)?.GetAddress() ?? 0;

            s_spinContext = new StockSpinContext(
                true,
                spinAddress,
                scrollListAddress,
                direction,
                allowWrap,
                minBound,
                maxBound);

            // API.LogInfo(
            //     $"[UniqueParameters][StockSpin] {source}: CONTEXT STARTED -> funcId={funcId}, range=[{minBound}..{maxBound}], dir={direction}, allowWrap={allowWrap}, spin=0x{spinAddress:X}, curNum={spin?.Num}, child0=0x{scrollListAddress:X} ({(child0 as IObject)?.GetTypeDefinition()?.FullName ?? "null"})");
        }
        catch (Exception ex)
        {
            API.LogWarning($"[UniqueParameters] Failed to begin stock spin context ({source}): {ex}");
        }
    }

    private static int AdjustStockBoundValue(int incoming, StockSpinContext ctx)
    {
        if (incoming >= ctx.MinBound && incoming <= ctx.MaxBound)
            return incoming;

        if (ctx.Direction > 0)
            return ctx.AllowWrap ? ctx.MinBound : ctx.MaxBound;

        return ctx.AllowWrap ? ctx.MaxBound : ctx.MinBound;
    }

    [MethodHook(typeof(app.UIPartsSpin), "Add()", MethodHookType.Pre)]
    public static PreHookResult OnSpinAddPre(Span<ulong> args)
    {
        if (s_spinInputDepth == 0 && args.Length >= 2)
            TryBeginStockSpinContext(args[1], 1, true, "Add");
        // else if (s_spinContext.Active)
        //     API.LogInfo(
        //         $"[UniqueParameters][StockSpin] Nested Add Pre: depth={s_spinInputDepth}, spin=0x{(args.Length >= 2 ? args[1] : 0):X}");

        s_spinInputDepth++;
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.UIPartsSpin), "Add()", MethodHookType.Post)]
    public static void OnSpinAddPost(ref ulong retval)
    {
        if (s_spinInputDepth > 0 && --s_spinInputDepth == 0)
            // if (s_spinContext.Active)
            //     API.LogInfo("[UniqueParameters][StockSpin] Add Post: clearing context.");
            s_spinContext = default;
    }

    [MethodHook(typeof(app.UIPartsSpin), "Sub()", MethodHookType.Pre)]
    public static PreHookResult OnSpinSubPre(Span<ulong> args)
    {
        if (s_spinInputDepth == 0 && args.Length >= 2)
            TryBeginStockSpinContext(args[1], -1, true, "Sub");
        // else if (s_spinContext.Active)
        //     API.LogInfo(
        //         $"[UniqueParameters][StockSpin] Nested Sub Pre: depth={s_spinInputDepth}, spin=0x{(args.Length >= 2 ? args[1] : 0):X}");

        s_spinInputDepth++;
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.UIPartsSpin), "Sub()", MethodHookType.Post)]
    public static void OnSpinSubPost(ref ulong retval)
    {
        if (s_spinInputDepth > 0 && --s_spinInputDepth == 0)
            // if (s_spinContext.Active)
            //     API.LogInfo("[UniqueParameters][StockSpin] Sub Post: clearing context.");
            s_spinContext = default;
    }

    [MethodHook(typeof(app.UIPartsSpin),
        "InputDirection(app.UIPartsItemDirection, app.InputDigitalFlag)", MethodHookType.Pre)]
    public static PreHookResult OnSpinInputDirectionPre(Span<ulong> args)
    {
        if (s_spinInputDepth == 0 && args.Length >= 4)
        {
            var direction = (app.UIPartsItemDirection)(int)args[2];
            var flags = (app.InputDigitalFlag)(int)args[3];

            var dirSign = direction switch
            {
                app.UIPartsItemDirection.Left => -1,
                app.UIPartsItemDirection.Right => 1,
                _ => 0
            };

            if (dirSign != 0 && (flags & (app.InputDigitalFlag.Trigger | app.InputDigitalFlag.Repeat)) != 0)
            {
                var allowWrap = (flags & app.InputDigitalFlag.Trigger) != 0;
                TryBeginStockSpinContext(args[1], dirSign, allowWrap, $"InputDirection({direction},{flags})");
            }
        }

        s_spinInputDepth++;
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.UIPartsSpin),
        "InputDirection(app.UIPartsItemDirection, app.InputDigitalFlag)", MethodHookType.Post)]
    public static void OnSpinInputDirectionPost(ref ulong retval)
    {
        if (s_spinInputDepth > 0 && --s_spinInputDepth == 0)
            // if (s_spinContext.Active)
            //     API.LogInfo("[UniqueParameters][StockSpin] InputDirection Post: clearing context.");
            s_spinContext = default;
    }

    private static int ResolveAndSyncStockBoundValue(int incoming)
    {
        if (s_spinContext.HasForcedValue)
            return s_spinContext.ForcedValue;

        var adjusted = AdjustStockBoundValue(incoming, s_spinContext);
        if (adjusted != incoming)
            s_spinContext = s_spinContext with { HasForcedValue = true, ForcedValue = adjusted };

        return adjusted;
    }

    [MethodHook(typeof(app.UIPartsSpin), "set_Num(System.Int32)", MethodHookType.Pre)]
    public static PreHookResult OnSpinSetNumPre(Span<ulong> args)
    {
        if (!s_spinContext.Active || args.Length < 3 || args[1] != s_spinContext.SpinAddress)
            return PreHookResult.Continue;

        var incoming = (int)args[2];
        // var wasSynced = s_spinContext.HasForcedValue;
        var adjusted = ResolveAndSyncStockBoundValue(incoming);
        // API.LogInfo(
        //     $"[UniqueParameters][StockSpin] set_Num: spin=0x{args[1]:X}, incoming={incoming} -> adjusted={adjusted} (synced={wasSynced}, range=[{s_spinContext.MinBound}..{s_spinContext.MaxBound}], dir={s_spinContext.Direction}, wrap={s_spinContext.AllowWrap})");

        if (adjusted != incoming)
            args[2] = (ulong)adjusted;

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.UIPartsScrollList), "SetSelectedIndex(System.Int32, System.Boolean)", MethodHookType.Pre)]
    public static PreHookResult OnScrollListSetSelectedIndexPre(Span<ulong> args)
    {
        if (!s_spinContext.Active || args.Length < 3)
            return PreHookResult.Continue;

        if (s_spinContext.ScrollListAddress != 0 && args[1] != s_spinContext.ScrollListAddress)
            return PreHookResult.Continue;

        var incoming = (int)args[2];
        // var wasSynced = s_spinContext.HasForcedValue;
        var adjusted = ResolveAndSyncStockBoundValue(incoming);
        // API.LogInfo(
        //     $"[UniqueParameters][StockSpin] SetSelectedIndex: list=0x{args[1]:X}, incoming={incoming} -> adjusted={adjusted} (synced={wasSynced}, range=[{s_spinContext.MinBound}..{s_spinContext.MaxBound}], dir={s_spinContext.Direction}, wrap={s_spinContext.AllowWrap})");

        if (adjusted != incoming)
            args[2] = (ulong)adjusted;

        return PreHookResult.Continue;
    }

    private enum UniqueGaugeBackingType
    {
        Stock,
        Timer
    }

    private enum UniqueGaugeCategory
    {
        Stock,
        Powerup,
        InstallTimer
    }

    private sealed class ParsedUniqueGaugeInfo
    {
        public required int FighterId { get; init; }
        public required int MenuRowIndex { get; init; }
        public required int SubIndex { get; init; }
        public required string FuncTypeName { get; init; }
        public required UniqueGaugeBackingType BackingType { get; init; }
        public required UniqueGaugeCategory Category { get; init; }
        public int? MaxTimerValue { get; init; }
        public bool AllowsInfinite { get; init; }
        public required System.Collections.Generic.IReadOnlyList<Guid> OptionMessageGuids { get; init; }

        // 1-indexed append position for PauseMenuAppendElementsRequest (immediately after MenuRowIndex)
        public int AppendIndex => MenuRowIndex + 1;
    }

    private readonly record struct StockSpinContext(
        bool Active,
        ulong SpinAddress,
        ulong ScrollListAddress,
        int Direction,
        bool AllowWrap,
        int MinBound,
        int MaxBound,
        bool HasForcedValue = false,
        int ForcedValue = 0);

    private sealed class GaugeUiSlotGuids
    {
        private readonly List<Guid> _lowerBoundOptionGuids = [.. Enumerable.Range(0, 16).Select(_ => Guid.NewGuid())];
        private readonly List<Guid> _upperBoundOptionGuids = [.. Enumerable.Range(0, 16).Select(_ => Guid.NewGuid())];

        public Guid RandomizerEnableSpinner { get; } = Guid.NewGuid();
        public Guid RandomizerEnableOn { get; } = Guid.NewGuid();
        public Guid RandomizerEnableOff { get; } = Guid.NewGuid();
        public Guid TimerInitialValueSlider { get; } = Guid.NewGuid();
        public Guid ProbabilitySlider { get; } = Guid.NewGuid();
        public Guid UpperBoundElement { get; } = Guid.NewGuid();
        public Guid LowerBoundElement { get; } = Guid.NewGuid();

        public Guid GetUpperBoundOptionGuid(int optionIndex)
        {
            while (_upperBoundOptionGuids.Count <= optionIndex)
                _upperBoundOptionGuids.Add(Guid.NewGuid());

            return _upperBoundOptionGuids[optionIndex];
        }

        public Guid GetLowerBoundOptionGuid(int optionIndex)
        {
            while (_lowerBoundOptionGuids.Count <= optionIndex)
                _lowerBoundOptionGuids.Add(Guid.NewGuid());

            return _lowerBoundOptionGuids[optionIndex];
        }

        public bool IsUpperBoundOptionGuid(Guid guid)
        {
            return _upperBoundOptionGuids.Contains(guid);
        }

        public bool IsLowerBoundOptionGuid(Guid guid)
        {
            return _lowerBoundOptionGuids.Contains(guid);
        }
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Config))]
    private partial class UniqueParametersConfigJsonContext : JsonSerializerContext;

    private sealed class GaugeConfig
    {
        public bool RandomizerEnabled { get; set; }
        public int InitialTimerValue { get; set; }
        public int ActivationProbability { get; set; } = DefaultActivationProbability;
        public int LowerBound { get; set; }
        public int UpperBound { get; set; }
    }

    private class Config
    {
        public Dictionary<string, GaugeConfig> Gauges { get; set; } = [];
    }
}