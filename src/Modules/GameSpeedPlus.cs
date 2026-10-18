// Core usings

using SF6_Plugin_Core.Memory;
using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;

namespace SF6_TMP.Modules;

public class GameSpeedPlus : ITrainingModePlusModule
{
    private static readonly List<int> PathToGamespeedSpinbox = [1, 3];

    private static readonly Guid ENV_GAMESPEED_60 = Guid.NewGuid();
    private static readonly Guid ENV_GAMESPEED_70 = Guid.NewGuid();
    private static readonly Guid ENV_GAMESPEED_80 = Guid.NewGuid();
    private static readonly Guid ENV_GAMESPEED_90 = Guid.NewGuid();
    private static readonly Guid ENV_GAMESPEED_110 = Guid.NewGuid();
    private static readonly Guid ENV_GAMESPEED_120 = Guid.NewGuid();
    private static readonly Guid ENV_GAMESPEED_130 = Guid.NewGuid();
    private static readonly Guid ENV_GAMESPEED_140 = Guid.NewGuid();
    private static readonly Guid ENV_GAMESPEED_150 = Guid.NewGuid();

    private static ManagedObject? _customGuideDataMo;
    private static CustomMessage? _newMessage;

    private readonly List<IUIModificationRequest> _modificationRequests = [];

    private GameSpeedPlus()
    {
    }

    public static GameSpeedPlus Instance { get; private set; } = new();

    public void Init()
    {
        API.LogInfo("Initializing GameSpeedPlus module...");

        // reorder to: Pause, 50%, Standard
        ReorderRequest reorderRequest = new([2, 1, 0]);

        var newElements = CreateGameSpeedElements();
        PauseMenuAppendElementsRequest appendRequest = new(newElements);

        InputGuideHookRequest inputGuideRequest = new(SetCustomGuide);
        SpinBoxHookRequest spinBoxRequest = new(GetCurrentGameSpeedFunctionName);
        OptionSelectHookRequest optionSelectRequest = new(ResetToDefaultGameSpeed);

        try
        {
            PauseMenuManager.ModifyUI(() =>
            {
                PauseMenuManager.RegisterModification(PathToGamespeedSpinbox, reorderRequest);
                _modificationRequests.Add(reorderRequest);

                PauseMenuManager.RegisterModification(PathToGamespeedSpinbox, appendRequest);
                _modificationRequests.Add(appendRequest);

                PauseMenuManager.RegisterModification(PathToGamespeedSpinbox, inputGuideRequest);
                _modificationRequests.Add(inputGuideRequest);

                PauseMenuManager.RegisterModification(PathToGamespeedSpinbox, spinBoxRequest);
                _modificationRequests.Add(spinBoxRequest);

                PauseMenuManager.RegisterModification(PathToGamespeedSpinbox, optionSelectRequest);
                _modificationRequests.Add(optionSelectRequest);
            });
        }
        catch (Exception ex)
        {
            API.LogError($"Error registering modification: {ex.Message}");
        }

        // input guide initialization to cache the elements instead of recreating them each time the input guide is queried
        _newMessage = new CustomMessage("Restore to Standard");

        _customGuideDataMo = app.InputGuideData.REFType.CreateInstance(0);
        _customGuideDataMo.Globalize();
        _customGuideDataMo.SetGuidField("<MessageId>k__BackingField", _newMessage.Id);
    }

    public void Unload()
    {
        API.LogInfo("Unloading GameSpeedPlus module...");
        if (_modificationRequests.Count > 0)
        {
            PauseMenuManager.ModifyUI(() =>
            {
                foreach (var request in _modificationRequests) PauseMenuManager.UnregisterModification(request);
            });

            _modificationRequests.Clear();
        }

        _customGuideDataMo?.ReleaseIfGlobalized();
        _customGuideDataMo = null;

        _newMessage?.Dispose();
        _newMessage = null;
    }

