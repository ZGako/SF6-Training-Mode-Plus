using System.Globalization;
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

public partial class SuperGauge : RandomizableFeatureBase<SuperGauge>
{
    private const int PointsPerStock = 10000;
    private const int PreciseStepsPerStock = 100;
    private const int PointsPerPreciseStep = PointsPerStock / PreciseStepsPerStock;
    private const int MaxStocks = 3;
    private const int MaxPreciseSteps = MaxStocks * PreciseStepsPerStock;
    private const int MaxPoints = MaxStocks * PointsPerStock;

    private const int MinRawBound = 0;
    private const int MaxRawBound = MaxPoints;
    private const int DefaultLowerBound = 0;
    private const int DefaultUpperBound = MaxPoints;

    private const int MinStockBound = 0;
    private const int MaxStockBound = MaxStocks;
    private const int MinPreciseBound = 0;
    private const int MaxPreciseBound = MaxPreciseSteps;

    private static readonly List<int> PathToSuperGaugeP1 = [5, 8];
    private static readonly List<int> PathToSuperGaugeP2 = [5, 9];

    private static readonly Guid SUPER_GAUGE_P1_RANDOMIZER_ENABLE_SPINNER = Guid.NewGuid();
    private static readonly Guid SUPER_GAUGE_P1_RANDOMIZER_UPPER_BOUND = Guid.NewGuid();
    private static readonly Guid SUPER_GAUGE_P1_RANDOMIZER_LOWER_BOUND = Guid.NewGuid();

    private static readonly Guid SUPER_GAUGE_P2_RANDOMIZER_ENABLE_SPINNER = Guid.NewGuid();
    private static readonly Guid SUPER_GAUGE_P2_RANDOMIZER_UPPER_BOUND = Guid.NewGuid();
    private static readonly Guid SUPER_GAUGE_P2_RANDOMIZER_LOWER_BOUND = Guid.NewGuid();

    private static bool isFirstOpen;
    private static bool _isInitialized;

    private static Config _config = new();
    private readonly List<IUIModificationRequest> _modeModificationRequests = [];

    private SuperGauge()
    {
        TrainingParametersAndRandomizer.ConfigSection.BindSection(
            nameof(SuperGauge),
            () => _config,
            cfg => _config = cfg,
            SuperGaugeConfigJsonContext.Default.Config);
    }

    private bool EnabledP1
    {
        get => _config.EnabledP1;
        set
        {
            _config.EnabledP1 = value;
            IsRandomizedEnabled = IsRandomizerFeatureEnabled && (_config.EnabledP1 || _config.EnabledP2);
        }
    }

    private bool EnabledP2
    {
        get => _config.EnabledP2;
        set
        {
            _config.EnabledP2 = value;
            IsRandomizedEnabled = IsRandomizerFeatureEnabled && (_config.EnabledP1 || _config.EnabledP2);
        }
    }

    private static bool IsPointMode(int playerIndex)
    {
        var tm = GameSingletonManager.TrainingManager;
        if (tm == null) return false;
        return tm._tData.ParameterSetting.PlayerDatas[playerIndex].SA_Type == app.training.TM_SA_GAUGE_TYPE.POINT;
    }

    private static int RawToStep(int rawValue, int stepSize)
    {
        return rawValue / stepSize;
    }

    private static int StepToRaw(int stepValue, int stepSize)
    {
        return stepValue * stepSize;
    }

    private static string FormatValue(int stepValue, bool isPointMode)
    {
        return isPointMode
            ? string.Create(CultureInfo.InvariantCulture, $"{stepValue / (double)PreciseStepsPerStock:F2}")
            : $"{stepValue}";
    }

