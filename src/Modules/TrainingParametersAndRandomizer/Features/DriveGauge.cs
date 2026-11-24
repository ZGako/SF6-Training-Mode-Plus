using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Helpers;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

public partial class DriveGauge : RandomizableFeatureBase<DriveGauge>
{
    private const int PointsPerStock = 10000;
    private const int PointsPerPreciseStep = 1000;
    private const int MaxStocks = 6;
    private const int MaxPreciseSteps = MaxStocks * 10;
    private const int MaxPoints = MaxStocks * PointsPerStock;

    // Encoded values use [-1] as Burnout 0 ("-0"), [-(N + 1)] as Burnout N, and [0..N] as Normal 0..N.
    private const int MinRawBound = -(MaxPoints + 1);
    private const int MaxRawBound = MaxPoints;
    private const int DefaultLowerBound = 0;
    private const int DefaultUpperBound = MaxPoints;

    private const int MinStockBound = -(MaxStocks + 1);
    private const int MaxStockBound = MaxStocks;
    private const int MinPreciseBound = -(MaxPreciseSteps + 1);
    private const int MaxPreciseBound = MaxPreciseSteps;

    private static readonly List<int> PathToDriveGaugeSliderP1 = [5, 4];
    private static readonly List<int> PathToDriveGaugeSliderP2 = [5, 5];

    private static readonly Guid DRIVE_GAUGE_P1_RANDOMIZER_ENABLE_SPINNER = Guid.NewGuid();
    private static readonly Guid DRIVE_GAUGE_P1_RANDOMIZER_UPPER_BOUND = Guid.NewGuid();
    private static readonly Guid DRIVE_GAUGE_P1_RANDOMIZER_LOWER_BOUND = Guid.NewGuid();

    private static readonly Guid DRIVE_GAUGE_P2_RANDOMIZER_ENABLE_SPINNER = Guid.NewGuid();
    private static readonly Guid DRIVE_GAUGE_P2_RANDOMIZER_UPPER_BOUND = Guid.NewGuid();
    private static readonly Guid DRIVE_GAUGE_P2_RANDOMIZER_LOWER_BOUND = Guid.NewGuid();

    private static readonly Field? FocusNewField = nBattle.cPlayer.REFType?.GetField("focus_new");
    private static readonly Field? FocusWaitField = nBattle.cPlayer.REFType?.GetField("focus_wait");
    private static readonly Field? IncapacitatedField = nBattle.cPlayer.REFType?.GetField("incapacitated");
    private static readonly Method? SetDriveEmptyMethod = nBattle.cPlayer.REFType?.GetMethod("SetDriveEmpty");
    private static readonly Method? SetDriveMaxMethod = nBattle.cPlayer.REFType?.GetMethod("SetDriveMax");

    private static readonly Method? DriveEmptyMethod =
        app.BattleSound.REFType?.GetMethod("DriveEmpty(System.UInt32)");

    private static readonly Method? DriveEmptySoundMethod =
        app.BattleSound.REFType?.GetMethod("DriveEmptySound(System.Int32)");

    private static readonly Method? SetDGaugeEmptyEffectMethod =
        nBattle.sGame.REFType?.GetMethod("setDGaugeEmptyEffect(System.Int32, System.Boolean)");

    private static readonly Method? SetStatusEffectMethod =
        nBattle.sGame.REFType?.GetMethod(
            "setStatusEffect(System.Int32, app.battle.vfx.eSysStatusElementID, app.battle.VfxKey.eOperationType)");

    private static readonly ManagedObject?[] BattlePlayers = new ManagedObject?[2];
    private static ManagedObject? _battleSoundMo;
    private static ManagedObject? _battleGameMo;
    private static bool _isInitialized;

    private static Config _config = new();

    [ThreadStatic] private static int s_updateDriveNormalDepth;
    [ThreadStatic] private static int s_updateDriveTimerRecoveryDepth;

