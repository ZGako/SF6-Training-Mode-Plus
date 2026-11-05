using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

public class HealthBar : IRandomizableFeature
{
    private static readonly List<int> PathToHPSliderP1 = [5, 0];
    private static readonly List<int> PathToHPSliderP2 = [5, 1];

    private static readonly Guid HP_P1_RANDOMIZER_ENABLE_SPINNER = Guid.NewGuid();
    private static readonly Guid HP_P1_RANDOMIZER_UPPER_BOUND = Guid.NewGuid();
    private static readonly Guid HP_P1_RANDOMIZER_LOWER_BOUND = Guid.NewGuid();

    private static int _lowerBound;
    private static int _upperBound = 100;
    private static bool _enabledP1;


    private readonly List<IUIModificationRequest> _modificationRequests = [];

    private HealthBar()
    {
    }

    public static HealthBar Instance { get; private set; } = new();

    public void Init()
    {
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
            () => _enabledP1 ? 1 : 0,
            val => val == 1 ? ("Enabled", true) : ("Disabled", false)));

        var upperBoundSpinnerP1 = new UICustomElementRequestNode(HP_P1_RANDOMIZER_UPPER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Upper Bound Randomization P1 Health"),
                new CustomMessage("set the upper bound"), (0, 100), false));

        upperBoundSpinnerP1.AddHook(new SubMenuSpinnerHookRequest(
            () => _upperBound,
            val => ($"{val}%", false),
            () => (_lowerBound, 100)));

        upperBoundSpinnerP1.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            _upperBound = index;
            return false;
        }));

        var lowerBoundSpinnerP1 = new UICustomElementRequestNode(HP_P1_RANDOMIZER_LOWER_BOUND,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Lower Bound Randomization P1 Health"),
                new CustomMessage("set the lower bound"), (0, 100), false));

        lowerBoundSpinnerP1.AddHook(new SubMenuSpinnerHookRequest(
            () => _lowerBound,
            val => ($"{val}%", false),
            () => (0, _upperBound)));

        lowerBoundSpinnerP1.AddHook(new FunctionHookRequest((_, _, index) =>
        {
            _lowerBound = index;
            return false;
        }));

        enableSpinnerP1.AddHook(new FunctionHookRequest((baseParam, _, val) =>
        {
            switch (val)
            {
                case 0:
                    _enabledP1 = false;
                    upperBoundSpinnerP1.GetBuiltElement().IsEnabled = false;
                    lowerBoundSpinnerP1.GetBuiltElement().IsEnabled = false;
                    break;
                case 1:
                    _enabledP1 = true;
                    upperBoundSpinnerP1.GetBuiltElement().IsEnabled = true;
                    lowerBoundSpinnerP1.GetBuiltElement().IsEnabled = true;
                    break;
            }

            (baseParam as IObject).Call("Initialize()");

            return false;
        }));

        var appendChildP1Req = new PauseMenuAppendElementsRequest([
            (0, enableSpinnerP1), (0, upperBoundSpinnerP1),
            (0, lowerBoundSpinnerP1)
        ]);

        try
        {
            PauseMenuManager.ModifyUI(() =>
            {
                PauseMenuManager.RegisterModification(PathToHPSliderP1, submenuOpenHookP1);
                PauseMenuManager.RegisterModification(PathToHPSliderP1, appendChildP1Req);
            });
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in BlockRandomChange: {ex.Message}");
        }

        _modificationRequests.Add(submenuOpenHookP1);
        _modificationRequests.Add(appendChildP1Req);
    }

    public void Unload()
    {
        if (_modificationRequests.Count > 0)
        {
            PauseMenuManager.ModifyUI(() =>
            {
                foreach (var request in _modificationRequests) PauseMenuManager.UnregisterModification(request);
            });

            _modificationRequests.Clear();
        }
    }

    public bool IsRandomizedEnabled { get; private set; }

    public void Randomize()
    {
        throw new NotImplementedException();
    }
}