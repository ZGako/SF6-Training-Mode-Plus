using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Helpers;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

public partial class PositionalSettings : RandomizableFeatureBase<PositionalSettings>
{
    private const int MinScreenPos = -765;
    private const int MaxScreenPos = 765;
    private static readonly Guid RELATIVE_POSITION_SPINNER = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_POINT_BLANK = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_ROUNDSTART = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_CLOSE_RANGE = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_MID_RANGE = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_FAR_RANGE = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_THROW_TECH = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_ZONING = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_MAX_RANGE = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_CUSTOM = Guid.NewGuid();

    private static readonly Guid RELATIVE_POS_SUBMENU_CUSTOM = Guid.NewGuid();
    private static readonly Guid RELATIVE_POS_SUBMENU_ENABLE_RANDOMIZER = Guid.NewGuid();

    private static readonly Guid SCREEN_POS_17 = Guid.NewGuid();
    private static readonly Guid SCREEN_POS_33 = Guid.NewGuid();
    private static readonly Guid SCREEN_POS_67 = Guid.NewGuid();
    private static readonly Guid SCREEN_POS_83 = Guid.NewGuid();
    private static readonly Guid SCREEN_POS_CUSTOM = Guid.NewGuid();

    private static Config _config = new();

    private static bool _screenPosInitialized;
    private static ScreenPositionType _initialScreenPosition = ScreenPositionType.Center;
    private static int _initialScreenPositionValue;

    private static bool _relativePosInitialized;
    private static RelativePositionType _initialRelativePosition = RelativePositionType.Roundstart;
    private static int _initialRelativePositionValue = 230;

    private static bool _refreshCallbackRegistered;

    private static ScreenPositionType _currentScreenPosition = ScreenPositionType.Center;
    private static RelativePositionType _currentRelativePosition = RelativePositionType.Roundstart;

    private PositionalSettings()
    {
        TrainingParametersAndRandomizer.ConfigSection.BindSection(
            nameof(PositionalSettings),
            () => _config,
            cfg => _config = cfg,
            PositionalSettingsConfigJsonContext.Default.Config);
    }

    private static ScreenPositionType CurrentScreenPosition
    {
        get => _currentScreenPosition;
        set
        {
            _currentScreenPosition = value;
            if (_screenPosInitialized) return;
            _initialScreenPosition = value;
            _initialScreenPositionValue = value.Value;
            _screenPosInitialized = true;
            RegisterRefreshCheck();
        }
    }

    private static RelativePositionType CurrentRelativePosition
    {
        get => _currentRelativePosition;
        set
        {
            _currentRelativePosition = value;
            if (_relativePosInitialized) return;
            _initialRelativePosition = value;
            _initialRelativePositionValue = value.Value;
            _relativePosInitialized = true;
            RegisterRefreshCheck();
        }
    }

    private static bool EnabledRandomizer2 { get; set; }

    private static bool OpeningRandomizerSubmenu { get; set; }

    private static void RegisterRefreshCheck()
    {
        if (_refreshCallbackRegistered) return;
        _refreshCallbackRegistered = true;
        PauseMenuManager.RequestTrainingModeRefreshOnMenuExit(ShouldRefreshTrainingMode);
    }

    private static bool ShouldRefreshTrainingMode()
    {
        var screenPosChanged = _screenPosInitialized &&
                               (_currentScreenPosition != _initialScreenPosition ||
                                _currentScreenPosition.Value != _initialScreenPositionValue);

        var relativePosChanged = _relativePosInitialized &&
                                 (_currentRelativePosition != _initialRelativePosition ||
                                  _currentRelativePosition.Value != _initialRelativePositionValue);

        _screenPosInitialized = false;
        _relativePosInitialized = false;
        _refreshCallbackRegistered = false;

        return screenPosChanged || relativePosChanged;
    }