    private DriveGauge()
    {
        TrainingParametersAndRandomizer.ConfigSection.BindSection(
            nameof(DriveGauge),
            () => _config,
            cfg => _config = cfg,
            DriveGaugeConfigJsonContext.Default.Config);
    }

    private bool EnabledP1
    {
        get => _config.EnabledP1;
        set
        {
            _config.EnabledP1 = value;
            IsRandomizedEnabled = _config.EnabledP1 || _config.EnabledP2;
        }
    }

    private bool EnabledP2
    {
        get => _config.EnabledP2;
        set
        {
            _config.EnabledP2 = value;
            IsRandomizedEnabled = _config.EnabledP1 || _config.EnabledP2;
        }
    }

    private static bool IsPointMode(int playerIndex)
    {
        var playerDatas = GameSingletonManager.TrainingManager?._tData?.ParameterSetting?.PlayerDatas;
        if (playerDatas == null || playerIndex < 0 || playerIndex >= playerDatas.Length) return false;
        return playerDatas[playerIndex].DG_Type == app.training.TM_DG_GAUGE_TYPE.POINT;
    }

    private static (bool IsBurnout, int Magnitude) DecodeEncodedValue(int encoded)
    {
        return encoded < 0
            ? (true, -(encoded + 1))
            : (false, encoded);
    }

    private static int EncodeValue(bool isBurnout, int magnitude)
    {
        return isBurnout ? -(magnitude + 1) : magnitude;
    }

    private static int RawToStep(int rawValue, int stepSize)
    {
        var (isBurnout, points) = DecodeEncodedValue(rawValue);
        return EncodeValue(isBurnout, points / stepSize);
    }

    private static int StepToRaw(int stepValue, int stepSize)
    {
        var (isBurnout, steps) = DecodeEncodedValue(stepValue);
        return EncodeValue(isBurnout, steps * stepSize);
    }

    private static string FormatValue(int stepValue, bool isPointMode)
    {
        var (isBurnout, magnitude) = DecodeEncodedValue(stepValue);
        var formatted = isPointMode
            ? string.Create(CultureInfo.InvariantCulture, $"{magnitude / 10.0:F1}")
            : $"{magnitude}";
        return isBurnout ? $"{formatted} (Burnout)" : formatted;
    }

    public override void Init()
    {
        _config.LowerBoundP1 = Math.Clamp(_config.LowerBoundP1, MinRawBound, MaxRawBound);
        _config.UpperBoundP1 = Math.Clamp(_config.UpperBoundP1, _config.LowerBoundP1, MaxRawBound);
        _config.LowerBoundP2 = Math.Clamp(_config.LowerBoundP2, MinRawBound, MaxRawBound);
        _config.UpperBoundP2 = Math.Clamp(_config.UpperBoundP2, _config.LowerBoundP2, MaxRawBound);

        IsRandomizedEnabled = _config.EnabledP1 || _config.EnabledP2;

        TryCacheBattleObjects();
        _isInitialized = true;

        // Drive bar P1
        var fieldOverrideP1 = new FieldOverrideRequest
        {
            GuideMessage = new CustomMessage(
                "Adjust P1's Drive Gauge. Use <INPT id=\"UI_X\" type=\"dc\"> to adjust the randomizer settings. " +
                "Use <INPT id=\"UI_BACK\" type=\"dc\"> to toggle gauge precision.")
        };

        var submenuOpenHookP1 = new OptionXHookRequest((baseParam, viewData, _) =>
        {
            if (viewData.Data.FuncType == app.training.TrainingFuncType.PARAMETER_DRIVE_GAUGE_1P)
            {
                GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                    baseParam);
                GameSingletonManager.TrainingManager?.RequestSE(
                    app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                    0);
            }

            return false;
        });

        var toggleDriveTypeP1 = new OptionSelectHookRequest((_, _, _) =>
        {
            ToggleDriveGaugeType(0);
            return false;
        });