    public override void Init()
    {
        if (_isInitialized) return;

        isFirstOpen = true;
        _config.LowerBoundP1 = Math.Clamp(_config.LowerBoundP1, MinRawBound, MaxRawBound);
        _config.UpperBoundP1 = Math.Clamp(_config.UpperBoundP1, _config.LowerBoundP1, MaxRawBound);
        _config.LowerBoundP2 = Math.Clamp(_config.LowerBoundP2, MinRawBound, MaxRawBound);
        _config.UpperBoundP2 = Math.Clamp(_config.UpperBoundP2, _config.LowerBoundP2, MaxRawBound);

        _isInitialized = true;
        IsRandomizedEnabled = IsRandomizerFeatureEnabled && (_config.EnabledP1 || _config.EnabledP2);

        var intervalOptionHookP1 = new OptionYHookRequest((baseParam, viewData, index) =>
        {
            // check if we're displaying the correct UI element
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

        var intervalOptionHookP2 = intervalOptionHookP1.Clone();

        var toggleSuperTypeP1 = new OptionSelectHookRequest((_, _, _) =>
        {
            ToggleSuperGaugeType(0);
            return false;
        });

        var functionHookP1 = CreateSuperGaugeFunctionHook(0);

        var toggleSuperTypeP2 = new OptionSelectHookRequest((_, _, _) =>
        {
            ToggleSuperGaugeType(1);
            return false;
        });

        var functionHookP2 = CreateSuperGaugeFunctionHook(1);

        try
        {
            PauseMenuManager.RegisterModification(PathToSuperGaugeP1, intervalOptionHookP1);
            ModificationRequests.Add(intervalOptionHookP1);
            PauseMenuManager.RegisterModification(PathToSuperGaugeP1, toggleSuperTypeP1);
            ModificationRequests.Add(toggleSuperTypeP1);
            PauseMenuManager.RegisterModification(PathToSuperGaugeP1, functionHookP1);
            ModificationRequests.Add(functionHookP1);

            PauseMenuManager.RegisterModification(PathToSuperGaugeP2, intervalOptionHookP2);
            ModificationRequests.Add(intervalOptionHookP2);
            PauseMenuManager.RegisterModification(PathToSuperGaugeP2, toggleSuperTypeP2);
            ModificationRequests.Add(toggleSuperTypeP2);
            PauseMenuManager.RegisterModification(PathToSuperGaugeP2, functionHookP2);
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in SuperGauge: {ex.Message}");
        }

        ApplyModeDependentUi();
    }

    private void ClearModeDependentUi()
    {
        UnregisterRequests(_modeModificationRequests);
    }

    private void ApplyModeDependentUi()
    {
        ClearModeDependentUi();

        var includeRandomizer = IsRandomizerFeatureEnabled;

        var guideP1Text = includeRandomizer
            ? "Adjust P1's Super Art Gauge. Use <INPT id=\"UI_X\" type=\"dc\"> to adjust the randomizer settings. " +
              "Use <INPT id=\"UI_BACK\" type=\"dc\"> to toggle gauge precision."
            : "Adjust P1's Super Art Gauge. Use <INPT id=\"UI_BACK\" type=\"dc\"> to toggle gauge precision.";

        var guideP2Text = includeRandomizer
            ? "Adjust P2's Super Art Gauge. Use <INPT id=\"UI_X\" type=\"dc\"> to adjust the randomizer settings. " +
              "Use <INPT id=\"UI_BACK\" type=\"dc\"> to toggle gauge precision."
            : "Adjust P2's Super Art Gauge. Use <INPT id=\"UI_BACK\" type=\"dc\"> to toggle gauge precision.";

        var fieldOverrideP1 = new FieldOverrideRequest
        {
            GuideMessage = new CustomMessage(guideP1Text),
            EndWordsGuid = new CustomMessage(Guid.Parse("86bf146b-7aa4-447c-8a20-35e4cb943b7d")),
            GuidIcon = app.InputAssign.Digital.Id.UITrainingOptionY,
            Interval = 1,
            Interval_Option = 10
        };

        var fieldOverrideP2 = new FieldOverrideRequest
        {
            GuideMessage = new CustomMessage(guideP2Text),
            EndWordsGuid = new CustomMessage(Guid.Parse("86bf146b-7aa4-447c-8a20-35e4cb943b7d")),
            GuidIcon = app.InputAssign.Digital.Id.UITrainingOptionY,
            Interval = 1,
            Interval_Option = 10
        };

        var submenuP1InputGuideHook = includeRandomizer
            ? new CustomInputGuideHookRequest(app.InputAssign.Digital.Id.UITrainingOptionX,
                app.InputAssign.Digital.Id.UITrainingOptionBack, xPos: 595F)
            : new CustomInputGuideHookRequest(app.InputAssign.Digital.Id.UITrainingOptionBack);
        var submenuP2InputGuideHook = submenuP1InputGuideHook.Clone();

        try
        {
            PauseMenuManager.RegisterModification(PathToSuperGaugeP1, fieldOverrideP1);
            _modeModificationRequests.Add(fieldOverrideP1);
            PauseMenuManager.RegisterModification(PathToSuperGaugeP1, submenuP1InputGuideHook);
            _modeModificationRequests.Add(submenuP1InputGuideHook);

            PauseMenuManager.RegisterModification(PathToSuperGaugeP2, fieldOverrideP2);
            _modeModificationRequests.Add(fieldOverrideP2);
            PauseMenuManager.RegisterModification(PathToSuperGaugeP2, submenuP2InputGuideHook);
            _modeModificationRequests.Add(submenuP2InputGuideHook);

            if (includeRandomizer)
            {
                var submenuOpenHookP1 = new OptionXHookRequest((baseParam, viewData, _) =>
                {
                    if (viewData.Data.FuncType == app.training.TrainingFuncType.PARAMETER_SA_GAUGE_1P)
                    {
                        GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                            baseParam);
                        GameSingletonManager.TrainingManager?.RequestSE(
                            app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                            0);
                    }

                    return false;
                });
                var appendChildP1Req = new PauseMenuAppendElementsRequest(
                    [.. CreateRandomizerSubmenuElements(true).Select(e => (-1, e))]);

                var submenuOpenHookP2 = new OptionXHookRequest((baseParam, viewData, _) =>
                {
                    if (viewData.Data.FuncType == app.training.TrainingFuncType.PARAMETER_SA_GAUGE_2P)
                    {
                        GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                            baseParam);
                        GameSingletonManager.TrainingManager?.RequestSE(
                            app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                            0);
                    }

                    return false;
                });
                var appendChildP2Req = new PauseMenuAppendElementsRequest(
                    [.. CreateRandomizerSubmenuElements(false).Select(e => (-1, e))]);

                PauseMenuManager.RegisterModification(PathToSuperGaugeP1, submenuOpenHookP1);
                _modeModificationRequests.Add(submenuOpenHookP1);
                PauseMenuManager.RegisterModification(PathToSuperGaugeP1, appendChildP1Req);
                _modeModificationRequests.Add(appendChildP1Req);

                PauseMenuManager.RegisterModification(PathToSuperGaugeP2, submenuOpenHookP2);
                _modeModificationRequests.Add(submenuOpenHookP2);
                PauseMenuManager.RegisterModification(PathToSuperGaugeP2, appendChildP2Req);
                _modeModificationRequests.Add(appendChildP2Req);
            }
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in SuperGauge mode UI setup: {ex.Message}");
        }
    }

