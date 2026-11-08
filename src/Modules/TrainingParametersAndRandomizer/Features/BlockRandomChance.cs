using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

public class RandomBlockChance : TrainingFeatureBase<RandomBlockChance>
{
    private static readonly List<int> PathToBlockRow = [2, 0, 0, 2];

    private static readonly Guid RANDOM_BLOCK_CHANCE_SPINNER = Guid.NewGuid();

    private RandomBlockChance()
    {
    }

    private static ushort CurrentRandomProbability
    {
        get => GameSingletonManager.TrainingManager!._tData.GuardSetting.DummyData.GuardWeight;
        set => GameSingletonManager.TrainingManager!._tData.GuardSetting.DummyData.GuardWeight = value;
    }

    public override void Init()
    {
        using var _ = LocalizationManager.UseFileScope("TrainingModePlus");
        var changeToGuidTypeReq = new FieldOverrideRequest
        {
            Type = app.training.ItemType.SPIN_ITEM_GUIDE,
            GuidIcon = app.InputAssign.Digital.Id.UITrainingOptionY
        };

        var newGuideMessage = CustomMessage.FromKey("RandomBlockPercentageGuide");

        var changeParentsGuideReq = new FieldOverrideRequest
        {
            GuideMessage = newGuideMessage
        };

        var submenuSliderNode = new UICustomElementRequestNode(RANDOM_BLOCK_CHANCE_SPINNER,
            new SubMenuSpinnerInitializer(
                CustomMessage.FromKey("RandomBlockPercentageTitle"),
                newGuideMessage,
                (0, 100)));
        var functionHook = new FunctionHookRequest((_, _, index) =>
        {
            CurrentRandomProbability = (ushort)index;
            return false;
        });
        var spinnerHook = new SubMenuSpinnerHookRequest(
            () => CurrentRandomProbability,
            val => ($"{val}%", val != 50));
        submenuSliderNode.AddHook(functionHook);
        submenuSliderNode.AddHook(spinnerHook);

        var appendReq = new PauseMenuAppendElementsRequest([(0, submenuSliderNode)]);

        var optionYHook = new OptionYHookRequest((baseParam, viewData, _) =>
        {
            // elaborate check if the option is the correct one (more generalized and ensures adding options and reordering won't break anything)
            var index = viewData.Index;
            var selectedChildFuncType = viewData.Data.ChildData[index].FuncType;
            if (selectedChildFuncType != app.training.TrainingFuncType.DUMMY_GUARD_4) return false;
            GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                baseParam);
            GameSingletonManager.TrainingManager?.RequestSE(
                app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                0);

            return false;
        });

        try
        {
            PauseMenuManager.RegisterModification([.. PathToBlockRow, 4], changeToGuidTypeReq);
            PauseMenuManager.RegisterModification([.. PathToBlockRow, 4], appendReq);
            PauseMenuManager.RegisterModification(PathToBlockRow, optionYHook);
            PauseMenuManager.RegisterModification(PathToBlockRow, changeParentsGuideReq);
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in BlockRandomChange: {ex.Message}");
        }

        ModificationRequests.Add(changeToGuidTypeReq);
        ModificationRequests.Add(appendReq);
        ModificationRequests.Add(optionYHook);
        ModificationRequests.Add(changeParentsGuideReq);
    }
}