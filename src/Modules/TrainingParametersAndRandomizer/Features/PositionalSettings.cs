using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Helpers;
using SF6_Plugin_Core.UI.InjectionShared;
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
    private const int DefaultMinRelativeDistance = 70;
    private const int DefaultMaxRelativeDistance = 490;
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

    private static readonly Guid SCREEN_POS_SUBMENU_CUSTOM = Guid.NewGuid();
    private static readonly Guid SCREEN_POS_SUBMENU_ENABLE_RANDOMIZER = Guid.NewGuid();

    private static Config _config = new();

    private static bool _screenPosInitialized;
    private static ScreenPositionType _initialScreenPosition = ScreenPositionType.Center;
    private static int _initialScreenPositionValue;

    private static bool _relativePosInitialized;
    private static RelativePositionType _initialRelativePosition = RelativePositionType.Roundstart;
    private static int _initialRelativePositionValue = 300;

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

    private static bool OpeningRandomizerSubmenu { get; set; }

    private static string FormatUnits(int value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value / 100.0:F2} units");

    private static int GetMinRelativeDistance()
    {
        // TODO: add per-character minimum_distance_offset based on P1 and P2 FighterID
        return DefaultMinRelativeDistance;
    }

    private static int GetMaxRelativeDistance()
    {
        // TODO: subtract per-character position_vs_relative_distance_offset based on P1 and P2 FighterID
        return DefaultMaxRelativeDistance;
    }

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

        var appendRelativeReq = new PauseMenuAppendElementsRequest([(2, relativePosSpinner)]);

        // Reorder original ScreenPosition spinner items from [Center(0), Right(1), Left(2)] -> [Left(2), Center(0), Right(1)]
        // and append custom items so final order is: Left, 17%, 33%, Center, 67%, 83%, Right, Custom
        var screenPosReorderReq = new ReorderRequest([2, 0, 1]);
        var screenPosAppendReq = new PauseMenuAppendElementsRequest(CreateScreenPositionSpinnerItems());
        var screenPosSpinInitHook = new SpinBoxHookRequest(GetCurrentScreenPosition);

        var screenPosOptionYHook = new OptionYHookRequest((baseParam, viewData, _) =>
        {
            var index = viewData.Index;
            var selectedChildFuncType = (int)viewData.Data.ChildData[index].FuncType;
            if (!UIHelpers.TryGetFunctionType(SCREEN_POS_CUSTOM, out var funcType)) return false;

            if (funcType != selectedChildFuncType) return false;
            OpeningRandomizerSubmenu = false;
            GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                baseParam);
            GameSingletonManager.TrainingManager?.RequestSE(
                app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                0);

            return false;
        });

        var screenPosOptionXHook = new OptionXHookRequest((baseParam, _, _) =>
        {
            OpeningRandomizerSubmenu = true;
            GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                baseParam);
            GameSingletonManager.TrainingManager?.RequestSE(
                app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                0);

            return false;
        });

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

        var centerScreenInputGuideHook = new CustomInputGuideHookRequest(app.InputAssign.Digital.Id.UITrainingOptionX);
        var rightScreenInputGuideHook = new CustomInputGuideHookRequest(app.InputAssign.Digital.Id.UITrainingOptionX);
        var leftScreenInputGuideHook = new CustomInputGuideHookRequest(app.InputAssign.Digital.Id.UITrainingOptionX);

        var centerScreenSubmenuReq =
            new PauseMenuAppendElementsRequest([(-1, CreateScreenPositionRandomizerSubmenuElement())]);
        var rightScreenSubmenuReq =
            new PauseMenuAppendElementsRequest([(-1, CreateScreenPositionRandomizerSubmenuElement())]);
        var leftScreenSubmenuReq =
            new PauseMenuAppendElementsRequest([(-1, CreateScreenPositionRandomizerSubmenuElement())]);

        try
        {
            PauseMenuManager.RegisterModification([1], appendRelativeReq);
            ModificationRequests.Add(appendRelativeReq);

            // hooks to the functions (registered before reorder/append using original indices 0=Center, 1=Right, 2=Left)
            PauseMenuManager.RegisterModification([1, 1, 0], centerScreenHook);
            ModificationRequests.Add(centerScreenHook);
            PauseMenuManager.RegisterModification([1, 1, 1], rightScreenHook);
            ModificationRequests.Add(rightScreenHook);
            PauseMenuManager.RegisterModification([1, 1, 2], leftScreenHook);
            ModificationRequests.Add(leftScreenHook);

            PauseMenuManager.RegisterModification([1, 1, 0], centerScreenInputGuideHook);
            ModificationRequests.Add(centerScreenInputGuideHook);
            PauseMenuManager.RegisterModification([1, 1, 1], rightScreenInputGuideHook);
            ModificationRequests.Add(rightScreenInputGuideHook);
            PauseMenuManager.RegisterModification([1, 1, 2], leftScreenInputGuideHook);
            ModificationRequests.Add(leftScreenInputGuideHook);

            PauseMenuManager.RegisterModification([1, 1, 0], centerScreenSubmenuReq);
            ModificationRequests.Add(centerScreenSubmenuReq);
            PauseMenuManager.RegisterModification([1, 1, 1], rightScreenSubmenuReq);
            ModificationRequests.Add(rightScreenSubmenuReq);
            PauseMenuManager.RegisterModification([1, 1, 2], leftScreenSubmenuReq);
            ModificationRequests.Add(leftScreenSubmenuReq);

            // ScreenPosition spinner modifications
            PauseMenuManager.RegisterModification([1, 1], screenPosReorderReq);
            ModificationRequests.Add(screenPosReorderReq);
            PauseMenuManager.RegisterModification([1, 1], screenPosAppendReq);
            ModificationRequests.Add(screenPosAppendReq);
            PauseMenuManager.RegisterModification([1, 1], screenPosSpinInitHook);
            ModificationRequests.Add(screenPosSpinInitHook);
            PauseMenuManager.RegisterModification([1, 1], screenPosOptionYHook);
            ModificationRequests.Add(screenPosOptionYHook);
            PauseMenuManager.RegisterModification([1, 1], screenPosOptionXHook);
            ModificationRequests.Add(screenPosOptionXHook);
        }
        catch (Exception e)
        {
            API.LogError($"Positional settings initialization failed: {e.Message}");
        }
    }

    private static UICustomElementRequestNode CreateScreenPositionRandomizerSubmenuElement()
    {
        var submenuEnable = new UICustomElementRequestNode(SCREEN_POS_SUBMENU_ENABLE_RANDOMIZER,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Enable Randomizer"),
                new CustomMessage("Enable or disable screen position randomization."),
                (0, 1)));

        submenuEnable.AddHook(new SubMenuSpinnerHookRequest(
            () => _config.EnabledScreenPositionRandomizer ? 1 : 0,
            val => val == 1 ? ("Enabled", true) : ("Disabled", false),
            null,
            () => !OpeningRandomizerSubmenu));

        submenuEnable.AddHook(new FunctionHookRequest((_, _, val) =>
        {
            _config.EnabledScreenPositionRandomizer = val == 1;
            return false;
        }));

        return submenuEnable;
    }

    private static List<(int, UICustomElementRequestNode)> CreateScreenPositionSpinnerItems()
    {
        var newElements = new List<(int, UICustomElementRequestNode)>();

        // After ReorderRequest([2, 0, 1]), the existing elements in [1, 1] are:
        // index 0: Left, index 1: Center, index 2: Right
        var screenOptionData =
            new (Guid functionId, string messageId, ScreenPositionType screenPos, int attachToIndex)[]
            {
                (SCREEN_POS_17, "Left (17%)", ScreenPositionType.Pos17, 3),
                (SCREEN_POS_33, "Center-Left (33%)", ScreenPositionType.Pos33, 3),
                (SCREEN_POS_67, "Center-Right (67%)", ScreenPositionType.Pos67, 1),
                (SCREEN_POS_83, "Right (83%)", ScreenPositionType.Pos83, 1)
            };

        foreach (var (functionId, messageId, screenPos, attachToIndex) in screenOptionData)
        {
            var newElement = new UICustomElementRequestNode(functionId,
                new SpinnerItemTextInitializer(new CustomMessage(messageId)));

            newElement.AddHook(new FunctionHookRequest((_, _, _) =>
            {
                CurrentScreenPosition = screenPos;
                SetPositioningData();
                return false;
            }));

            newElements.Add((attachToIndex, newElement));
        }

        // Custom screen position element (appended after Right, which is at index 2 before appends)
        {
            var customScreenElement = new UICustomElementRequestNode(SCREEN_POS_CUSTOM,
                new SpinnerItemTextInitializer(new CustomMessage("Custom Position"),
                    app.InputAssign.Digital.Id.UITrainingOptionY));

            customScreenElement.AddHook(new FunctionHookRequest((_, _, _) =>
            {
                CurrentScreenPosition = ScreenPositionType.CustomPosition;
                SetPositioningData();
                return false;
            }));

            var submenuCustom = new UICustomElementRequestNode(SCREEN_POS_SUBMENU_CUSTOM,
                new SubMenuSpinnerInitializer(
                    new CustomMessage("Custom Position"),
                    new CustomMessage("Set the custom screen position in units"),
                    (MinScreenPos, MaxScreenPos)));

            submenuCustom.AddHook(new SubMenuSpinnerHookRequest(
                () => _config.CustomScreenPosition,
                val => (FormatUnits(val), false),
                null,
                () => OpeningRandomizerSubmenu
            ));

            submenuCustom.AddHook(new FunctionHookRequest((_, _, value) =>
            {
                _config.CustomScreenPosition = value;
                SetPositioningData();
                return false;
            }));

            customScreenElement.AddChildren([submenuCustom]);
            newElements.Add((2, customScreenElement));
        }

        foreach (var (_, element) in newElements)
        {
            var submenuEnable = CreateScreenPositionRandomizerSubmenuElement();

            element.AddHook(new CustomInputGuideHookRequest(app.InputAssign.Digital.Id.UITrainingOptionX));
            element.AddChildren([submenuEnable]);
        }

        return newElements;
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
                new SpinnerItemTextInitializer(new CustomMessage($"{messageId} ({FormatUnits(relativePos.Value)})")));

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
                    (DefaultMinRelativeDistance, DefaultMaxRelativeDistance)));

            submenuCustom.AddHook(new SubMenuSpinnerHookRequest(
                () => _config.CustomRelativePosValue,
                val => (FormatUnits(val), false),
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
                () => _config.EnabledRelativePositionRandomizer ? 1 : 0,
                val => val == 1 ? ("Enabled", true) : ("Disabled", false),
                null,
                () => !OpeningRandomizerSubmenu));

            submenuEnable.AddHook(new FunctionHookRequest((_, _, val) =>
            {
                _config.EnabledRelativePositionRandomizer = val == 1;
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
                var p1Pos = tm._tData.SelectMenu.PlayerDatas[0].ManualPosX;
                var p2Pos = tm._tData.SelectMenu.PlayerDatas[1].ManualPosX;
                var midPointPos = (p1Pos + p2Pos) / 2;

                if (!_config.ScreenPositionIsPreset)
                {
                    if (p1Pos > MinScreenPos && p2Pos < MaxScreenPos)
                        _config.CustomScreenPosition = midPointPos;
                    CurrentScreenPosition = ScreenPositionType.CustomPosition;
                }
                else if (p1Pos <= MinScreenPos)
                {
                    CurrentScreenPosition = _config.ScreenPositionIsCorner
                        ? ScreenPositionType.Left
                        : ScreenPositionType.Pos17;
                }
                else if (p2Pos >= MaxScreenPos)
                {
                    CurrentScreenPosition = _config.ScreenPositionIsCorner
                        ? ScreenPositionType.Right
                        : ScreenPositionType.Pos83;
                }
                else
                {
                    CurrentScreenPosition = ScreenPositionType.FromMidpoint(midPointPos);
                }

                return CurrentScreenPosition.GuidValue;
        }

        throw new UnreachableException("positionSetting should never be MAX");
    }

    private static void SetPositioningData()
    {
        // do stuff here
        var tm = GameSingletonManager.TrainingManager!;

        _config.RelativePositionIsPreset = CurrentRelativePosition != RelativePositionType.CustomRange;
        _config.ScreenPositionIsPreset = CurrentScreenPosition != ScreenPositionType.CustomPosition;
        _config.ScreenPositionIsCorner = CurrentScreenPosition == ScreenPositionType.Left ||
                                         CurrentScreenPosition == ScreenPositionType.Right;

        // here we check if we should change anything first 
        var tmMenuFunc = tm.MenuFunc;
        if (CurrentScreenPosition == ScreenPositionType.Center &&
            CurrentRelativePosition == RelativePositionType.Roundstart)
        {
            tmMenuFunc.ChangeStartLocationType(app.EConfigInitLayout.CENTER);
        }
        else if (CurrentRelativePosition == RelativePositionType.PointBlank &&
                 CurrentScreenPosition == ScreenPositionType.Right)
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

            var relDist = CurrentRelativePosition.Value;

            if (CurrentScreenPosition == ScreenPositionType.Right)
            {
                tmMenuFunc.SetManualLocation(0, MaxScreenPos - relDist);
                tmMenuFunc.SetManualLocation(1, MaxScreenPos);
            }
            else if (CurrentScreenPosition == ScreenPositionType.Left)
            {
                tmMenuFunc.SetManualLocation(0, MinScreenPos);
                tmMenuFunc.SetManualLocation(1, MinScreenPos + relDist);
            }
            else
            {
                var anchor = CurrentScreenPosition.Value;
                var p1Pos = anchor - relDist / 2 - relDist % 2;
                var p2Pos = anchor + relDist / 2;

                if (p1Pos < MinScreenPos)
                {
                    p1Pos = MinScreenPos;
                    p2Pos = MinScreenPos + relDist;
                }
                else if (p2Pos > MaxScreenPos)
                {
                    p2Pos = MaxScreenPos;
                    p1Pos = MaxScreenPos - relDist;
                }

                tmMenuFunc.SetManualLocation(0, p1Pos);
                tmMenuFunc.SetManualLocation(1, p2Pos);
            }
        }
    }

    public override void Randomize()
    {
        API.LogInfo("Do nothing here");
    }

    public readonly record struct RelativePositionType
    {
        private readonly int _offsetValue;

        private RelativePositionType(int offsetValue, Guid guidValue)
        {
            _offsetValue = offsetValue;
            GuidValue = guidValue;
        }

        public int Value
        {
            get
            {
                if (GuidValue == RELATIVE_POS_CUSTOM) return _config.CustomRelativePosValue;
                if (GuidValue == RELATIVE_POS_MAX_RANGE) return GetMaxRelativeDistance();
                return GetMinRelativeDistance() + _offsetValue;
            }
        }

        public Guid GuidValue { get; }

        // Predefined fixed range offsets (added to GetMinRelativeDistance())
        public static RelativePositionType PointBlank => new(0, RELATIVE_POS_POINT_BLANK);
        public static RelativePositionType CloseRange => new(40, RELATIVE_POS_CLOSE_RANGE);
        public static RelativePositionType MidRange => new(100, RELATIVE_POS_MID_RANGE);
        public static RelativePositionType FarRange => new(140, RELATIVE_POS_FAR_RANGE);
        public static RelativePositionType ThrowTech => new(212, RELATIVE_POS_THROW_TECH);
        public static RelativePositionType Roundstart => new(230, RELATIVE_POS_ROUNDSTART);
        public static RelativePositionType ZoningRange => new(330, RELATIVE_POS_ZONING);
        public static RelativePositionType MaxRange => new(0, RELATIVE_POS_MAX_RANGE);

        // Automatically pulls the value from your config object
        public static RelativePositionType CustomRange =>
            new(0, RELATIVE_POS_CUSTOM);

        public static RelativePositionType FromValue(int value)
        {
            if (value == GetMaxRelativeDistance()) return MaxRange;

            var offset = value - GetMinRelativeDistance();
            return offset switch
            {
                0 => PointBlank,
                40 => CloseRange,
                100 => MidRange,
                140 => FarRange,
                212 => ThrowTech,
                230 => Roundstart,
                330 => ZoningRange,
                _ => CustomRange
            };
        }
    }

    public readonly record struct ScreenPositionType
    {
        private const int StageWidth = MaxScreenPos - MinScreenPos;
        private const int SlotWidth = StageWidth / 6;
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
            new(MaxScreenPos, app.training.TrainingFuncType.ENV_POSITION_TYPE_1);

        public static ScreenPositionType Left =>
            new(MinScreenPos, app.training.TrainingFuncType.ENV_POSITION_TYPE_2);

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

        public static ScreenPositionType FromMidpoint(int midPointPos)
        {
            if (Math.Abs(midPointPos - Center.Value) <= 1) return Center;
            if (Math.Abs(midPointPos - Pos17.Value) <= 1) return Pos17;
            if (Math.Abs(midPointPos - Pos33.Value) <= 1) return Pos33;
            if (Math.Abs(midPointPos - Pos67.Value) <= 1) return Pos67;
            if (Math.Abs(midPointPos - Pos83.Value) <= 1) return Pos83;

            _config.CustomScreenPosition = midPointPos;
            return CustomPosition;
        }
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
        public bool EnabledRelativePositionRandomizer { get; set; }

        public bool EnabledScreenPositionRandomizer { get; set; }

        // positional settings

        // since the positional parameter settings get preserved by the game, I only
        // need to remember if it is a custom value, or a preset. (to properly Init the spinner)
        public bool RelativePositionIsPreset { get; set; } = true;

        public int CustomRelativePosValue { get; set; } = 300;

        // screen position setting
        public bool ScreenPositionIsPreset { get; set; } = true;

        // differentiates between Left/Right (corner) and Pos17/Pos83 when hitting the stage boundary
        public bool ScreenPositionIsCorner { get; set; }

        public int CustomScreenPosition { get; set; }
    }
}