    protected override void OnRandomizerEnabledChanged(bool enabled)
    {
        if (!_isInitialized) return;
        IsRandomizedEnabled = enabled && (_config.EnabledP1 || _config.EnabledP2);
        ApplyModeDependentUi();
    }

    public override void Unload()
    {
        if (!_isInitialized) return;
        _isInitialized = false;
        isFirstOpen = false;
        IsRandomizedEnabled = false;
        ClearModeDependentUi();
        base.Unload();
    }

    private static void ToggleSuperGaugeType(int playerIndex)
    {
        try
        {
            var tm = GameSingletonManager.TrainingManager!;
            tm.RequestSE(
                app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                0);
            var playerData = tm._tData.ParameterSetting.PlayerDatas[playerIndex];
            var newType = playerData.SA_Type == app.training.TM_SA_GAUGE_TYPE.POINT
                ? app.training.TM_SA_GAUGE_TYPE.STOCK
                : app.training.TM_SA_GAUGE_TYPE.POINT;

            tm.ParamFunc.ChangeSAGaugeType(playerIndex, newType);
            var paramFunc = GetTrainingMenuParam();
            paramFunc.UpdatePrimaryList();
            paramFunc.InitSecondaryList();
            paramFunc.InitSlider();
        }
        catch (Exception ex)
        {
            API.LogError($"Exception toggling SuperGauge P{playerIndex + 1} type: {ex.Message}");
        }
    }