        var functionHookP1 = CreateDriveGaugeFunctionHook(0);

        var submenuP1InputGuideHook = new CustomInputGuideHookRequest(app.InputAssign.Digital.Id.UITrainingOptionX,
            app.InputAssign.Digital.Id.UITrainingOptionBack, xPos: 595F);

        var appendChildP1Req = new PauseMenuAppendElementsRequest(
            [.. CreateRandomizerSubmenuElements(true).Select(e => (-1, e))]);

        // Drive bar P2
        var fieldOverrideP2 = new FieldOverrideRequest
        {
            GuideMessage = new CustomMessage(
                "Adjust P2's Drive Gauge. Use <INPT id=\"UI_X\" type=\"dc\"> to adjust the randomizer settings. " +
                "Use <INPT id=\"UI_BACK\" type=\"dc\"> to toggle gauge precision.")
        };

        var submenuOpenHookP2 = new OptionXHookRequest((baseParam, viewData, _) =>
        {
            if (viewData.Data.FuncType == app.training.TrainingFuncType.PARAMETER_DRIVE_GAUGE_2P)
            {
                GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                    baseParam);
                GameSingletonManager.TrainingManager?.RequestSE(
                    app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                    0);
            }

            return false;
        });

        var toggleDriveTypeP2 = new OptionSelectHookRequest((_, _, _) =>
        {
            ToggleDriveGaugeType(1);
            return false;
        });

        var functionHookP2 = CreateDriveGaugeFunctionHook(1);

        var submenuP2InputGuideHook = submenuP1InputGuideHook.Clone();

        var appendChildP2Req = new PauseMenuAppendElementsRequest(
            [.. CreateRandomizerSubmenuElements(false).Select(e => (-1, e))]);

        try
        {
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, fieldOverrideP1);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, submenuOpenHookP1);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, submenuP1InputGuideHook);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, appendChildP1Req);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, toggleDriveTypeP1);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, functionHookP1);

            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, fieldOverrideP2);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, submenuOpenHookP2);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, submenuP2InputGuideHook);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, appendChildP2Req);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, toggleDriveTypeP2);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, functionHookP2);
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in DriveGauge: {ex.Message}");
        }

        ModificationRequests.Add(fieldOverrideP1);
        ModificationRequests.Add(submenuOpenHookP1);
        ModificationRequests.Add(submenuP1InputGuideHook);
        ModificationRequests.Add(appendChildP1Req);
        ModificationRequests.Add(toggleDriveTypeP1);
        ModificationRequests.Add(functionHookP1);

        ModificationRequests.Add(fieldOverrideP2);
        ModificationRequests.Add(submenuOpenHookP2);
        ModificationRequests.Add(submenuP2InputGuideHook);
        ModificationRequests.Add(appendChildP2Req);
        ModificationRequests.Add(toggleDriveTypeP2);
        ModificationRequests.Add(functionHookP2);
    }

    public override void Unload()
    {
        _isInitialized = false;
        Array.Clear(BattlePlayers);
        _battleSoundMo = null;
        _battleGameMo = null;
        base.Unload();
    }

    private static void TryCacheBattleObjects()
    {
        try
        {
            var battle = API.GetTDB().FindType("gBattle");
            var statics = battle?.Statics;
            if (statics == null) return;

            if (BattlePlayers[0] == null || BattlePlayers[1] == null)
            {
                var playerMo = statics.GetField("Player") as ManagedObject;
                var mcPlayer = (playerMo as IObject)?.GetField("mcPlayer");
                if (mcPlayer != null)
                {
                    var playerArray =
                        mcPlayer.GetObjectAs<_System.Array>("Failed to retrieve cPlayer_Array1D from Player.");
                    BattlePlayers[0] = playerArray[0] as ManagedObject;
                    BattlePlayers[1] = playerArray[1] as ManagedObject;
                }
            }

            _battleSoundMo ??= statics.GetField("Sound") as ManagedObject;
            _battleGameMo ??= statics.GetField("Game") as ManagedObject;
        }
        catch (Exception ex)
        {
            API.LogWarning($"[DriveGauge] Failed to cache gBattle objects: {ex.Message}");
        }
    }

    private static app.training.UIFlowTrainingMenu.Param GetTrainingMenuParam()
    {
        return ((GameSingletonManager.TrainingManager!._UITrainingMenu as IObject)?.GetField("_ParamData")
                   as ManagedObject)
               ?.As<app.training.UIFlowTrainingMenu.Param>() ??
               throw new NullReferenceException("Could not get field _ParamData of UITrainingMenu");
    }

    private static void ToggleDriveGaugeType(int playerIndex)
    {
        try
        {
            var tm = GameSingletonManager.TrainingManager!;
            tm.RequestSE(
                app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                0);
            var playerData = tm._tData.ParameterSetting.PlayerDatas[playerIndex];
            var newType = playerData.DG_Type == app.training.TM_DG_GAUGE_TYPE.POINT
                ? app.training.TM_DG_GAUGE_TYPE.STOCK
                : app.training.TM_DG_GAUGE_TYPE.POINT;

            tm.ParamFunc.ChangeDGType(playerIndex, newType);
            GetTrainingMenuParam().InitSlider();
        }
        catch (Exception ex)
        {
            API.LogError($"Exception toggling DriveGauge P{playerIndex + 1} type: {ex.Message}");
        }
    }

    private static FunctionHookRequest CreateDriveGaugeFunctionHook(int playerIndex)
    {
        return new FunctionHookRequest((_, viewData, _) =>
        {
            var tm = GameSingletonManager.TrainingManager!;
            var playerData = tm._tData.ParameterSetting.PlayerDatas[playerIndex];
            var paramFunc = tm.ParamFunc;

            if (playerData.DG_Type != app.training.TM_DG_GAUGE_TYPE.POINT)
            {
                paramFunc.SetDGDetailPoint(playerIndex, viewData.SliderValue * PointsPerStock);
                return (false, PreHookResult.Continue);
            }

            paramFunc.ChangeDGType(playerIndex, app.training.TM_DG_GAUGE_TYPE.POINT);
            paramFunc.SetDGDetailPoint(playerIndex, viewData.SliderValue * PointsPerPreciseStep);
            paramFunc.SetDGStock(playerIndex, (app.training.TM_DG_STOCK)(viewData.SliderValue / 10));

            return (false, PreHookResult.Skip);
        });
    }

    private List<UICustomElementRequestNode> CreateRandomizerSubmenuElements(bool isP1)
    {
        var playerIndex = isP1 ? 0 : 1;
        var playerLabel = isP1 ? "P1" : "P2";
        var enableGuid = isP1 ? DRIVE_GAUGE_P1_RANDOMIZER_ENABLE_SPINNER : DRIVE_GAUGE_P2_RANDOMIZER_ENABLE_SPINNER;
        var upperGuid = isP1 ? DRIVE_GAUGE_P1_RANDOMIZER_UPPER_BOUND : DRIVE_GAUGE_P2_RANDOMIZER_UPPER_BOUND;
        var lowerGuid = isP1 ? DRIVE_GAUGE_P1_RANDOMIZER_LOWER_BOUND : DRIVE_GAUGE_P2_RANDOMIZER_LOWER_BOUND;

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
                new CustomMessage($"Randomize Drive Gauge {playerLabel}"),
                new CustomMessage($"Toggle whether {playerLabel}'s Drive Gauge is randomized."),
                (0, 1)));

        enableSpinner.AddHook(new SubMenuSpinnerHookRequest(
            () => GetEnabled() ? 1 : 0,
            val => val == 1 ? ("Enabled", true) : ("Disabled", false)));

        var upperBoundSpinner = new UICustomElementRequestNode(upperGuid,
            new SubMenuSpinnerInitializer(
                new CustomMessage($"Upper Bound Randomization {playerLabel} Drive Gauge"),
                new CustomMessage($"Set the maximum value {playerLabel}'s Drive Gauge can reach."),
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
                new CustomMessage($"Lower Bound Randomization {playerLabel} Drive Gauge"),
                new CustomMessage($"Set the minimum value {playerLabel}'s Drive Gauge can drop to."),
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
            var (isBurnout, tenths) = DecodeEncodedValue(randVal);

            paramFunc.SetBreakStart(playerIndex, isBurnout);
            paramFunc.ChangeDGType(playerIndex, app.training.TM_DG_GAUGE_TYPE.POINT);
            paramFunc.SetDGDetailPoint(playerIndex, tenths * PointsPerPreciseStep);
            paramFunc.SetDGStock(playerIndex, (app.training.TM_DG_STOCK)(tenths / 10));
        }
        else
        {
            var lower = RawToStep(lowerBoundRaw, PointsPerStock);
            var upper = RawToStep(upperBoundRaw, PointsPerStock);
            var randVal = Random.Shared.Next(lower, upper + 1);
            var (isBurnout, stock) = DecodeEncodedValue(randVal);

            paramFunc.SetBreakStart(playerIndex, isBurnout);
            paramFunc.ChangeDGType(playerIndex, app.training.TM_DG_GAUGE_TYPE.STOCK);
            paramFunc.SetDGStock(playerIndex, (app.training.TM_DG_STOCK)stock);
        }
    }

    public override void Randomize()
    {
        var paramFunc = GameSingletonManager.TrainingManager!.ParamFunc;

        if (EnabledP1)
            RandomizePlayer(paramFunc, 0, _config.LowerBoundP1, _config.UpperBoundP1);

        if (EnabledP2)
            RandomizePlayer(paramFunc, 1, _config.LowerBoundP2, _config.UpperBoundP2);
    }

    [MethodHook(
        typeof(app.training.tf_ParameterSetting),
        "UpdateDriveNormal(app.training.TrainingFuncBase.TrainingPLData)",
        MethodHookType.Pre)]
    public static PreHookResult OnUpdateDriveNormalPre(Span<ulong> args)
    {
        s_updateDriveNormalDepth++;
        return PreHookResult.Continue;
    }

    [MethodHook(
        typeof(app.training.tf_ParameterSetting),
        "UpdateDriveNormal(app.training.TrainingFuncBase.TrainingPLData)",
        MethodHookType.Post)]
    public static void OnUpdateDriveNormalPost(ref ulong retval)
    {
        if (s_updateDriveNormalDepth > 0)
            s_updateDriveNormalDepth--;
    }

    [MethodHook(
        typeof(app.training.tf_ParameterSetting),
        "UpdateDriveTimerRecovery(app.training.TrainingFuncBase.TrainingPLData)",
        MethodHookType.Pre)]
    public static PreHookResult OnUpdateDriveTimerRecoveryPre(Span<ulong> args)
    {
        s_updateDriveTimerRecoveryDepth++;
        return PreHookResult.Continue;
    }

    [MethodHook(
        typeof(app.training.tf_ParameterSetting),
        "UpdateDriveTimerRecovery(app.training.TrainingFuncBase.TrainingPLData)",
        MethodHookType.Post)]
    public static void OnUpdateDriveTimerRecoveryPost(ref ulong retval)
    {
        if (s_updateDriveTimerRecoveryDepth > 0)
            s_updateDriveTimerRecoveryDepth--;
    }

    [MethodHook(
        typeof(app.training.tf_ParameterSetting),
        "_SetDrive(System.Int32, nBattle.cPlayer, System.Int32, System.Boolean)",
        MethodHookType.Pre)]
    public static PreHookResult OnSetDrivePre(Span<ulong> args)
    {
        if (!_isInitialized)
            return PreHookResult.Continue;

        var fromNormal = s_updateDriveNormalDepth > 0;
        var fromTimerRecovery = s_updateDriveTimerRecoveryDepth > 0;
        if ((!fromNormal && !fromTimerRecovery) || args.Length < 3)
            return PreHookResult.Continue;

        try
        {
            var playerIndex = (int)args[2];
            var playerDatas = GameSingletonManager.TrainingManager?._tData?.ParameterSetting?.PlayerDatas;
            if (playerDatas == null || playerIndex < 0 || playerIndex >= playerDatas.Length)
                return PreHookResult.Continue;

            var playerData = playerDatas[playerIndex];
            if (playerData.DG_Type != app.training.TM_DG_GAUGE_TYPE.POINT)
                return PreHookResult.Continue;

            if (fromTimerRecovery && FocusNewField != null && FocusWaitField != null && IncapacitatedField != null)
            {
                var playerMo = BattlePlayers[playerIndex];
                if (playerMo == null)
                {
                    TryCacheBattleObjects();
                    playerMo = BattlePlayers[playerIndex];
                }

                var cPlayerAddr = playerMo?.GetAddress() ?? 0;
                if (cPlayerAddr != 0)
                {
                    var isCurrentlyBurnout =
                        Marshal.ReadByte((nint)IncapacitatedField.GetDataRaw(cPlayerAddr, false)) != 0;
                    var targetBurnout = playerData.Is_DG_Break;

                    const short driveRecoveryWaitFrames = 3;
                    Marshal.WriteInt32((nint)FocusNewField.GetDataRaw(cPlayerAddr, false), playerData.DG_Point);
                    Marshal.WriteInt16((nint)FocusWaitField.GetDataRaw(cPlayerAddr, false), driveRecoveryWaitFrames);

                    if (isCurrentlyBurnout != targetBurnout)
                    {
                        if (_battleSoundMo == null || _battleGameMo == null)
                            TryCacheBattleObjects();

                        if (targetBurnout)
                        {
                            SetDriveEmptyMethod?.Invoke(playerMo!, []);
                            if (_battleSoundMo != null)
                            {
                                DriveEmptyMethod?.Invoke(_battleSoundMo, [(uint)playerIndex]);
                                DriveEmptySoundMethod?.Invoke(_battleSoundMo, [playerIndex]);
                            }

                            if (_battleGameMo != null)
                            {
                                SetDGaugeEmptyEffectMethod?.Invoke(_battleGameMo, [playerIndex, false]);
                                SetStatusEffectMethod?.Invoke(_battleGameMo, [
                                    playerIndex,
                                    (int)app.battle.vfx.eSysStatusElementID.DGAUGE_LOST,
                                    (int)app.battle.VfxKey.eOperationType.CANCEL
                                ]);
                                SetStatusEffectMethod?.Invoke(_battleGameMo, [
                                    playerIndex,
                                    (int)app.battle.vfx.eSysStatusElementID.DGAUGE_LOST,
                                    (int)app.battle.VfxKey.eOperationType.SPAWN
                                ]);
                            }
                        }
                        else
                        {
                            SetDriveMaxMethod?.Invoke(playerMo!, []);
                        }
                    }

                    Marshal.WriteByte(
                        (nint)IncapacitatedField.GetDataRaw(cPlayerAddr, false),
                        (byte)(targetBurnout ? 1 : 0));
                }
            }

            return PreHookResult.Skip;
        }
        catch (Exception e)
        {
            API.LogError($"Exception in OnSetDrivePre: {e.Message}");
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSlider", MethodHookType.Pre)]
    public static PreHookResult OnInitSliderPre(Span<ulong> args)
    {
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
                if (data.FuncType is app.training.TrainingFuncType.PARAMETER_DRIVE_GAUGE_1P
                    or app.training.TrainingFuncType.PARAMETER_DRIVE_GAUGE_2P)
                {
                    var playerData =
                        tm._tData.ParameterSetting.PlayerDatas[
                            data.FuncType == app.training.TrainingFuncType.PARAMETER_DRIVE_GAUGE_1P ? 0 : 1];

                    if (playerData.DG_Type == app.training.TM_DG_GAUGE_TYPE.POINT)
                    {
                        // in this case we want to override the values with our own
                        var currentVal = playerData.DG_Point / 1000;

                        viewData[i].SliderValue = currentVal;
                        viewData[i].Index = currentVal;

                        var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
                        limit.x = 0;
                        limit.y = 60;
                        data.LimitValue = limit;
                    }
                    else
                    {
                        viewData[i].SliderValue = (int)playerData.DG_Stock;
                        viewData[i].Index = (int)playerData.DG_Stock;

                        var limit = via.Int2.REFType.CreateValueType().As<via.Int2>();
                        limit.x = 0;
                        limit.y = 6;
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
        "UpdateSliderDrive(app.UIPartsGroup, System.Int32)",
        MethodHookType.Pre)]
    public static PreHookResult OnUpdateSliderDrivePre(Span<ulong> args)
    {
        try
        {
            var partsGroup = MemoryHelpers.GetAddressAs<app.UIPartsGroup>(args[2]);

            if (partsGroup == null)
            {
                API.LogWarning("Failed to retrieve current app.UIPartsGroup object.");
                return PreHookResult.Continue;
            }

            // get the current value 
            var sliderArrow = ManagedProxy<app.UIPartsSliderArrow>.Create(partsGroup.GetFocusItem());

            // get the values to determine how to update the display
            var max = (int)sliderArrow.RangeMax;
            var current = (int)sliderArrow.CurrentParam;

            const int stocks = 6;

            var stockWidth = max / stocks;

            const float defaultWidth = 60.0f;
            const float leftEdge = -(defaultWidth / 2.0f); // -30.0f
            const float rightBorderOffset = 1.0f; // +1px texture offset (+31 at full width)

            var stockPartIndex = current / stockWidth;
            var remainder = current % stockWidth;

            var selectItem = sliderArrow._SelectItem;
            var gaugeItem = sliderArrow._PanelGauge;

            for (var i = 0; i < stocks; i++)
            {
                var panel = gaugeItem.GetGuiChild($"c_part{i}").As<via.gui.Panel>();
                if (panel == null) continue;

                // Only the active partial bar shrinks; all other bars restore to full width
                var ratio = i == stockPartIndex && remainder != 0
                    ? (float)remainder / stockWidth
                    : 1.0f;

                var targetWidth = defaultWidth * ratio;
                var targetRightX = leftEdge + targetWidth + rightBorderOffset;

                var gaugeBg = panel.GetGuiChild("e_rect_gauge").As<via.gui.Rect>();
                var topBorder = panel.GetGuiChild("e_tex_glow_T").As<via.gui.Texture>();
                var bottomBorder = panel.GetGuiChild("e_tex_glow_B").As<via.gui.Texture>();
                var rightBorder = panel.GetGuiChild("e_tex_glow_R").As<via.gui.Texture>();

                gaugeBg.Size = MakeSize(targetWidth, gaugeBg.Size.h);
                topBorder.Size = MakeSize(targetWidth, topBorder.Size.h);
                bottomBorder.Size = MakeSize(targetWidth, bottomBorder.Size.h);

                var oldPos = rightBorder.Position;
                rightBorder.Position = MakeVec3(targetRightX, oldPos.y, oldPos.z);
            }

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
        }
        catch (Exception e)
        {
            API.LogError($"Exception in OnUpdateSliderDrivePre: {e.Message}");
        }

        return PreHookResult.Continue;
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Config))]
    private partial class DriveGaugeConfigJsonContext : JsonSerializerContext;

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