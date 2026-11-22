using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
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

    // Shared spinner item GUID pool reused across stock upper/lower bound option items (index -> Guid)
    private static readonly List<Guid> StockBoundOptionGuids =
        [.. Enumerable.Range(0, 16).Select(_ => Guid.NewGuid())];

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

    private static Guid GetStockBoundOptionGuid(int optionIndex)
    {
        while (StockBoundOptionGuids.Count <= optionIndex)
            StockBoundOptionGuids.Add(Guid.NewGuid());

        return StockBoundOptionGuids[optionIndex];
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

    private void ParseAllUniqueGauges(app.training.TrainingManager tm)
    {
        _parsedGaugesByFighter.Clear();

        var funcTypeNames = BuildUniqueGaugeFuncTypeNameMap();
        var paramSettingTypeDef = API.GetTDB().FindType("app.training.tf_ParameterSetting");
        var gData = tm.ParamFunc._gData;
        var rows = GetUniqueParamMenuRows(tm);

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row == null) continue;

            var funcTypeInt = (int)row.FuncType;
            if (!funcTypeNames.TryGetValue(funcTypeInt, out var funcTypeName)) continue;
            if (!TryParseUniqueGaugeFuncTypeName(funcTypeName, out var fighterId, out var subIndex)) continue;

            var hasStock = gData.UniqueStockDict.ContainsKey(fighterId);
            var hasTimer = gData.UniqueTimerDict.ContainsKey(fighterId);

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

        uiFlowParam.UpdateSecondaryList();
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
        System.Collections.Generic.IReadOnlyList<Guid> optionMessageGuids)
    {
        var items = new List<UICustomElementRequestNode>(optionMessageGuids.Count);
        for (var i = 0; i < optionMessageGuids.Count; i++)
            items.Add(new UICustomElementRequestNode(
                GetStockBoundOptionGuid(i),
                new SpinnerItemTextInitializer(new CustomMessage(optionMessageGuids[i]))));

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
                upperBoundSpinner.AddChildren([.. CreateStockOptionItems(gauge.OptionMessageGuids)]);

                var lowerBoundSpinner = new UICustomElementRequestNode(slotGuids.LowerBoundElement,
                    new SpinnerInitializer(
                        new CustomMessage("Randomizer Lower Bound"),
                        new CustomMessage("Set the lower bound for stock randomization."),
                        isRandomizerEnabled));
                lowerBoundSpinner.AddChildren([.. CreateStockOptionItems(gauge.OptionMessageGuids)]);

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
            ParseAllUniqueGauges(tm);
            _activeP1FighterId = -1;
            _activeP2FighterId = -1;
            _isInitialized = true;

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
            Instance.RefreshActiveCharacterUI(tm);
        }
        catch (Exception ex)
        {
            API.LogError($"[UniqueParameters] Failed to refresh unique parameters on Initialize: {ex}");
        }
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

    private sealed class GaugeUiSlotGuids
    {
        public Guid RandomizerEnableSpinner { get; } = Guid.NewGuid();
        public Guid RandomizerEnableOn { get; } = Guid.NewGuid();
        public Guid RandomizerEnableOff { get; } = Guid.NewGuid();
        public Guid TimerInitialValueSlider { get; } = Guid.NewGuid();
        public Guid ProbabilitySlider { get; } = Guid.NewGuid();
        public Guid UpperBoundElement { get; } = Guid.NewGuid();
        public Guid LowerBoundElement { get; } = Guid.NewGuid();
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