    private static FunctionHookRequest CreateSuperGaugeFunctionHook(int playerIndex)
    {
        return new FunctionHookRequest((_, viewData, _) =>
        {
            var tm = GameSingletonManager.TrainingManager!;
            var playerData = tm._tData.ParameterSetting.PlayerDatas[playerIndex];
            var paramFunc = tm.ParamFunc;

            if (playerData.SA_Type != app.training.TM_SA_GAUGE_TYPE.POINT)
            {
                paramFunc.SetSADetailPoint(playerIndex, viewData.SliderValue * PointsPerStock);
                return (false, PreHookResult.Continue);
            }

            paramFunc.ChangeSAGaugeType(playerIndex, app.training.TM_SA_GAUGE_TYPE.POINT);
            paramFunc.SetSADetailPoint(playerIndex, viewData.SliderValue * PointsPerPreciseStep);
            paramFunc.SetSAStock(playerIndex, (app.training.TM_SA_STOCK)(viewData.SliderValue / PreciseStepsPerStock));

            return (false, PreHookResult.Skip);
        });
    }

    private List<UICustomElementRequestNode> CreateRandomizerSubmenuElements(bool isP1)
    {
        var playerIndex = isP1 ? 0 : 1;
        var playerLabel = isP1 ? "P1" : "P2";
        var enableGuid = isP1 ? SUPER_GAUGE_P1_RANDOMIZER_ENABLE_SPINNER : SUPER_GAUGE_P2_RANDOMIZER_ENABLE_SPINNER;
        var upperGuid = isP1 ? SUPER_GAUGE_P1_RANDOMIZER_UPPER_BOUND : SUPER_GAUGE_P2_RANDOMIZER_UPPER_BOUND;
        var lowerGuid = isP1 ? SUPER_GAUGE_P1_RANDOMIZER_LOWER_BOUND : SUPER_GAUGE_P2_RANDOMIZER_LOWER_BOUND;

        int GetStepSize()
        {
            return IsPointMode(playerIndex) ? PointsPerPreciseStep : PointsPerStock;
        }

        int GetMinStep()
        {
            return IsPointMode(playerIndex) ? MinPreciseBound : MinStockBound;
        }

        int GetMaxStep()
        {
            return IsPointMode(playerIndex) ? MaxPreciseBound : MaxStockBound;
        }

        bool GetEnabled()
        {
            return isP1 ? EnabledP1 : EnabledP2;
        }

        void SetEnabled(bool value)
        {
            if (isP1) EnabledP1 = value;
            else EnabledP2 = value;
        }

        int GetLowerBoundRaw()
        {
            return isP1 ? _config.LowerBoundP1 : _config.LowerBoundP2;
        }

        void SetLowerBoundRaw(int value)
        {
            var clamped = Math.Clamp(value, MinRawBound, MaxRawBound);
            if (isP1)
            {
                _config.LowerBoundP1 = clamped;
                if (_config.UpperBoundP1 < clamped) _config.UpperBoundP1 = clamped;
            }
            else
            {
                _config.LowerBoundP2 = clamped;
                if (_config.UpperBoundP2 < clamped) _config.UpperBoundP2 = clamped;
            }
        }

        int GetUpperBoundRaw()
        {
            return isP1 ? _config.UpperBoundP1 : _config.UpperBoundP2;
        }

        void SetUpperBoundRaw(int value)
        {
            var clamped = Math.Clamp(value, MinRawBound, MaxRawBound);
            if (isP1)
            {
                _config.UpperBoundP1 = clamped;
                if (_config.LowerBoundP1 > clamped) _config.LowerBoundP1 = clamped;
            }
            else
            {
                _config.UpperBoundP2 = clamped;
                if (_config.LowerBoundP2 > clamped) _config.LowerBoundP2 = clamped;
            }
        }

        var enableSpinner = new UICustomElementRequestNode(enableGuid,
            new SubMenuSpinnerInitializer(
                new CustomMessage($"Randomize Super Art Gauge {playerLabel}"),
                new CustomMessage($"Toggle whether {playerLabel}'s Super Art Gauge is randomized."),
                (0, 1)));

        enableSpinner.AddHook(new SubMenuSpinnerHookRequest(
            () => GetEnabled() ? 1 : 0,
            val => val == 1 ? ("Enabled", true) : ("Disabled", false)));

        var upperBoundSpinner = new UICustomElementRequestNode(upperGuid,
            new SubMenuSpinnerInitializer(
                new CustomMessage($"Upper Bound Randomization {playerLabel} Super Art Gauge"),
                new CustomMessage($"Set the maximum value {playerLabel}'s Super Art Gauge can reach."),
                (MinStockBound, MaxStockBound),
                GetEnabled()));

        upperBoundSpinner.AddHook(new SubMenuSpinnerHookRequest(
            () => RawToStep(GetUpperBoundRaw(), GetStepSize()),
            val => (FormatValue(val, IsPointMode(playerIndex)), false),
            () => (RawToStep(GetLowerBoundRaw(), GetStepSize()), GetMaxStep())));

        upperBoundSpinner.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            SetUpperBoundRaw(StepToRaw(index, GetStepSize()));
            return false;
        }));

        var lowerBoundSpinner = new UICustomElementRequestNode(lowerGuid,
            new SubMenuSpinnerInitializer(
                new CustomMessage($"Lower Bound Randomization {playerLabel} Super Art Gauge"),
                new CustomMessage($"Set the minimum value {playerLabel}'s Super Art Gauge can drop to."),
                (MinStockBound, MaxStockBound),
                GetEnabled()));

        lowerBoundSpinner.AddHook(new SubMenuSpinnerHookRequest(
            () => RawToStep(GetLowerBoundRaw(), GetStepSize()),
            val => (FormatValue(val, IsPointMode(playerIndex)), false),
            () => (GetMinStep(), RawToStep(GetUpperBoundRaw(), GetStepSize()))));

        lowerBoundSpinner.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            SetLowerBoundRaw(StepToRaw(index, GetStepSize()));
            return false;
        }));

        enableSpinner.AddHook(new FunctionHookRequest((baseParam, _, val) =>
        {
            var enabled = val == 1;
            SetEnabled(enabled);
            upperBoundSpinner.GetBuiltElement().IsEnabled = enabled;
            lowerBoundSpinner.GetBuiltElement().IsEnabled = enabled;

            (baseParam as IObject)?.Call("UpdateList()");

            return false;
        }));

        return [enableSpinner, upperBoundSpinner, lowerBoundSpinner];
    }

    private static void RandomizePlayer(app.training.tf_ParameterSetting.FuncData paramFunc, int playerIndex,
        int lowerBoundRaw, int upperBoundRaw)
    {
        if (IsPointMode(playerIndex))
        {
            var lower = RawToStep(lowerBoundRaw, PointsPerPreciseStep);
            var upper = RawToStep(upperBoundRaw, PointsPerPreciseStep);
            var randVal = Random.Shared.Next(lower, upper + 1);

            paramFunc.ChangeSAGaugeType(playerIndex, app.training.TM_SA_GAUGE_TYPE.POINT);
            paramFunc.SetSADetailPoint(playerIndex, randVal * PointsPerPreciseStep);
            paramFunc.SetSAStock(playerIndex, (app.training.TM_SA_STOCK)(randVal / PreciseStepsPerStock));
        }
        else
        {
            var lower = RawToStep(lowerBoundRaw, PointsPerStock);
            var upper = RawToStep(upperBoundRaw, PointsPerStock);
            var randVal = Random.Shared.Next(lower, upper + 1);

            paramFunc.ChangeSAGaugeType(playerIndex, app.training.TM_SA_GAUGE_TYPE.STOCK);
            paramFunc.SetSAStock(playerIndex, (app.training.TM_SA_STOCK)randVal);
        }
    }

    public override void Randomize()
    {
        if (!_isInitialized || !IsRandomizerFeatureEnabled) return;

        var paramFunc = GameSingletonManager.TrainingManager!.ParamFunc;

        if (EnabledP1)
            RandomizePlayer(paramFunc, 0, _config.LowerBoundP1, _config.UpperBoundP1);

        if (EnabledP2)
            RandomizePlayer(paramFunc, 1, _config.LowerBoundP2, _config.UpperBoundP2);
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSlider", MethodHookType.Pre)]
    public static PreHookResult OnInitSliderPre(Span<ulong> args)
    {
        if (!_isInitialized) return PreHookResult.Continue;

        try
        {
            var currentParam = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[1]);

            if (currentParam == null)
            {
                API.LogWarning("Failed to retrieve current UIFlowTrainingMenu.Param object.");
                return PreHookResult.Continue;
            }

            var tm = GameSingletonManager.TrainingManager!;

            var viewData = currentParam.ViewDataList;

            for (var i = 0; i < viewData.Count; i++)
            {
                // iterate through the list and find the indices for the Drive specific elements.
                var data = viewData[i].Data;
                if (data.FuncType is app.training.TrainingFuncType.PARAMETER_SA_GAUGE_1P
                    or app.training.TrainingFuncType.PARAMETER_SA_GAUGE_2P)
                {
                    var playerIndex = data.FuncType == app.training.TrainingFuncType.PARAMETER_SA_GAUGE_1P ? 0 : 1;

                    var playerData =
                        tm._tData.ParameterSetting.PlayerDatas[playerIndex];

                    if (playerData.SA_Type == app.training.TM_SA_GAUGE_TYPE.POINT)
                    {
                        // in this case we want to override the values with our own
                        var currentVal = playerData.SA_Point / 100;

                        viewData[i].SliderValue = currentVal;
                        viewData[i].Index = currentVal;

                        viewData[i].Data.Type = playerIndex == 0
                            ? app.training.ItemType.SLIDER_VITAL_1P
                            : app.training.ItemType.SLIDER_VITAL_2P;

                        var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
                        limit.x = 0;
                        limit.y = 300;
                        data.LimitValue = limit;
                    }
                    else
                    {
                        viewData[i].Data.Type = playerIndex == 0
                            ? app.training.ItemType.SLIDER_SA_1P
                            : app.training.ItemType.SLIDER_SA_2P;
                        viewData[i].SliderValue = (int)playerData.SA_Stock;
                        viewData[i].Index = (int)playerData.SA_Stock;

                        var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
                        limit.x = 0;
                        limit.y = 3;
                        data.LimitValue = limit;
                    }
                }
            }
        }
        catch (Exception e)
        {
            API.LogError($"Exception in OnInitSliderPre: {e.Message}");
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSecondaryList", MethodHookType.Pre)]
    public static PreHookResult OnInitSecondaryPre(Span<ulong> args)
    {
        if (!_isInitialized) return PreHookResult.Continue;

        // TODO: can probably slim down this function, I believe I need some part of it, but most likely not all of it
        try
        {
            var currentParam = MemoryHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[1]);

            if (currentParam == null)
            {
                API.LogWarning("Failed to retrieve current UIFlowTrainingMenu.Param object.");
                return PreHookResult.Continue;
            }

            var tm = GameSingletonManager.TrainingManager!;

            var viewData = currentParam.ViewDataList;

            for (var i = 0; i < viewData.Count; i++)
            {
                // iterate through the list and find the indices for the Drive specific elements.
                var data = viewData[i].Data;
                if (data.FuncType is app.training.TrainingFuncType.PARAMETER_SA_GAUGE_1P
                    or app.training.TrainingFuncType.PARAMETER_SA_GAUGE_2P)
                {
                    var playerIndex = data.FuncType == app.training.TrainingFuncType.PARAMETER_SA_GAUGE_1P ? 0 : 1;

                    var playerData =
                        tm._tData.ParameterSetting.PlayerDatas[playerIndex];

                    if (playerData.SA_Type == app.training.TM_SA_GAUGE_TYPE.POINT)
                    {
                        // in this case we want to override the values with our own
                        var currentVal = playerData.SA_Point / 100;

                        viewData[i].SliderValue = currentVal;
                        viewData[i].Index = currentVal;

                        viewData[i].Data.Type = playerIndex == 0
                            ? app.training.ItemType.SLIDER_VITAL_1P
                            : app.training.ItemType.SLIDER_VITAL_2P;

                        var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
                        limit.x = 0;
                        limit.y = 300;
                        data.LimitValue = limit;
                    }
                    else
                    {
                        viewData[i].Data.Type = playerIndex == 0
                            ? app.training.ItemType.SLIDER_SA_1P
                            : app.training.ItemType.SLIDER_SA_2P;
                        viewData[i].SliderValue = (int)playerData.SA_Stock;
                        viewData[i].Index = (int)playerData.SA_Stock;

                        var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
                        limit.x = 0;
                        limit.y = 3;
                        data.LimitValue = limit;
                    }
                }
            }
        }
        catch (Exception e)
        {
            API.LogError($"Exception in OnInitSliderPre: {e.Message}");
        }

        return PreHookResult.Continue;
    }

    [MethodHook(
        typeof(app.training.UIFlowTrainingMenu.Param),
        "UpdateSliderVital(app.UIPartsGroup, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnUpdateSliderVitalPre(Span<ulong> args)
    {
        if (!_isInitialized) return PreHookResult.Continue;

        try
        {
            var param = GetTrainingMenuParam();
            var elementIndex = (int)args[3];

            if (elementIndex < 0 || elementIndex >= param.ViewDataList.Count)
                return PreHookResult.Continue;

            var viewData = param.ViewDataList[elementIndex];
            var isTargetType = viewData.Data.FuncType is app.training.TrainingFuncType.PARAMETER_SA_GAUGE_1P
                or app.training.TrainingFuncType.PARAMETER_SA_GAUGE_2P;
            var isP1 = viewData.Data.FuncType == app.training.TrainingFuncType.PARAMETER_SA_GAUGE_1P ||
                       viewData.Data.Type == app.training.ItemType.SLIDER_VITAL_1P;

            var partsGroup = MemoryHelpers.GetAddressAs<app.UIPartsGroup>(args[2]);
            if (partsGroup == null)
            {
                API.LogWarning("Failed to retrieve current app.UIPartsGroup object.");
                return PreHookResult.Continue;
            }

            var sliderArrow = ManagedProxy<app.UIPartsSliderArrow>.Create(partsGroup.GetFocusItem());
            var gaugeItem = sliderArrow._PanelGauge;
            if (gaugeItem == null) return PreHookResult.Continue;

            const float vitalHeight = 20.0f;
            const float saHeight = 25.0f;
            const float vitalSideThickness = 6.0f;
            const float saSideThickness = 5.0f;
            const float vitalVerticalOffset = 10.0f;
            const float saVerticalOffset = 12.5f;

            var targetHeight = isTargetType ? saHeight : vitalHeight;
            var targetSideThickness = isTargetType ? saSideThickness : vitalSideThickness;
            var targetVerticalOffset = isTargetType ? saVerticalOffset : vitalVerticalOffset;

            var gaugeBg = gaugeItem.GetGuiChild("e_rect_gauge").As<via.gui.Rect>();
            var gaugeBase = gaugeItem.GetGuiChild("e_rect_base").As<via.gui.Rect>();
            var leftBorder = gaugeItem.GetGuiChild("e_tex_glow_L").As<via.gui.Texture>();
            var rightBorder = gaugeItem.GetGuiChild("e_tex_glow_R").As<via.gui.Texture>();
            var topBorder = gaugeItem.GetGuiChild("e_tex_glow_T").As<via.gui.Texture>();
            var bottomBorder = gaugeItem.GetGuiChild("e_tex_glow_B").As<via.gui.Texture>();

            if (isTargetType)
            {
                var (colorLeft, colorRight) = isP1
                    ? (MakeColor(205, 28, 121), MakeColor(240, 102, 154))
                    : (MakeColor(28, 111, 205), MakeColor(102, 212, 240));
                gaugeBg.ColorLeft = colorLeft;
                gaugeBg.ColorRight = colorRight;
            }
            else
            {
                gaugeBg.Color = isP1 ? MakeColor(221, 28, 96) : MakeColor(14, 108, 187);
            }
            gaugeBg.Size = MakeSize(gaugeBg.Size.w, targetHeight);
            gaugeBase.Size = MakeSize(gaugeBase.Size.w, targetHeight);

            // L/R glow textures are rotated, so their width maps to bar height and their height is border thickness.
            leftBorder.Size = MakeSize(targetHeight, targetSideThickness);
            rightBorder.Size = MakeSize(targetHeight, targetSideThickness);

            var topPos = topBorder.Position;
            topBorder.Position = MakeVec3(
                topPos.x,
                topPos.y == 0 ? -targetVerticalOffset : MathF.CopySign(targetVerticalOffset, topPos.y),
                topPos.z);

            var bottomPos = bottomBorder.Position;
            bottomBorder.Position = MakeVec3(
                bottomPos.x,
                bottomPos.y == 0 ? targetVerticalOffset : MathF.CopySign(targetVerticalOffset, bottomPos.y),
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
        catch (Exception e)
        {
            API.LogError($"Exception in OnUpdateSliderVitalPre: {e.Message}");
        }

        return PreHookResult.Continue;
    }

    private static app.training.UIFlowTrainingMenu.Param GetTrainingMenuParam()
    {
        return ((GameSingletonManager.TrainingManager!._UITrainingMenu as IObject)?.GetField("_ParamData")
                   as ManagedObject)
               ?.As<app.training.UIFlowTrainingMenu.Param>() ??
               throw new NullReferenceException("Could not get field _ParamData of UITrainingMenu");
    }


    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSecondaryList", MethodHookType.Post)]
    public static void OnFirstMenuOpen(ref ulong retval)
    {
        if (!_isInitialized || !isFirstOpen) return;

        try
        {
            var paramFunc = GetTrainingMenuParam();
            var viewData = paramFunc.ViewDataList;

            var hasSuperGauge = false;
            for (var i = 0; i < viewData.Count; i++)
            {
                var data = viewData[i].Data;
                if (data.FuncType is app.training.TrainingFuncType.PARAMETER_SA_GAUGE_1P
                    or app.training.TrainingFuncType.PARAMETER_SA_GAUGE_2P)
                {
                    hasSuperGauge = true;
                    break;
                }
            }

            if (!hasSuperGauge) return;

            isFirstOpen = false;
            paramFunc.UpdatePrimaryList();
            paramFunc.InitSecondaryList();
            paramFunc.InitSlider();
        }
        catch (Exception e)
        {
            API.LogError($"Exception in OnFirstMenuOpen: {e.Message}");
        }
        // "hacky" fix for the menu not displaying properly, but it is what it is
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Config))]
    private partial class SuperGaugeConfigJsonContext : JsonSerializerContext;

    private class Config
    {
        public bool EnabledP1 { get; set; }
        public int LowerBoundP1 { get; set; } = DefaultLowerBound;
        public int UpperBoundP1 { get; set; } = DefaultUpperBound;

        public bool EnabledP2 { get; set; }
        public int LowerBoundP2 { get; set; } = DefaultLowerBound;
        public int UpperBoundP2 { get; set; } = DefaultUpperBound;
    }
}