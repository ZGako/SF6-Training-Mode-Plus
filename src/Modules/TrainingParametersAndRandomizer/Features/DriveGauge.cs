using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

public partial class DriveGauge : RandomizableFeatureBase<DriveGauge>
{
    private const int MinBound = -7;
    private const int MaxBound = 6;
    private const int DefaultLowerBound = 0;
    private const int DefaultUpperBound = 6;

    private static readonly List<int> PathToDriveGaugeSliderP1 = [5, 4];
    private static readonly List<int> PathToDriveGaugeSliderP2 = [5, 5];

    private static readonly Guid DRIVE_GAUGE_P1_RANDOMIZER_ENABLE_SPINNER = Guid.NewGuid();
    private static readonly Guid DRIVE_GAUGE_P1_RANDOMIZER_UPPER_BOUND = Guid.NewGuid();
    private static readonly Guid DRIVE_GAUGE_P1_RANDOMIZER_LOWER_BOUND = Guid.NewGuid();

    private static readonly Guid DRIVE_GAUGE_P2_RANDOMIZER_ENABLE_SPINNER = Guid.NewGuid();
    private static readonly Guid DRIVE_GAUGE_P2_RANDOMIZER_UPPER_BOUND = Guid.NewGuid();
    private static readonly Guid DRIVE_GAUGE_P2_RANDOMIZER_LOWER_BOUND = Guid.NewGuid();

    private static Config _config = new();

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

    // Internal range [-7..-1] maps to Burnout stocks [6..0] (-1 acts as "-0"), and [0..6] maps to normal stocks [0..6].
    private static (bool IsBurnout, int Stock) DecodeValue(int value)
    {
        return value < 0
            ? (true, -(value + 1))
            : (false, value);
    }

    private static string FormatValue(int value)
    {
        var (isBurnout, stock) = DecodeValue(value);
        return isBurnout ? $"{stock} (Burnout)" : $"{stock}";
    }

    public override void Init()
    {
        _config.LowerBoundP1 = Math.Clamp(_config.LowerBoundP1, MinBound, MaxBound);
        _config.UpperBoundP1 = Math.Clamp(_config.UpperBoundP1, _config.LowerBoundP1, MaxBound);
        _config.LowerBoundP2 = Math.Clamp(_config.LowerBoundP2, MinBound, MaxBound);
        _config.UpperBoundP2 = Math.Clamp(_config.UpperBoundP2, _config.LowerBoundP2, MaxBound);

        IsRandomizedEnabled = _config.EnabledP1 || _config.EnabledP2;

        var changeGuideMessageP1Req = new FieldOverrideRequest
        {
            GuideMessage = new CustomMessage(
                "Adjust P1's Drive Gauge. Use <INPT id=\"UI_X\" type=\"dc\"> to adjust the randomizer settings.")
        };

        var changeGuideMessageP2Req = new FieldOverrideRequest
        {
            GuideMessage = new CustomMessage(
                "Adjust P2's Drive Gauge. Use <INPT id=\"UI_X\" type=\"dc\"> to adjust the randomizer settings.")
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

        var submenuP1InputGuideHook = new CustomInputGuideHookRequest(app.InputAssign.Digital.Id.UITrainingOptionX);
        var submenuP2InputGuideHook = submenuP1InputGuideHook.Clone();

        var enableSpinnerP1 = new UICustomElementRequestNode(DRIVE_GAUGE_P1_RANDOMIZER_ENABLE_SPINNER,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Randomize Drive Gauge P1"),
                new CustomMessage("Toggle whether P1's Drive Gauge is randomized."),
                (0, 1)));

        enableSpinnerP1.AddHook(new SubMenuSpinnerHookRequest(
            () => EnabledP1 ? 1 : 0,
            val => val == 1 ? ("Enabled", true) : ("Disabled", false)));

        var upperBoundSpinnerP1 = new UICustomElementRequestNode(DRIVE_GAUGE_P1_RANDOMIZER_UPPER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Upper Bound Randomization P1 Drive Gauge"),
                new CustomMessage("Set the maximum value P1's Drive Gauge can reach."),
                (MinBound, MaxBound),
                EnabledP1));

        upperBoundSpinnerP1.AddHook(new SubMenuSpinnerHookRequest(
            () => _config.UpperBoundP1,
            val => (FormatValue(val), false),
            () => (_config.LowerBoundP1, MaxBound)));