    /// <summary>
    ///     Creates a list of new game speed elements to be added to the training pause menu. Each element is associated with a
    ///     specific game speed and has its own message ID, function name, and index to attach to in the menu.
    /// </summary>
    /// <returns></returns>
    private static List<(int, UICustomElementRequestNode)> CreateGameSpeedElements()
    {
        var newElements = new List<(int, UICustomElementRequestNode)>();

        // Create the new game speed options
        var speedOptionData = new (app.training.GameSpeed speed, string messageID, Guid functionId, int attachToIndex)[]
        {
            (app.training.GameSpeed.SPEED_60, "60%", ENV_GAMESPEED_60, 2),
            (app.training.GameSpeed.SPEED_70, "70%", ENV_GAMESPEED_70, 2),
            (app.training.GameSpeed.SPEED_80, "80%", ENV_GAMESPEED_80, 2),
            (app.training.GameSpeed.SPEED_90, "90%", ENV_GAMESPEED_90, 2),
            (app.training.GameSpeed.SPEED_110, "110%", ENV_GAMESPEED_110, 1),
            (app.training.GameSpeed.SPEED_120, "120%", ENV_GAMESPEED_120, 1),
            (app.training.GameSpeed.SPEED_130, "130%", ENV_GAMESPEED_130, 1),
            (app.training.GameSpeed.SPEED_140, "140%", ENV_GAMESPEED_140, 1),
            (app.training.GameSpeed.SPEED_150, "150%", ENV_GAMESPEED_150, 1)
        };

        foreach (var (speed, messageID, functionName, attachToIndex) in speedOptionData)
        {
            var message = new CustomMessage(messageID);
            var initializer = new SpinnerOptionTextInitializer(message);
            var newElement = new UICustomElementRequestNode(functionName, initializer);

            // Register the hook for this game speed option
            FunctionHookRequest hookRequest = new((_, _, _) => ChangeGameSpeed(speed));
            newElement.AddHook(hookRequest);

            // Add the new element to the list with its index
            newElements.Add((attachToIndex, newElement));
        }

        return newElements;
    }

    /// <summary>
    ///     Captured function to change the game speed. This function accesses the TrainingManager singleton and modifies the
    ///     game speed settings based on the provided speed index.
    /// </summary>
    /// <param name="speedIndex"></param>
    private static bool ChangeGameSpeed(app.training.GameSpeed speedIndex)
    {
        if (GameSingletonManager.TrainingManager == null)
        {
            API.LogError("TrainingManager is null. Cannot initialize GameSpeedPlus module.");
            return false;
        }

        // get nested members of the singleton for easier access
        var tfFuncs = GameSingletonManager.TrainingManager._tfFuncs as IObject;
        var entriesArray = (tfFuncs?.GetField("_entries") as ManagedObject)?.As<_System.Array>();

        if (entriesArray != null && entriesArray.Length > 10)
        {
            var entry10 = entriesArray.GetValue(10) as ManagedObject;
            var tf_OS = (entry10 as IObject)?.GetField("value") as ManagedObject;
            var funcList = (tf_OS as IObject)?.GetField("FuncList") as ManagedObject;

            // 2. Call the methods safely
            if (funcList is IObject funcListObj)
            {
                funcListObj.Call("SetActiveGameSpeed(System.Boolean)", true);
                funcListObj.Call("SetMenuPause(System.Boolean)", false);

                // Apply your custom speed here
                funcListObj.Call("SetGameSpeed(app.training.GameSpeed)", (int)speedIndex);
                return false;
            }
        }

        API.LogError("Failed to change game speed. Could not access the necessary fields.");
        return false;
    }

    // delegate for the custom guide hook
    private static void SetCustomGuide(ref REFrameworkNET.Collections.IList<app.InputGuideData> outInputGuideDataList,
        ref REFrameworkNET.Collections.IList<string> outStringList)
    {
        if (_customGuideDataMo == null)
        {
            API.LogError("Custom guide data ManagedObject is null. Cannot set custom guide.");
            return;
        }

        var customGuideData = _customGuideDataMo.As<app.InputGuideData>();
        customGuideData.Type = app.InputGuideDataType.DigitalConfig;
        customGuideData.DigitalConfigId = app.InputAssign.Digital.ConfigId.UI_BACK;

        outInputGuideDataList.Add(customGuideData);
    }