    public override void Init()
    {
        var relativePosSpinner = new UICustomElementRequestNode(RELATIVE_POSITION_SPINNER,
            new SpinnerInitializer(
                new CustomMessage("Relative Position"),
                new CustomMessage("Adjust the relative position between the players")));

        relativePosSpinner.AddChildren([.. CreateRelativeSpinnerItems()]);

        relativePosSpinner.AddHook(new OptionYHookRequest((baseParam, viewData, _) =>
        {
            var index = viewData.Index;
            var selectedChildFuncType = (int)viewData.Data.ChildData[index].FuncType;
            if (!UIHelpers.TryGetFunctionType(RELATIVE_POS_CUSTOM, out var funcType)) return false;

            if (funcType != selectedChildFuncType) return false;
            OpeningRandomizerSubmenu = false;
            GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                baseParam);
            GameSingletonManager.TrainingManager?.RequestSE(
                app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                0);

            return false;
        }));

        relativePosSpinner.AddHook(new OptionXHookRequest((baseParam, _, _) =>
        {
            OpeningRandomizerSubmenu = true;
            GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                baseParam);
            GameSingletonManager.TrainingManager?.RequestSE(
                app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                0);

            return false;
        }));

        relativePosSpinner.AddHook(new SpinBoxHookRequest(GetCurrentRelativePosition));

        var screenPosSpinInitHook = new SpinBoxHookRequest(GetCurrentScreenPosition);

        var appendRelativeReq = new PauseMenuAppendElementsRequest([(2, relativePosSpinner)]);

        // here we add hooks to the original ScreenPosition spinner to our ease of use
        var centerScreenHook = new FunctionHookRequest((_, _, _) =>
        {
            CurrentScreenPosition = ScreenPositionType.Center;
            SetPositioningData();
            return false;
        });
        var rightScreenHook = new FunctionHookRequest((_, _, _) =>
        {
            CurrentScreenPosition = ScreenPositionType.Right;
            SetPositioningData();
            return false;
        });
        var leftScreenHook = new FunctionHookRequest((_, _, _) =>
        {
            CurrentScreenPosition = ScreenPositionType.Left;
            SetPositioningData();
            return false;
        });

        try
        {
            PauseMenuManager.RegisterModification([1], appendRelativeReq);
            ModificationRequests.Add(appendRelativeReq);

            // hooks to the functions
            PauseMenuManager.RegisterModification([1, 1], screenPosSpinInitHook);
            ModificationRequests.Add(screenPosSpinInitHook);

            PauseMenuManager.RegisterModification([1, 1, 0], centerScreenHook);
            ModificationRequests.Add(centerScreenHook);
            PauseMenuManager.RegisterModification([1, 1, 1], rightScreenHook);
            ModificationRequests.Add(rightScreenHook);
            PauseMenuManager.RegisterModification([1, 1, 2], leftScreenHook);
            ModificationRequests.Add(leftScreenHook);
        }
        catch (Exception e)
        {
            API.LogError($"Positional settings initialization failed: {e.Message}");
        }
    }

    private static List<UICustomElementRequestNode> CreateRelativeSpinnerItems()
    {
        var newElements = new List<UICustomElementRequestNode>();

        var speedOptionData = new (Guid functionId, string messageID, RelativePositionType relativePos)[]
        {
            (RELATIVE_POS_POINT_BLANK, "Point Blank", RelativePositionType.PointBlank),
            (RELATIVE_POS_CLOSE_RANGE, "Close Range", RelativePositionType.CloseRange),
            (RELATIVE_POS_MID_RANGE, "Mid Range", RelativePositionType.MidRange),
            (RELATIVE_POS_FAR_RANGE, "Far Range", RelativePositionType.FarRange),
            (RELATIVE_POS_THROW_TECH, "Throw Tech Distance", RelativePositionType.ThrowTech),
            (RELATIVE_POS_ROUNDSTART, "Roundstart Distance", RelativePositionType.Roundstart),
            (RELATIVE_POS_ZONING, "Zoning Range", RelativePositionType.ZoningRange),
            (RELATIVE_POS_MAX_RANGE, "Max Range", RelativePositionType.MaxRange)
        };

        foreach (var (functionId, messageId, relativePos) in speedOptionData)
        {
            var newElement = new UICustomElementRequestNode(functionId,
                new SpinnerItemTextInitializer(new CustomMessage(messageId)));

            newElement.AddHook(new FunctionHookRequest((_, _, _) =>
            {
                CurrentRelativePosition = relativePos;
                SetPositioningData();
                return false;
            }));

            newElements.Add(newElement);
        }

        // custom distance element stuff
        {
            var customDistanceElement = new UICustomElementRequestNode(RELATIVE_POS_CUSTOM,
                new SpinnerItemTextInitializer(new CustomMessage("Custom Distance"),
                    app.InputAssign.Digital.Id.UITrainingOptionY));

            customDistanceElement.AddHook(new FunctionHookRequest((_, _, _) =>
            {
                CurrentRelativePosition = RelativePositionType.CustomRange;
                SetPositioningData();
                return false;
            }));

            var submenuCustom = new UICustomElementRequestNode(RELATIVE_POS_SUBMENU_CUSTOM,
                new SubMenuSpinnerInitializer(
                    new CustomMessage("Custom Distance"),
                    new CustomMessage("Set the custom relative distance in units"),
                    (75, 490)));

            submenuCustom.AddHook(new SubMenuSpinnerHookRequest(
                () => _config.CustomRelativePosValue,
                val => ($"{val}", false),
                null,
                () => OpeningRandomizerSubmenu
            ));

            submenuCustom.AddHook(new FunctionHookRequest((_, _, value) =>
            {
                _config.CustomRelativePosValue = value;
                SetPositioningData();
                return false;
            }));


            customDistanceElement.AddChildren([submenuCustom]);
            newElements.Add(customDistanceElement);
        }

        // construct all the in common submenus
        foreach (var element in newElements)
        {
            // randomizer elements (shared)
            // TODO: finish adding these (the other options)
            var submenuEnable = new UICustomElementRequestNode(RELATIVE_POS_SUBMENU_ENABLE_RANDOMIZER,
                new SubMenuSpinnerInitializer(
                    new CustomMessage("Enable Randomizer"),
                    new CustomMessage("Set the custom relative distance in units."),
                    (0, 1)));

            submenuEnable.AddHook(new SubMenuSpinnerHookRequest(
                () => EnabledRandomizer2 ? 1 : 0,
                val => val == 1 ? ("Enabled", true) : ("Disabled", false),
                null,
                () => !OpeningRandomizerSubmenu));

            submenuEnable.AddHook(new FunctionHookRequest((_, _, val) =>
            {
                EnabledRandomizer2 = val == 1;
                return false;
            }));

            // input guide thing
            element.AddHook(new CustomInputGuideHookRequest(app.InputAssign.Digital.Id.UITrainingOptionX));

            element.AddChildren([submenuEnable]);
        }

        return newElements;
    }

    private static TrainingFuncOrGuid GetCurrentRelativePosition(app.training.UIFlowTrainingMenu.Param _)
    {
        // here we get the current positional values, and set it based on that.
        var tm = GameSingletonManager.TrainingManager!;

        var positionSetting = tm._tData.SelectMenu.StartLocation;

        switch (positionSetting)
        {
            case app.EConfigInitLayout.CENTER:
                CurrentScreenPosition = ScreenPositionType.Center;
                CurrentRelativePosition = RelativePositionType.Roundstart;
                return RELATIVE_POS_ROUNDSTART;
            case app.EConfigInitLayout.RIGHT:
                CurrentScreenPosition = ScreenPositionType.Right;
                CurrentRelativePosition = RelativePositionType.PointBlank;
                return RELATIVE_POS_POINT_BLANK;
            case app.EConfigInitLayout.LEFT:
                CurrentScreenPosition = ScreenPositionType.Left;
                CurrentRelativePosition = RelativePositionType.PointBlank;
                return RELATIVE_POS_POINT_BLANK;
            case app.EConfigInitLayout.MANUAL:
                //TODO: finish logic here
                // here we need to check what the current offset between the manual positions are
                var p1Pos = tm._tData.SelectMenu.PlayerDatas[0].ManualPosX;
                var p2Pos = tm._tData.SelectMenu.PlayerDatas[1].ManualPosX;
                var relativePos = p2Pos - p1Pos;
                var type = _config.RelativePositionIsPreset
                    ? RelativePositionType.FromValue(relativePos)
                    : RelativePositionType.CustomRange;
                if (type == RelativePositionType.CustomRange) _config.CustomRelativePosValue = relativePos;
                CurrentRelativePosition = type;
                return type.GuidValue;
        }

        throw new UnreachableException("positionSetting should never be MAX");
    }

    private static TrainingFuncOrGuid GetCurrentScreenPosition(app.training.UIFlowTrainingMenu.Param _)
    {
        var tm = GameSingletonManager.TrainingManager!;
        var positionSetting = tm._tData.SelectMenu.StartLocation;

        switch (positionSetting)
        {
            case app.EConfigInitLayout.CENTER:
                CurrentScreenPosition = ScreenPositionType.Center;
                CurrentRelativePosition = RelativePositionType.Roundstart;
                return app.training.TrainingFuncType.ENV_POSITION_TYPE_0;
            case app.EConfigInitLayout.RIGHT:
                CurrentScreenPosition = ScreenPositionType.Right;
                CurrentRelativePosition = RelativePositionType.PointBlank;
                return app.training.TrainingFuncType.ENV_POSITION_TYPE_1;
            case app.EConfigInitLayout.LEFT:
                CurrentScreenPosition = ScreenPositionType.Left;
                CurrentRelativePosition = RelativePositionType.PointBlank;
                return app.training.TrainingFuncType.ENV_POSITION_TYPE_2;
            case app.EConfigInitLayout.MANUAL:
                //TODO: finish logic here
                // here we need to check what the current offset between the manual positions are
                var p1Pos = tm._tData.SelectMenu.PlayerDatas[0].ManualPosX;
                var p2Pos = tm._tData.SelectMenu.PlayerDatas[1].ManualPosX;
                var midPointPos = (p1Pos + p2Pos) / 2;

                CurrentScreenPosition = midPointPos switch
                {
                    >= 1 => ScreenPositionType.Right,
                    <= -1 => ScreenPositionType.Left,
                    _ => ScreenPositionType.Center
                };
                return CurrentScreenPosition.GuidValue;
        }

        throw new UnreachableException("positionSetting should never be MAX");
    }

    private static void SetPositioningData()
    {
        // do stuff here
        var tm = GameSingletonManager.TrainingManager!;

        _config.RelativePositionIsPreset = CurrentRelativePosition != RelativePositionType.CustomRange;

        // here we check if we should change anything first 
        var tmMenuFunc = tm.MenuFunc;
        if (CurrentScreenPosition == ScreenPositionType.Center &&
            CurrentRelativePosition == RelativePositionType.Roundstart)
        {
            tmMenuFunc.ChangeStartLocationType(app.EConfigInitLayout.CENTER);
            // selectMen.StartLocation = app.EConfigInitLayout.CENTER;
        }
        else if (CurrentRelativePosition == RelativePositionType.PointBlank &&
                 CurrentScreenPosition == ScreenPositionType.Right)
            // do something else here
        {
            tmMenuFunc.ChangeStartLocationType(app.EConfigInitLayout.RIGHT);
        }
        else if (CurrentRelativePosition == RelativePositionType.PointBlank &&
                 CurrentScreenPosition == ScreenPositionType.Left)
        {
            tmMenuFunc.ChangeStartLocationType(app.EConfigInitLayout.LEFT);
        }
        else
        {
            // in this case we know it must use custom logic
            tmMenuFunc.ChangeStartLocationType(app.EConfigInitLayout.MANUAL);

            if (CurrentScreenPosition == ScreenPositionType.Center)
            {
                tmMenuFunc.SetManualLocation(0,
                    -CurrentRelativePosition.Value / 2 - CurrentRelativePosition.Value % 2);
                tmMenuFunc.SetManualLocation(1, CurrentRelativePosition.Value / 2);
            }
            else if (CurrentScreenPosition == ScreenPositionType.Right)
            {
                tmMenuFunc.SetManualLocation(0, MaxScreenPos - CurrentRelativePosition.Value);
                tmMenuFunc.SetManualLocation(1, MaxScreenPos);
            }
            else if (CurrentScreenPosition == ScreenPositionType.Left)
            {
                tmMenuFunc.SetManualLocation(0, MinScreenPos);
                tmMenuFunc.SetManualLocation(1, MinScreenPos + CurrentRelativePosition.Value);
            }
        }
    }

    public override void Randomize()
    {
        API.LogInfo("Do nothing here");
    }

    public readonly record struct RelativePositionType
    {
        private readonly int _value;

        private RelativePositionType(int value, Guid guidValue)
        {
            _value = value;
            GuidValue = guidValue;
        }

        public int Value => GuidValue == RELATIVE_POS_CUSTOM ? _config.CustomRelativePosValue : _value;

        public Guid GuidValue { get; }

        // Predefined fixed ranges
        public static RelativePositionType PointBlank => new(0, RELATIVE_POS_POINT_BLANK);
        public static RelativePositionType CloseRange => new(40, RELATIVE_POS_CLOSE_RANGE);
        public static RelativePositionType MidRange => new(100, RELATIVE_POS_MID_RANGE);
        public static RelativePositionType FarRange => new(140, RELATIVE_POS_FAR_RANGE);
        public static RelativePositionType ThrowTech => new(212, RELATIVE_POS_THROW_TECH);
        public static RelativePositionType Roundstart => new(230, RELATIVE_POS_ROUNDSTART);
        public static RelativePositionType ZoningRange => new(330, RELATIVE_POS_ZONING);
        public static RelativePositionType MaxRange => new(480, RELATIVE_POS_MAX_RANGE);

        // Automatically pulls the value from your config object
        public static RelativePositionType CustomRange =>
            new(0, RELATIVE_POS_CUSTOM);

        public static RelativePositionType FromValue(int value)
        {
            return value switch
            {
                0 => PointBlank,
                40 => CloseRange,
                100 => MidRange,
                140 => FarRange,
                212 => ThrowTech,
                230 => Roundstart,
                330 => ZoningRange,
                480 => MaxRange,
                _ => CustomRange
            };
        }
    }

    public readonly record struct ScreenPositionType
    {
        private const int StageWidth = MaxScreenPos - MinScreenPos;
        private const int SlotWidth = StageWidth / 7;
        private readonly app.training.TrainingFuncType? _funcType;
        private readonly Guid _guidValue;

        private readonly int _value;

        private ScreenPositionType(int value, app.training.TrainingFuncType funcType)
        {
            _value = value;
            _funcType = funcType;
            _guidValue = Guid.Empty;
        }

        private ScreenPositionType(int value, Guid guidValue)
        {
            _value = value;
            _funcType = null;
            _guidValue = guidValue;
        }

        public int Value => _guidValue == SCREEN_POS_CUSTOM ? _config.CustomScreenPosition : _value;

        public TrainingFuncOrGuid GuidValue =>
            _funcType.HasValue ? _funcType.Value : _guidValue;

        // Predefined fixed ranges
        public static ScreenPositionType Center =>
            new(0, app.training.TrainingFuncType.ENV_POSITION_TYPE_0);

        public static ScreenPositionType Right =>
            new(0, app.training.TrainingFuncType.ENV_POSITION_TYPE_1);

        public static ScreenPositionType Left =>
            new(0, app.training.TrainingFuncType.ENV_POSITION_TYPE_2);

        public static ScreenPositionType Pos17 =>
            new(MinScreenPos + SlotWidth, SCREEN_POS_17);

        public static ScreenPositionType Pos33 =>
            new(MinScreenPos + 2 * SlotWidth, SCREEN_POS_33);

        public static ScreenPositionType Pos67 =>
            new(MaxScreenPos - 2 * SlotWidth, SCREEN_POS_67);

        public static ScreenPositionType Pos83 =>
            new(MaxScreenPos - SlotWidth, SCREEN_POS_83);

        public static ScreenPositionType CustomPosition =>
            new(0, SCREEN_POS_CUSTOM);
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Config))]
    private partial class PositionalSettingsConfigJsonContext : JsonSerializerContext;

    private class Config
    {
        // randomizer stuff
        public bool EnabledRandomizer { get; set; }

        // positional settings

        // since the positional parameter settings get preserved by the game, I only
        // need to remember if it is a custom value, or a preset. (to properly Init the spinner)
        public bool RelativePositionIsPreset { get; set; } = true;

        public int CustomRelativePosValue { get; set; } = 230;

        // screen position setting
        public int CustomScreenPosition { get; set; }
    }
}