        upperBoundSpinnerP1.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            _config.UpperBoundP1 = index;
            return false;
        }));

        var lowerBoundSpinnerP1 = new UICustomElementRequestNode(DRIVE_GAUGE_P1_RANDOMIZER_LOWER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Lower Bound Randomization P1 Drive Gauge"),
                new CustomMessage("Set the minimum value P1's Drive Gauge can drop to."),
                (MinBound, MaxBound),
                EnabledP1));

        lowerBoundSpinnerP1.AddHook(new SubMenuSpinnerHookRequest(
            () => _config.LowerBoundP1,
            val => (FormatValue(val), false),
            () => (MinBound, _config.UpperBoundP1)));

        lowerBoundSpinnerP1.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            _config.LowerBoundP1 = index;
            return false;
        }));

        enableSpinnerP1.AddHook(new FunctionHookRequest((baseParam, _, val) =>
        {
            switch (val)
            {
                case 0:
                    EnabledP1 = false;
                    upperBoundSpinnerP1.GetBuiltElement().IsEnabled = false;
                    lowerBoundSpinnerP1.GetBuiltElement().IsEnabled = false;
                    break;
                case 1:
                    EnabledP1 = true;
                    upperBoundSpinnerP1.GetBuiltElement().IsEnabled = true;
                    lowerBoundSpinnerP1.GetBuiltElement().IsEnabled = true;
                    break;
            }

            (baseParam as IObject)?.Call("UpdateList()");

            return false;
        }));

        var appendChildP1Req = new PauseMenuAppendElementsRequest([
            (-1, enableSpinnerP1), (-1, upperBoundSpinnerP1),
            (-1, lowerBoundSpinnerP1)
        ]);

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

        var enableSpinnerP2 = new UICustomElementRequestNode(DRIVE_GAUGE_P2_RANDOMIZER_ENABLE_SPINNER,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Randomize Drive Gauge P2"),
                new CustomMessage("Toggle whether P2's Drive Gauge is randomized."),
                (0, 1)));

        enableSpinnerP2.AddHook(new SubMenuSpinnerHookRequest(
            () => EnabledP2 ? 1 : 0,
            val => val == 1 ? ("Enabled", true) : ("Disabled", false)));

        var upperBoundSpinnerP2 = new UICustomElementRequestNode(DRIVE_GAUGE_P2_RANDOMIZER_UPPER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Upper Bound Randomization P2 Drive Gauge"),
                new CustomMessage("Set the maximum value P2's Drive Gauge can reach."),
                (MinBound, MaxBound),
                EnabledP2));

        upperBoundSpinnerP2.AddHook(new SubMenuSpinnerHookRequest(
            () => _config.UpperBoundP2,
            val => (FormatValue(val), false),
            () => (_config.LowerBoundP2, MaxBound)));

        upperBoundSpinnerP2.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            _config.UpperBoundP2 = index;
            return false;
        }));

        var lowerBoundSpinnerP2 = new UICustomElementRequestNode(DRIVE_GAUGE_P2_RANDOMIZER_LOWER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Lower Bound Randomization P2 Drive Gauge"),
                new CustomMessage("Set the minimum value P2's Drive Gauge can drop to."),
                (MinBound, MaxBound),
                EnabledP2));

        lowerBoundSpinnerP2.AddHook(new SubMenuSpinnerHookRequest(
            () => _config.LowerBoundP2,
            val => (FormatValue(val), false),
            () => (MinBound, _config.UpperBoundP2)));

        lowerBoundSpinnerP2.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            _config.LowerBoundP2 = index;
            return false;
        }));

        enableSpinnerP2.AddHook(new FunctionHookRequest((baseParam, _, val) =>
        {
            switch (val)
            {
                case 0:
                    EnabledP2 = false;
                    upperBoundSpinnerP2.GetBuiltElement().IsEnabled = false;
                    lowerBoundSpinnerP2.GetBuiltElement().IsEnabled = false;
                    break;
                case 1:
                    EnabledP2 = true;
                    upperBoundSpinnerP2.GetBuiltElement().IsEnabled = true;
                    lowerBoundSpinnerP2.GetBuiltElement().IsEnabled = true;
                    break;
            }

            (baseParam as IObject)?.Call("UpdateList()");

            return false;
        }));

        var appendChildP2Req = new PauseMenuAppendElementsRequest([
            (-1, enableSpinnerP2), (-1, upperBoundSpinnerP2),
            (-1, lowerBoundSpinnerP2)
        ]);

        try
        {
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, changeGuideMessageP1Req);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, submenuOpenHookP1);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, submenuP1InputGuideHook);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP1, appendChildP1Req);

            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, changeGuideMessageP2Req);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, submenuOpenHookP2);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, submenuP2InputGuideHook);
            PauseMenuManager.RegisterModification(PathToDriveGaugeSliderP2, appendChildP2Req);
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in DriveGauge: {ex.Message}");
        }

        ModificationRequests.Add(changeGuideMessageP1Req);
        ModificationRequests.Add(submenuOpenHookP1);
        ModificationRequests.Add(submenuP1InputGuideHook);
        ModificationRequests.Add(appendChildP1Req);
        ModificationRequests.Add(changeGuideMessageP2Req);
        ModificationRequests.Add(submenuOpenHookP2);
        ModificationRequests.Add(submenuP2InputGuideHook);
        ModificationRequests.Add(appendChildP2Req);
    }

    public override void Randomize()
    {
        var paramFunc = GameSingletonManager.TrainingManager!.ParamFunc;

        if (EnabledP1)
        {
            var randVal = Random.Shared.Next(_config.LowerBoundP1, _config.UpperBoundP1 + 1);
            var (isBurnout, stock) = DecodeValue(randVal);

            paramFunc.SetBreakStart(0, isBurnout);

            // set to stock
            paramFunc.ChangeDGType(0, app.training.TM_DG_GAUGE_TYPE.STOCK);
            paramFunc.SetDGStock(0, (app.training.TM_DG_STOCK)stock);
        }

        if (EnabledP2)
        {
            var randVal = Random.Shared.Next(_config.LowerBoundP2, _config.UpperBoundP2 + 1);
            var (isBurnout, stock) = DecodeValue(randVal);

            paramFunc.SetBreakStart(1, isBurnout);

            // set to stock
            paramFunc.ChangeDGType(1, app.training.TM_DG_GAUGE_TYPE.STOCK);
            paramFunc.SetDGStock(1, (app.training.TM_DG_STOCK)stock);
        }
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