    private static TrainingFuncOrGuid GetCurrentGameSpeedFunctionName(app.training.UIFlowTrainingMenu.Param _)
    {
        var defaultGameSpeed = app.training.TrainingFuncType.ENV_GAME_SPEED_0;

        if (GameSingletonManager.TrainingManager == null)
        {
            API.LogError("TrainingManager is null. Cannot retrieve current game speed.");
            return defaultGameSpeed;
        }

        // get nested members of the singleton for easier access
        var tfFuncs = GameSingletonManager.TrainingManager._tfFuncs as IObject;
        var entriesArray = (tfFuncs?.GetField("_entries") as ManagedObject)?.As<_System.Array>();

        if (entriesArray == null || entriesArray.Length <= 10)
        {
            API.LogError("Failed to get current game speed index. Could not access the necessary fields.");
            return defaultGameSpeed;
        }

        var entry10 = entriesArray.GetValue(10) as ManagedObject;
        var tf_OS = (entry10 as IObject)?.GetField("value") as ManagedObject;
        var gameData = ((tf_OS as IObject)?.GetField("_GameData") as ManagedObject)
            ?.As<app.training.tf_OtherSetting.GameLocalData>();

        if (gameData == null)
        {
            API.LogError("Failed to get current game speed index. Could not access the necessary fields.");
            return defaultGameSpeed;
        }

        if (gameData.IsMenuPause) return app.training.TrainingFuncType.ENV_GAME_SPEED_2;

        if (GameSingletonManager.TrainingManager?.TData.OtherSetting.Is_Speed_Setting == true)
        {
            var currentSpeed = GameSingletonManager.TrainingManager.TData.OtherSetting.OS_Game_Speed;

            switch (currentSpeed)
            {
                case app.training.GameSpeed.SPEED_50:
                    return app.training.TrainingFuncType.ENV_GAME_SPEED_1;
                case app.training.GameSpeed.SPEED_60:
                    return ENV_GAMESPEED_60;
                case app.training.GameSpeed.SPEED_70:
                    return ENV_GAMESPEED_70;
                case app.training.GameSpeed.SPEED_80:
                    return ENV_GAMESPEED_80;
                case app.training.GameSpeed.SPEED_90:
                    return ENV_GAMESPEED_90;
                case app.training.GameSpeed.SPEED_110:
                    return ENV_GAMESPEED_110;
                case app.training.GameSpeed.SPEED_120:
                    return ENV_GAMESPEED_120;
                case app.training.GameSpeed.SPEED_130:
                    return ENV_GAMESPEED_130;
                case app.training.GameSpeed.SPEED_140:
                    return ENV_GAMESPEED_140;
                case app.training.GameSpeed.SPEED_150:
                    return ENV_GAMESPEED_150;
                default:
                    API.LogWarning($"Unknown game speed: {currentSpeed}. Defaulting to index 0.");
                    return defaultGameSpeed;
            }
        }

        return defaultGameSpeed;
    }

    private static bool ResetToDefaultGameSpeed(app.training.BaseParam baseParam,
        app.training.UIFlowTrainingMenu.Param.ViewData viewData, int rowIndex)
    {
        // we first want to find the index of the default game speed
        var childArray = viewData.Data.ChildData;
        var index = -1;
        for (var i = 0; i < childArray.Count; i++)
        {
            var childData = childArray[i];
            if (childData.FuncType == app.training.TrainingFuncType.ENV_GAME_SPEED_0)
            {
                index = i;
                break;
            }
        }

        var uiFlowParam = ManagedProxy<app.training.UIFlowTrainingMenu.Param>.Create(baseParam);
        var uipart = ManagedProxy<app.UIPartsSpin>.Create(uiFlowParam.SecondaryList.GetFocusItem());

        if (uipart.Num == index)
            // already at default, no need to change
            return false;

        uipart.Num = index;
        var scrollList = ManagedProxy<app.UIPartsScrollList>.Create(uipart.GetChild(0));
        scrollList.SetSelectedIndex(index, false);
        uiFlowParam.UpdateSpinBox(rowIndex, true, true);
        uiFlowParam.OnUpdateSpin();

        return false;
    }
}