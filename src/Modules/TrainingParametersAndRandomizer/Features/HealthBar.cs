using System.Text.Json;
using System.Text.Json.Serialization;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

public partial class HealthBar : RandomizableFeatureBase<HealthBar>
{
    private static readonly List<int> PathToHPSliderP1 = [5, 0];
    private static readonly List<int> PathToHPSliderP2 = [5, 1];

    private static readonly Guid HP_P1_RANDOMIZER_ENABLE_SPINNER = Guid.NewGuid();
    private static readonly Guid HP_P1_RANDOMIZER_UPPER_BOUND = Guid.NewGuid();
    private static readonly Guid HP_P1_RANDOMIZER_LOWER_BOUND = Guid.NewGuid();

    private static readonly Guid HP_P2_RANDOMIZER_ENABLE_SPINNER = Guid.NewGuid();
    private static readonly Guid HP_P2_RANDOMIZER_UPPER_BOUND = Guid.NewGuid();
    private static readonly Guid HP_P2_RANDOMIZER_LOWER_BOUND = Guid.NewGuid();

    private static Config _config = new();

    private HealthBar()
    {
        TrainingParametersAndRandomizer.ConfigSection.BindSection(
            nameof(HealthBar),
            () => _config,
            cfg => _config = cfg,
            ConfigJsonContext.Default.Config);
    }

    private bool EnabledP1
    {
        get => _config.EnabledP1;
        set
        {
            _config.EnabledP1 = value;
            IsRandomizedEnabled = _config.EnabledP1 || _config.EnabledP2;
            field = value;
        }
    }

    private bool EnabledP2
    {
        get => _config.EnabledP2;
        set
        {
            _config.EnabledP2 = value;
            IsRandomizedEnabled = _config.EnabledP1 || _config.EnabledP2;
            field = value;
        }
    }

    public override void Init()
    {
        IsRandomizedEnabled = _config.EnabledP1 || _config.EnabledP2;

        using var _ = LocalizationManager.UseFileScope("TrainingModePlus");

        var submenuOpenHookP1 = new OptionXHookRequest((baseParam, viewData, _) =>
        {
            if (viewData.Data.FuncType == app.training.TrainingFuncType.PARAMETER_VITAL_1P)
            {
                GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                    baseParam);
                GameSingletonManager.TrainingManager?.RequestSE(
                    app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                    0);
            }

            return false;
        });

