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

    // Known game localization message GUIDs used to classify unique gauge options and units
    private static readonly Guid InfiniteMessageGuid = Guid.Parse("d6184f70-057a-42d7-b925-bd3a42d74970");
    private static readonly Guid StandardMessageGuid = Guid.Parse("471dda2a-0981-4c84-ab5f-8960ade5eae5");
    private static readonly Guid ActivatedMessageGuid = Guid.Parse("220ad173-a227-4561-9fa3-04e0f269f6a4");
    private static readonly Guid PercentUnitMessageGuid = Guid.Parse("86bf146b-7aa4-447c-8a20-35e4cb943b7d");

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
    [ThreadStatic] private static ulong s_pendingSliderParamAddress;
    [ThreadStatic] private static ulong s_pendingSliderAddress;
    [ThreadStatic] private static ulong s_pendingVitalParamAddress;
    [ThreadStatic] private static ulong s_pendingVitalPartsGroupAddress;
    [ThreadStatic] private static int s_pendingVitalElementIndex;
    [ThreadStatic] private static ulong s_pendingTimerApplyPlayerAddress;

    // Tracks fighter IDs whose ApplyTimer### dynamic hook has already been installed for this assembly lifetime
    private static readonly HashSet<int> HookedTimerFighterIds = [];

    // Active gauges for the currently loaded P1/P2 characters (index maps to GaugeSlots[0..3])
    private readonly List<ParsedUniqueGaugeInfo> _activeGauges = [];

    // Parsed once per Init() from the unmodified _UIData._MenuData tree + TDB metadata
    private readonly Dictionary<int, List<ParsedUniqueGaugeInfo>> _parsedGaugesByFighter = [];

    // Per-slot "Initial Timer Value" slider node (only enabled when that install timer gauge is set to Activate/MAX_START)
    private readonly UICustomElementRequestNode?[] _slotInitialTimerNodes =
        new UICustomElementRequestNode?[MaxActiveGaugeSlots];

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
        var originalRows = PauseMenuManager.GetOriginalChildData(PathToUniqueParamMenu);
        if (originalRows != null) return originalRows;

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
                var rawPtr = field.GetDataRaw(0, false);
                if (rawPtr == 0) continue;
                var value = Marshal.ReadInt32((nint)rawPtr);
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
            if (field.IsLiteral() && field.Type.FullName == "System.Int32")
            {
                var rawPtr = field.GetDataRaw(0, false);
                if (rawPtr != 0) return Marshal.ReadInt32((nint)rawPtr);
            }

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

    private static (List<Guid> OptionMessageGuids, bool AllowsInfinite, List<(int ChildIndex, bool IsActivated)>
        ChildOptions)
        ParseGaugeChildMessages(app.training.TrainingMenuData row)
    {
        var optionGuids = new List<Guid>();
        var childOptions = new List<(int ChildIndex, bool IsActivated)>();
        var allowsInfinite = false;

        var children = row._ChildData;
        if (children == null || children.Length == 0)
            return (optionGuids, allowsInfinite, childOptions);

        var count = children.Length;
        for (var i = 0; i < count; i++)
        {
            var child = children[i];
            if (child == null) continue;

            var msgGuid = ReadMessageGuid(child);
            if (msgGuid == Guid.Empty) continue;

            // Record every original child index (including Infinite) so switching off Activate updates UI immediately
            childOptions.Add((i, i == (int)app.training.UNIQUE_TIMER_TYPE.MAX_START));

            if (i == count - 1 && msgGuid == InfiniteMessageGuid)
            {
                allowsInfinite = true;
                continue;
            }

            optionGuids.Add(msgGuid);
        }

        return (optionGuids, allowsInfinite, childOptions);
    }

    private static bool IsStandardActivatedPowerup(System.Collections.Generic.IReadOnlyList<Guid> optionGuids)
    {
        return optionGuids.Count == 2 &&
               optionGuids[0] == StandardMessageGuid &&
               optionGuids[1] == ActivatedMessageGuid;
    }

    private bool TryEnsureUniqueGaugesParsed(app.training.TrainingManager tm, string source)
    {
        if (_parsedGaugesByFighter.Count > 0)
        {
            EnsureTimerApplyHooksRegistered();
            return true;
        }

        try
        {
            if (!TryParseAllUniqueGauges(tm, source)) return false;

            EnsureTimerApplyHooksRegistered();
            return true;
        }
        catch (Exception ex)
        {
            API.LogWarning(
                $"[UniqueParameters] ({source}) Failed to parse unique gauges (will retry on ParameterSetting.Initialize): {ex.Message}");
            _parsedGaugesByFighter.Clear();
            return false;
        }
    }

    private bool TryGetInstallTimerGauge(int fighterId, out ParsedUniqueGaugeInfo gauge)
    {
        if (_parsedGaugesByFighter.TryGetValue(fighterId, out var gauges))
            foreach (var g in gauges)
                if (g.Category == UniqueGaugeCategory.InstallTimer)
                {
                    gauge = g;
                    return true;
                }

        gauge = null!;
        return false;
    }

    private void EnsureTimerApplyHooksRegistered()
    {
        var tdb = API.GetTDB();
        var paramSettingTypeDef = tdb.FindType("app.training.tf_ParameterSetting");
        if (paramSettingTypeDef == null)
        {
            API.LogWarning(
                "[UniqueParameters] Could not find app.training.tf_ParameterSetting in TDB for timer hooks.");
            return;
        }

        var cPlayerTypeDef = tdb.FindType("nBattle.cPlayer");
        var styleTimerField = cPlayerTypeDef?.GetField("style_timer");
        var styleTimer0Field = cPlayerTypeDef?.GetField("style_timer0");
        if (styleTimerField == null || styleTimer0Field == null)
        {
            API.LogWarning("[UniqueParameters] Could not find style_timer/style_timer0 fields on nBattle.cPlayer.");
            return;
        }

        foreach (var gauges in _parsedGaugesByFighter.Values)
        foreach (var gauge in gauges)
        {
            if (gauge.Category != UniqueGaugeCategory.InstallTimer) continue;
            if (HookedTimerFighterIds.Contains(gauge.FighterId)) continue;

            var methodName = $"ApplyTimer{gauge.FighterId:D3}";
            var method = paramSettingTypeDef.GetMethod(methodName);
            if (method == null)
            {
                API.LogWarning(
                    $"[UniqueParameters] Could not find tf_ParameterSetting.{methodName} for {gauge.FuncTypeName}.");
                continue;
            }

            var fighterId = gauge.FighterId;
            var hook = method.AddHook(false);

            hook.AddPre(args =>
            {
                s_pendingTimerApplyPlayerAddress = _isInitialized && args.Length >= 5 ? args[4] : 0;
                return PreHookResult.Continue;
            });

            hook.AddPost((ref _) =>
            {
                var cPlayerAddress = s_pendingTimerApplyPlayerAddress;
                s_pendingTimerApplyPlayerAddress = 0;

                if (!_isInitialized || cPlayerAddress == 0) return;

                try
                {
                    if (!Instance.TryGetInstallTimerGauge(fighterId, out var activeGauge)) return;

                    if (activeGauge.UniqueTimerFieldOffset.HasValue &&
                        !IsTimerGaugeActivated(GameSingletonManager.TrainingManager, activeGauge))
                        return;

                    var cPlayerMo = ManagedObject.ToManagedObject(cPlayerAddress);
                    if (cPlayerMo == null) return;

                    var isValueType = cPlayerMo.GetTypeDefinition()?.IsValueType() ?? false;
                    var maxActiveTimer = Convert.ToInt32(styleTimer0Field.GetDataBoxed(cPlayerAddress, isValueType));
                    if (maxActiveTimer <= 0) return;

                    var targetTimer = Math.Clamp(GetInitialTimerValue(activeGauge), 0, maxActiveTimer);
                    styleTimerField.SetDataBoxed(cPlayerAddress, (short)targetTimer, isValueType);
                }
                catch (Exception ex)
                {
                    API.LogWarning($"[UniqueParameters] Failed in {methodName} Post: {ex.Message}");
                }
            });

            HookedTimerFighterIds.Add(fighterId);
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
        var uniqueGaugeDataTypeDef = app.training.TrainingData.TM_ParameterSetting.UniqueGaugeData.REFType;
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

            var (optionGuids, allowsInfinite, childOptions) = ParseGaugeChildMessages(row);

            int? maxTimerValue = null;
            uint? uniqueTimerFieldOffset = null;
            UniqueGaugeCategory category;
            if (backingType == UniqueGaugeBackingType.Timer)
            {
                maxTimerValue = TryGetUniqueTimerMaxValue(paramSettingTypeDef, fighterId);
                category = maxTimerValue.HasValue
                    ? UniqueGaugeCategory.InstallTimer
                    : UniqueGaugeCategory.Powerup;

                if (category == UniqueGaugeCategory.InstallTimer)
                {
                    var fieldName = $"timer_0_{fighterId:D3}";
                    uniqueTimerFieldOffset = uniqueGaugeDataTypeDef?.GetField(fieldName)?.OffsetFromBase;
                    if (!uniqueTimerFieldOffset.HasValue)
                        API.LogWarning(
                            $"[UniqueParameters] Could not find field '{fieldName}' on UniqueGaugeData for {funcTypeName}.");
                }
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
                UniqueTimerFieldOffset = uniqueTimerFieldOffset,
                AllowsInfinite = allowsInfinite,
                OptionMessageGuids = optionGuids,
                ChildOptions = childOptions
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
        IsRandomizedEnabled = IsRandomizerFeatureEnabled && _activeGauges.Any(IsGaugeRandomizerEnabled);
    }

    private void ClearActiveUI()
    {
        base.Unload();
        _activeGauges.Clear();
        Array.Clear(_slotInitialTimerNodes);
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

    private static bool IsTimerGaugeActivated(app.training.TrainingManager? tm, ParsedUniqueGaugeInfo gauge)
    {
        if (tm == null || !gauge.UniqueTimerFieldOffset.HasValue) return false;

        var uniqueDataObj = tm._tData?.ParameterSetting?.UniqueData as IObject;
        if (uniqueDataObj == null) return false;

        var addr = uniqueDataObj.GetAddress();
        if (addr == 0) return false;

        var timerType = (app.training.UNIQUE_TIMER_TYPE)Marshal.ReadInt32(
            (IntPtr)(addr + gauge.UniqueTimerFieldOffset.Value));
        return timerType == app.training.UNIQUE_TIMER_TYPE.MAX_START;
    }

    private void SetInitialTimerSliderEnabled(int slot, bool enabled, app.training.BaseParam? baseParam)
    {
        if (slot < 0 || slot >= _slotInitialTimerNodes.Length) return;

        var node = _slotInitialTimerNodes[slot];
        if (node == null) return;

        app.training.TrainingMenuData builtElement;
        try
        {
            builtElement = node.GetBuiltElement();
        }
        catch
        {
            return;
        }

        builtElement.IsEnabled = enabled;

        if (baseParam == null) return;

        var uiFlowParam = ManagedProxy<app.training.UIFlowTrainingMenu.Param>.Create(baseParam);
        var viewDataList = uiFlowParam?.ViewDataList;
        if (viewDataList != null)
            for (var i = 0; i < viewDataList.Count; i++)
            {
                var vd = viewDataList[i];
                if (vd?.Data != null && vd.Data.FuncType == builtElement.FuncType)
                    vd.IsDisable = !enabled;
            }
    }

    private static UICustomElementRequestNode CreateActivationProbabilitySlider(
        ParsedUniqueGaugeInfo gauge, GaugeUiSlotGuids slotGuids, string description, bool isRandomizerEnabled)
    {
        var slider = new UICustomElementRequestNode(slotGuids.ProbabilitySlider,
            new SliderInitializer(
                new CustomMessage("Activation Probability"),
                new CustomMessage(description),
                (0, 100),
                1,
                10,
                isRandomizerEnabled,
                new CustomMessage(PercentUnitMessageGuid)));

        slider.AddHook(new FunctionHookRequest((_, viewData, _) =>
        {
            SetActivationProbability(gauge, viewData.SliderValue);
            return false;
        }));

        return slider;
    }

    private static OptionYHookRequest CreateIntervalOptionHook()
    {
        return new OptionYHookRequest((baseParam, viewData, index) =>
        {
            if (viewData.Data.Type is not (app.training.ItemType.SLIDER_VITAL_1P
                or app.training.ItemType.SLIDER_VITAL_2P))
                return false;

            GameSingletonManager.TrainingManager?.RequestSE(
                app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                0);

            var param = ManagedProxy<app.training.UIFlowTrainingMenu.Param>.Create(baseParam);
            param.ChangeYType(index);

            return false;
        });
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
        _slotInitialTimerNodes[slot] = null;

        switch (gauge.Category)
        {
            case UniqueGaugeCategory.InstallTimer:
            {
                var maxTimer = gauge.MaxTimerValue ?? 0;
                var isTimerActivated = IsTimerGaugeActivated(GameSingletonManager.TrainingManager, gauge);

                var initialValueSlider = new UICustomElementRequestNode(slotGuids.TimerInitialValueSlider,
                    new VitalSliderInitializer(
                        new CustomMessage("Initial Timer Value"),
                        new CustomMessage("Set the starting timer value when this install is activated."),
                        (0, maxTimer),
                        isTimerActivated,
                        intervalOption: 10,
                        guidIcon: app.InputAssign.Digital.Id.UITrainingOptionY));
                initialValueSlider.AddHook(new FunctionHookRequest((_, viewData, _) =>
                {
                    SetInitialTimerValue(gauge, viewData.SliderValue);
                    return false;
                }));
                initialValueSlider.AddHook(CreateIntervalOptionHook());

                _slotInitialTimerNodes[slot] = initialValueSlider;

                if (!IsRandomizerFeatureEnabled)
                    return [initialValueSlider];

                var randomizerToggle = CreateRandomizerToggleSpinner(slot, gauge, slotGuids);

                var probabilitySlider = CreateActivationProbabilitySlider(
                    gauge,
                    slotGuids,
                    "Set the probability (%) of starting with this install activated when randomized.",
                    isRandomizerEnabled);

                var upperBoundSlider = new UICustomElementRequestNode(slotGuids.UpperBoundElement,
                    new VitalSliderInitializer(
                        new CustomMessage("Randomizer Upper Bound"),
                        new CustomMessage("Set the upper bound for timer randomization."),
                        (0, maxTimer),
                        isRandomizerEnabled,
                        intervalOption: 10,
                        guidIcon: app.InputAssign.Digital.Id.UITrainingOptionY));
                upperBoundSlider.AddHook(new FunctionHookRequest((_, viewData, _) =>
                {
                    SetUpperBound(gauge, viewData.SliderValue);
                    return false;
                }));
                upperBoundSlider.AddHook(CreateIntervalOptionHook());

                var lowerBoundSlider = new UICustomElementRequestNode(slotGuids.LowerBoundElement,
                    new VitalSliderInitializer(
                        new CustomMessage("Randomizer Lower Bound"),
                        new CustomMessage("Set the lower bound for timer randomization."),
                        (0, maxTimer),
                        isRandomizerEnabled,
                        intervalOption: 10,
                        guidIcon: app.InputAssign.Digital.Id.UITrainingOptionY));
                lowerBoundSlider.AddHook(new FunctionHookRequest((_, viewData, _) =>
                {
                    SetLowerBound(gauge, viewData.SliderValue);
                    return false;
                }));
                lowerBoundSlider.AddHook(CreateIntervalOptionHook());

                dependentNodes.AddRange([probabilitySlider, upperBoundSlider, lowerBoundSlider]);
                return [initialValueSlider, randomizerToggle, probabilitySlider, upperBoundSlider, lowerBoundSlider];
            }

            case UniqueGaugeCategory.Powerup:
            {
                if (!IsRandomizerFeatureEnabled)
                    return [];

                var randomizerToggle = CreateRandomizerToggleSpinner(slot, gauge, slotGuids);

                var probabilitySlider = CreateActivationProbabilitySlider(
                    gauge,
                    slotGuids,
                    "Set the probability (%) of starting with this powerup activated when randomized.",
                    isRandomizerEnabled);

                dependentNodes.Add(probabilitySlider);
                return [randomizerToggle, probabilitySlider];
            }

            case UniqueGaugeCategory.Stock:
            {
                if (!IsRandomizerFeatureEnabled)
                    return [];

                var randomizerToggle = CreateRandomizerToggleSpinner(slot, gauge, slotGuids);

                var upperBoundSpinner = new UICustomElementRequestNode(slotGuids.UpperBoundElement,
                    new SpinnerInitializer(
                        new CustomMessage("Randomizer Upper Bound"),
                        new CustomMessage("Set the upper bound for stock randomization."),
                        isRandomizerEnabled));
                upperBoundSpinner.AddHook(new SpinBoxHookRequest(_ =>
                    slotGuids.GetUpperBoundOptionGuid(GetUpperBound(gauge))));
                upperBoundSpinner.AddChildren([.. CreateStockOptionItems(gauge, slotGuids, true)]);

                var lowerBoundSpinner = new UICustomElementRequestNode(slotGuids.LowerBoundElement,
                    new SpinnerInitializer(
                        new CustomMessage("Randomizer Lower Bound"),
                        new CustomMessage("Set the lower bound for stock randomization."),
                        isRandomizerEnabled));
                lowerBoundSpinner.AddHook(new SpinBoxHookRequest(_ =>
                    slotGuids.GetLowerBoundOptionGuid(GetLowerBound(gauge))));
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
        var timerOptionHooks = new List<(List<int> Path, FunctionHookRequest Request)>();

        for (var slot = 0; slot < _activeGauges.Count; slot++)
        {
            var gauge = _activeGauges[slot];
            var slotGuids = GaugeSlots[slot];

            var nodes = CreateNodesForGauge(slot, gauge, slotGuids);
            foreach (var node in nodes)
                elementsToAppend.Add((gauge.AppendIndex, node));

            if (gauge.Category == UniqueGaugeCategory.InstallTimer)
            {
                var capturedSlot = slot;
                foreach (var (childIndex, isActivatedOption) in gauge.ChildOptions)
                {
                    var capturedEnabled = isActivatedOption;
                    var hook = new FunctionHookRequest((baseParam, _, _) =>
                    {
                        SetInitialTimerSliderEnabled(capturedSlot, capturedEnabled, baseParam);
                        return (false, PreHookResult.Continue);
                    });

                    // MenuRowIndex and childIndex reference the original unmodified menu tree, as expected by PauseMenuManager
                    timerOptionHooks.Add(([.. PathToUniqueParamMenu, gauge.MenuRowIndex, childIndex], hook));
                }
            }
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

            foreach (var (path, hook) in timerOptionHooks)
            {
                PauseMenuManager.RegisterModification(path, hook);
                ModificationRequests.Add(hook);
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
        if (_isInitialized) return;

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

    protected override void OnRandomizerEnabledChanged(bool enabled)
    {
        if (!_isInitialized) return;

        var tm = GameSingletonManager.TrainingManager;
        if (tm == null) return;

        _activeP1FighterId = -1;
        _activeP2FighterId = -1;

        if (TryEnsureUniqueGaugesParsed(tm, "RandomizerToggle"))
            RefreshActiveCharacterUI(tm);
    }

    public override void Unload()
    {
        if (!_isInitialized) return;

        _isInitialized = false;
        _activeP1FighterId = -1;
        _activeP2FighterId = -1;
        IsRandomizedEnabled = false;

        ClearActiveUI();
        _parsedGaugesByFighter.Clear();
    }

    public override void Randomize()
    {
        if (!_isInitialized || !IsRandomizerFeatureEnabled || !IsRandomizedEnabled || _activeGauges.Count == 0)
            return;

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

                case UniqueGaugeCategory.Powerup:
                {
                    var activate = Random.Shared.Next(100) < cfg.ActivationProbability;
                    if (gauge.BackingType == UniqueGaugeBackingType.Stock)
                        paramFunc.SetUnique((app.CHARA_ID)gauge.FighterId,
                            activate ? app.training.UNIQUE_STOCK_TYPE.ONE : app.training.UNIQUE_STOCK_TYPE.ZERO);
                    else
                        paramFunc.SetUnique((app.CHARA_ID)gauge.FighterId,
                            activate ? app.training.UNIQUE_TIMER_TYPE.MAX_START : app.training.UNIQUE_TIMER_TYPE.OFF);
                    break;
                }

                case UniqueGaugeCategory.InstallTimer:
                {
                    var activate = Random.Shared.Next(100) < cfg.ActivationProbability;
                    paramFunc.SetUnique((app.CHARA_ID)gauge.FighterId,
                        activate ? app.training.UNIQUE_TIMER_TYPE.MAX_START : app.training.UNIQUE_TIMER_TYPE.OFF);
                    if (activate)
                    {
                        var randomTimer = Random.Shared.Next(cfg.LowerBound, cfg.UpperBound + 1);
                        SetInitialTimerValue(gauge, randomTimer);
                    }

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

    private bool TryResolveCustomSlider(Guid funcId, out ParsedUniqueGaugeInfo gauge, out CustomSliderKind kind)
    {
        for (var i = 0; i < _activeGauges.Count; i++)
        {
            var activeGauge = _activeGauges[i];
            var slotGuids = GaugeSlots[i];

            if (funcId == slotGuids.ProbabilitySlider)
            {
                gauge = activeGauge;
                kind = CustomSliderKind.Probability;
                return true;
            }

            if (activeGauge.Category == UniqueGaugeCategory.InstallTimer)
            {
                if (funcId == slotGuids.TimerInitialValueSlider)
                {
                    gauge = activeGauge;
                    kind = CustomSliderKind.TimerInitialValue;
                    return true;
                }

                if (funcId == slotGuids.UpperBoundElement)
                {
                    gauge = activeGauge;
                    kind = CustomSliderKind.TimerUpperBound;
                    return true;
                }

                if (funcId == slotGuids.LowerBoundElement)
                {
                    gauge = activeGauge;
                    kind = CustomSliderKind.TimerLowerBound;
                    return true;
                }
            }
        }

        gauge = null!;
        kind = default;
        return false;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSecondaryList", MethodHookType.Pre)]
    public static PreHookResult OnInitSecondaryListPre(Span<ulong> args)
    {
        if (!_isInitialized) return PreHookResult.Continue;

        try
        {
            var tm = GameSingletonManager.TrainingManager;
            if (tm == null) return PreHookResult.Continue;

            var inst = Instance;
            for (var slot = 0; slot < inst._activeGauges.Count; slot++)
            {
                var gauge = inst._activeGauges[slot];
                if (gauge.Category != UniqueGaugeCategory.InstallTimer) continue;

                var isActivated = IsTimerGaugeActivated(tm, gauge);
                inst.SetInitialTimerSliderEnabled(slot, isActivated, null);
            }
        }
        catch (Exception ex)
        {
            API.LogWarning(
                $"[UniqueParameters] Failed to sync initial timer slider state in InitSecondaryList: {ex.Message}");
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSlider", MethodHookType.Pre)]
    public static PreHookResult OnInitSliderPre(Span<ulong> args)
    {
        if (!_isInitialized || args.Length < 2) return PreHookResult.Continue;

        try
        {
            var currentParam = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[1]);
            var viewDataList = currentParam?.ViewDataList;
            if (viewDataList == null) return PreHookResult.Continue;

            var inst = Instance;
            for (var i = 0; i < viewDataList.Count; i++)
            {
                var vd = viewDataList[i];
                var data = vd?.Data;
                if (data == null) continue;

                if (!UIHelpers.TryGetFunctionId((int)data.FuncType, out var funcId)) continue;

                if (inst.TryResolveCustomSlider(funcId, out var gauge, out var sliderKind))
                {
                    var val = sliderKind switch
                    {
                        CustomSliderKind.Probability => GetActivationProbability(gauge),
                        CustomSliderKind.TimerInitialValue => GetInitialTimerValue(gauge),
                        CustomSliderKind.TimerUpperBound => GetUpperBound(gauge),
                        CustomSliderKind.TimerLowerBound => GetLowerBound(gauge),
                        _ => 0
                    };
                    vd!.SliderValue = val;
                    vd.Index = val;
                }
            }
        }
        catch (Exception ex)
        {
            API.LogWarning($"[UniqueParameters] Failed to initialize unique parameter sliders: {ex.Message}");
        }

        return PreHookResult.Continue;
    }

    private static int ResolveFocusedSliderIndex(app.training.UIFlowTrainingMenu.Param param, int count)
    {
        var viewDataList = param.ViewDataList;
        var selectChildIndex = param.SelectChildIndex;
        var secondaryListIndex = param.SecondaryListIndex;
        var currentDataFuncType = param.CurrentData != null ? (int)param.CurrentData.FuncType : -1;

        if (currentDataFuncType >= 0)
        {
            if (selectChildIndex >= 0 && selectChildIndex < count &&
                viewDataList[selectChildIndex]?.Data != null &&
                (int)viewDataList[selectChildIndex].Data.FuncType == currentDataFuncType)
                return selectChildIndex;

            if (secondaryListIndex >= 0 && secondaryListIndex < count &&
                viewDataList[secondaryListIndex]?.Data != null &&
                (int)viewDataList[secondaryListIndex].Data.FuncType == currentDataFuncType)
                return secondaryListIndex;
        }

        if (selectChildIndex >= 0 && selectChildIndex < count)
            return selectChildIndex;

        return secondaryListIndex >= 0 && secondaryListIndex < count ? secondaryListIndex : -1;
    }

    private static void HandleSliderChangePre(Span<ulong> args)
    {
        if (_isInitialized && args.Length >= 2)
        {
            s_pendingSliderParamAddress = args[1];
            s_pendingSliderAddress = args.Length >= 3 ? args[2] : 0;
        }
        else
        {
            s_pendingSliderParamAddress = 0;
            s_pendingSliderAddress = 0;
        }
    }

    private static app.training.UIFlowTrainingMenu.Param? TryGetTrainingMenuAllParam(bool requireActiveMenu = true)
    {
        var tm = GameSingletonManager.TrainingManager;
        if (tm == null || !tm.IsInit)
            return null;

        if (requireActiveMenu && tm._ActiveMenuType != app.training.TrainingManager.MenuType.ALL)
            return null;

        return ((tm._UITrainingMenu_All as IObject)?.GetField("_ParamData") as ManagedObject)
            ?.As<app.training.UIFlowTrainingMenu.Param>();
    }

    private static void HandleSliderChangePost(string source)
    {
        var paramAddress = s_pendingSliderParamAddress;
        var sliderAddress = s_pendingSliderAddress;
        s_pendingSliderParamAddress = 0;
        s_pendingSliderAddress = 0;

        if (!_isInitialized || paramAddress == 0) return;

        try
        {
            var param = TryGetTrainingMenuAllParam(false);
            if (param == null || (param as IObject)?.GetAddress() != paramAddress) return;

            var viewDataList = param.ViewDataList;
            if (viewDataList == null) return;

            var index = ResolveFocusedSliderIndex(param, viewDataList.Count);
            if (index < 0 || index >= viewDataList.Count) return;

            var viewData = viewDataList[index];
            var data = viewData?.Data;
            if (viewData == null || data == null) return;

            var funcTypeInt = (int)data.FuncType;
            if (!UIHelpers.TryGetFunctionId(funcTypeInt, out var funcId)) return;
            if (!Instance.TryResolveCustomSlider(funcId, out var gauge, out var sliderKind)) return;

            if (sliderKind is CustomSliderKind.TimerUpperBound or CustomSliderKind.TimerLowerBound)
            {
                var (minBound, maxBound) = sliderKind == CustomSliderKind.TimerUpperBound
                    ? (GetLowerBound(gauge), GetMaxBoundForGauge(gauge))
                    : (0, GetUpperBound(gauge));

                var incoming = viewData.SliderValue;
                var clamped = Math.Clamp(incoming, minBound, maxBound);
                if (clamped != incoming)
                {
                    viewData.SliderValue = clamped;
                    viewData.Index = clamped;

                    if (sliderAddress != 0)
                    {
                        var slider = MemoryHelpers.GetAddressAs<app.UIPartsSlider>(sliderAddress);
                        if (slider != null)
                            slider.CurrentParam = clamped;
                    }
                }
            }

            var baseParam = (param as ManagedObject)?.As<app.training.BaseParam>()
                            ?? MemoryHelpers.GetAddressAs<app.training.BaseParam>(paramAddress);
            if (baseParam == null) return;

            GameSingletonManager.TrainingManager!._UIFunc.Function(
                data.FuncType, baseParam, viewData, index);
        }
        catch (Exception ex)
        {
            API.LogWarning($"[UniqueParameters] Failed in {source} Post: {ex.Message}");
        }
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "OnSliderUp(app.UIPartsSlider)", MethodHookType.Pre)]
    public static PreHookResult OnSliderUpPre(Span<ulong> args)
    {
        HandleSliderChangePre(args);
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "OnSliderUp(app.UIPartsSlider)", MethodHookType.Post)]
    public static void OnSliderUpPost(ref ulong retval)
    {
        HandleSliderChangePost("OnSliderUp");
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "OnSliderDown(app.UIPartsSlider)", MethodHookType.Pre)]
    public static PreHookResult OnSliderDownPre(Span<ulong> args)
    {
        HandleSliderChangePre(args);
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "OnSliderDown(app.UIPartsSlider)", MethodHookType.Post)]
    public static void OnSliderDownPost(ref ulong retval)
    {
        HandleSliderChangePost("OnSliderDown");
    }

    [MethodHook(
        typeof(app.training.UIFlowTrainingMenu.Param),
        "UpdateSliderVital(app.UIPartsGroup, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnUpdateSliderVitalPre(Span<ulong> args)
    {
        if (_isInitialized && args.Length >= 4)
        {
            s_pendingVitalParamAddress = args[1];
            s_pendingVitalPartsGroupAddress = args[2];
            s_pendingVitalElementIndex = (int)args[3];
        }
        else
        {
            s_pendingVitalParamAddress = 0;
            s_pendingVitalPartsGroupAddress = 0;
            s_pendingVitalElementIndex = -1;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(
        typeof(app.training.UIFlowTrainingMenu.Param),
        "UpdateSliderVital(app.UIPartsGroup, System.Int32)",
        MethodHookType.Post)]
    public static void OnUpdateSliderVitalPost(ref ulong retval)
    {
        var paramAddress = s_pendingVitalParamAddress;
        var partsGroupAddress = s_pendingVitalPartsGroupAddress;
        var elementIndex = s_pendingVitalElementIndex;
        s_pendingVitalParamAddress = 0;
        s_pendingVitalPartsGroupAddress = 0;
        s_pendingVitalElementIndex = -1;

        if (!_isInitialized || paramAddress == 0 || partsGroupAddress == 0 || elementIndex < 0)
            return;

        try
        {
            var param = TryGetTrainingMenuAllParam(false);
            if (param == null || (param as IObject)?.GetAddress() != paramAddress)
                return;

            var viewDataList = param.ViewDataList;
            if (viewDataList == null || elementIndex >= viewDataList.Count)
                return;

            var data = viewDataList[elementIndex]?.Data;
            if (data == null || !UIHelpers.TryGetFunctionId((int)data.FuncType, out var funcId))
                return;

            if (!Instance.TryResolveCustomSlider(funcId, out var _, out var sliderKind) ||
                sliderKind == CustomSliderKind.Probability)
                return;

            var partsGroup = MemoryHelpers.GetAddressAs<app.UIPartsGroup>(partsGroupAddress);
            var focusItem = partsGroup?.GetFocusItem();
            if (focusItem == null) return;

            var sliderArrow = ManagedProxy<app.UIPartsSliderArrow>.Create(focusItem);
            var gaugeItem = sliderArrow?._PanelGauge;
            if (gaugeItem == null) return;

            const float uniqueHeight = 25.0f;
            const float uniqueSideThickness = 5.0f;
            const float uniqueVerticalOffset = 12.5f;

            var colorLeft = MakeColor(235, 143, 63);
            var colorRight = MakeColor(240, 231, 102);

            var gaugeBg = gaugeItem.GetGuiChild("e_rect_gauge").As<via.gui.Rect>();
            var gaugeBase = gaugeItem.GetGuiChild("e_rect_base").As<via.gui.Rect>();
            var leftBorder = gaugeItem.GetGuiChild("e_tex_glow_L").As<via.gui.Texture>();
            var rightBorder = gaugeItem.GetGuiChild("e_tex_glow_R").As<via.gui.Texture>();
            var topBorder = gaugeItem.GetGuiChild("e_tex_glow_T").As<via.gui.Texture>();
            var bottomBorder = gaugeItem.GetGuiChild("e_tex_glow_B").As<via.gui.Texture>();

            gaugeBg.ColorLeft = colorLeft;
            gaugeBg.ColorRight = colorRight;
            gaugeBg.Size = MakeSize(gaugeBg.Size.w, uniqueHeight);
            gaugeBase.Size = MakeSize(gaugeBase.Size.w, uniqueHeight);

            // L/R glow textures are rotated, so their width maps to bar height and their height is border thickness.
            leftBorder.Size = MakeSize(uniqueHeight, uniqueSideThickness);
            rightBorder.Size = MakeSize(uniqueHeight, uniqueSideThickness);

            var topPos = topBorder.Position;
            topBorder.Position = MakeVec3(
                topPos.x,
                topPos.y == 0 ? -uniqueVerticalOffset : MathF.CopySign(uniqueVerticalOffset, topPos.y),
                topPos.z);

            var bottomPos = bottomBorder.Position;
            bottomBorder.Position = MakeVec3(
                bottomPos.x,
                bottomPos.y == 0 ? uniqueVerticalOffset : MathF.CopySign(uniqueVerticalOffset, bottomPos.y),
                bottomPos.z);

            static via.Size MakeSize(float w, float h)
            {
                var s = via.Size.REFType.CreateValueType().As<via.Size>();
                s.w = w;
                s.h = h;
                return s;
            }

            static via.vec3 MakeVec3(float x, float y, float z)
            {
                var v = via.vec3.REFType.CreateValueType().As<via.vec3>();
                v.x = x;
                v.y = y;
                v.z = z;
                return v;
            }

            static via.Color MakeColor(byte r, byte g, byte b, byte a = 255)
            {
                var color = REFrameworkNET.ValueType.New<via.Color>();
                color.rgba = (uint)((a << 24) | (b << 16) | (g << 8) | r);
                return color;
            }
        }
        catch (Exception ex)
        {
            API.LogWarning($"[UniqueParameters] Failed in OnUpdateSliderVitalPost: {ex.Message}");
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
            var paramData = TryGetTrainingMenuAllParam();
            if (paramData == null) return;

            var currentData = paramData.CurrentData;
            if (currentData == null)
            {
                var secondaryIndex = paramData.SecondaryListIndex;
                if (secondaryIndex < 0 || secondaryIndex >= paramData.ViewDataList.Count) return;

                currentData = paramData.ViewDataList[secondaryIndex]?.Data;
            }

            if (currentData == null) return;

            var funcTypeInt = (int)currentData.FuncType;
            if (!UIHelpers.TryGetFunctionId(funcTypeInt, out var funcId)) return;
            if (!Instance.TryGetStockBoundRange(funcId, out var minBound, out var maxBound)) return;

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

        s_spinInputDepth++;
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.UIPartsSpin), "Add()", MethodHookType.Post)]
    public static void OnSpinAddPost(ref ulong retval)
    {
        if (s_spinInputDepth > 0 && --s_spinInputDepth == 0)
            s_spinContext = default;
    }

    [MethodHook(typeof(app.UIPartsSpin), "Sub()", MethodHookType.Pre)]
    public static PreHookResult OnSpinSubPre(Span<ulong> args)
    {
        if (s_spinInputDepth == 0 && args.Length >= 2)
            TryBeginStockSpinContext(args[1], -1, true, "Sub");

        s_spinInputDepth++;
        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.UIPartsSpin), "Sub()", MethodHookType.Post)]
    public static void OnSpinSubPost(ref ulong retval)
    {
        if (s_spinInputDepth > 0 && --s_spinInputDepth == 0)
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
        var adjusted = ResolveAndSyncStockBoundValue(incoming);
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
        var adjusted = ResolveAndSyncStockBoundValue(incoming);
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

    private enum CustomSliderKind
    {
        Probability,
        TimerInitialValue,
        TimerUpperBound,
        TimerLowerBound
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
        public uint? UniqueTimerFieldOffset { get; init; }
        public bool AllowsInfinite { get; init; }
        public required System.Collections.Generic.IReadOnlyList<Guid> OptionMessageGuids { get; init; }

        public required System.Collections.Generic.IReadOnlyList<(int ChildIndex, bool IsActivated)> ChildOptions
        {
            get;
            init;
        }

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