        var enableSpinnerP1 = new UICustomElementRequestNode(HP_P1_RANDOMIZER_ENABLE_SPINNER,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Randomize Vitality Gauge P1"),
                new CustomMessage("randomize the vitality "),
                (0, 1)));

        enableSpinnerP1.AddHook(new SubMenuSpinnerHookRequest(
            () => EnabledP1 ? 1 : 0,
            val => val == 1 ? ("Enabled", true) : ("Disabled", false)));

        var upperBoundSpinnerP1 = new UICustomElementRequestNode(HP_P1_RANDOMIZER_UPPER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Upper Bound Randomization P1 Health"),
                new CustomMessage("set the upper bound"), (0, 100), EnabledP1));

        upperBoundSpinnerP1.AddHook(new SubMenuSpinnerHookRequest(
            () => _config.UpperBoundP1,
            val => ($"{val}%", false),
            () => (_config.LowerBoundP1, 100)));

        upperBoundSpinnerP1.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            _config.UpperBoundP1 = index;
            return false;
        }));

        var lowerBoundSpinnerP1 = new UICustomElementRequestNode(HP_P1_RANDOMIZER_LOWER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Lower Bound Randomization P1 Health"),
                new CustomMessage("set the lower bound"), (0, 100), EnabledP1));

        lowerBoundSpinnerP1.AddHook(new SubMenuSpinnerHookRequest(
            () => _config.LowerBoundP1,
            val => ($"{val}%", false),
            () => (0, _config.UpperBoundP1)));

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

            (baseParam as IObject).Call("UpdateList()");

            return false;
        }));

        var appendChildP1Req = new PauseMenuAppendElementsRequest([
            (-1, enableSpinnerP1), (-1, upperBoundSpinnerP1),
            (-1, lowerBoundSpinnerP1)
        ]);

        var submenuOpenHookP2 = new OptionXHookRequest((baseParam, viewData, _) =>
        {
            if (viewData.Data.FuncType == app.training.TrainingFuncType.PARAMETER_VITAL_2P)
            {
                GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                    baseParam);
                GameSingletonManager.TrainingManager?.RequestSE(
                    app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                    0);
            }

            return false;
        });

        var enableSpinnerP2 = new UICustomElementRequestNode(HP_P2_RANDOMIZER_ENABLE_SPINNER,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Randomize Vitality Gauge P2"),
                new CustomMessage("randomize the vitality "),
                (0, 1)));

        enableSpinnerP2.AddHook(new SubMenuSpinnerHookRequest(
            () => EnabledP2 ? 1 : 0,
            val => val == 1 ? ("Enabled", true) : ("Disabled", false)));

        var upperBoundSpinnerP2 = new UICustomElementRequestNode(HP_P2_RANDOMIZER_UPPER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Upper Bound Randomization P2 Health"),
                new CustomMessage("set the upper bound"), (0, 100), EnabledP2));

        upperBoundSpinnerP2.AddHook(new SubMenuSpinnerHookRequest(
            () => _config.UpperBoundP2,
            val => ($"{val}%", false),
            () => (_config.LowerBoundP2, 100)));

        upperBoundSpinnerP2.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            _config.UpperBoundP2 = index;
            return false;
        }));

        var lowerBoundSpinnerP2 = new UICustomElementRequestNode(HP_P2_RANDOMIZER_LOWER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Lower Bound Randomization P2 Health"),
                new CustomMessage("set the lower bound"), (0, 100), EnabledP2));

        lowerBoundSpinnerP2.AddHook(new SubMenuSpinnerHookRequest(
            () => _config.LowerBoundP2,
            val => ($"{val}%", false),
            () => (0, _config.UpperBoundP2)));

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

            (baseParam as IObject).Call("UpdateList()");

            return false;
        }));

        var appendChildP2Req = new PauseMenuAppendElementsRequest([
            (-1, enableSpinnerP2), (-1, upperBoundSpinnerP2),
            (-1, lowerBoundSpinnerP2)
        ]);

        try
        {
            PauseMenuManager.ModifyUI(() =>
            {
                PauseMenuManager.RegisterModification(PathToHPSliderP1, submenuOpenHookP1);
                PauseMenuManager.RegisterModification(PathToHPSliderP1, appendChildP1Req);
                PauseMenuManager.RegisterModification(PathToHPSliderP2, submenuOpenHookP2);
                PauseMenuManager.RegisterModification(PathToHPSliderP2, appendChildP2Req);
            });
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in BlockRandomChange: {ex.Message}");
        }

        ModificationRequests.Add(submenuOpenHookP1);
        ModificationRequests.Add(appendChildP1Req);
        ModificationRequests.Add(submenuOpenHookP2);
        ModificationRequests.Add(appendChildP2Req);
    }

    public override void Randomize()
    {
        var tm = GameSingletonManager.TrainingManager!;

        if (EnabledP1)
            tm.ParamFunc.SetVitalPoint(0, Random.Shared.Next(_config.LowerBoundP1, _config.UpperBoundP1 + 1));

        if (EnabledP2)
            tm.ParamFunc.SetVitalPoint(1, Random.Shared.Next(_config.LowerBoundP2, _config.UpperBoundP2 + 1));
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(Config))]
    private partial class ConfigJsonContext : JsonSerializerContext;

    private class Config
    {
        public bool EnabledP1 { get; set; }
        public int LowerBoundP1 { get; set; }
        public int UpperBoundP1 { get; set; } = 100;


        public bool EnabledP2 { get; set; }
        public int LowerBoundP2 { get; set; }
        public int UpperBoundP2 { get; set; } = 100;
    }
}