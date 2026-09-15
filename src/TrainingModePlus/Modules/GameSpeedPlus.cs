// Core usings
using SF6_TMP.Core;
using SF6_TMP.Core.UI;
using SF6_TMP.Core.UI.TrainingPauseMenu;
using SF6_TMP.Core.UI.TrainingPauseMenu.CustomElements;
using SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;
using SF6_TMP.Core.UI.TrainingPauseMenu.DispatchRequests;
using SF6_TMP.Core.UI.TrainingPauseMenu.ModificationRequests;

namespace SF6_TMP.TrainingModePlus.Modules;

public class GameSpeedPlus : ITrainingModePlusModule
{

    public static GameSpeedPlus Instance { get; private set; } = new GameSpeedPlus();
    private GameSpeedPlus() { }

    private readonly List<IUIModificationRequest> _modificationRequests = [];

    private static readonly List<int> PathToGamespeedSpinbox = [1, 3];

    public void Init()
    {
        API.LogInfo("Initializing GameSpeedPlus module...");

        // reorder to: Pause, 50%, Standard
        PauseMenuManager.ReorderRequest reorderRequest = new([2, 1, 0]);

        List<(int, UICustomElementNode)> newElements = CreateGameSpeedElements();
        AppendElements appendRequest = new(newElements);

        InputGuideDispatcherRequest inputGuideRequest = new(SetCustomGuide);
        SpinBoxDispatcherRequest spinBoxRequest = new(GetCurrentGameSpeedFunctionName);
        OptionSelectDispatcherRequest optionSelectRequest = new(ResetToDefaultGameSpeed);

        try
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
        }
        catch (Exception ex)
        {
            API.LogError($"Error registering modification: {ex.Message}");
        }

    }

    public void Unload()
    {
        API.LogInfo("Unloading GameSpeedPlus module...");
        foreach (var request in _modificationRequests)
        {
            PauseMenuManager.UnregisterModification(request);
        }
    }

    /// <summary>
    /// Creates a list of new game speed elements to be added to the training pause menu. Each element is associated with a specific game speed and has its own message ID, function name, and index to attach to in the menu.
    /// </summary>
    /// <returns></returns>
    private static List<(int, UICustomElementNode)> CreateGameSpeedElements()
    {
        var newElements = new List<(int, UICustomElementNode)>();

        // Create the new game speed options
        var speedOptionData = new (app.training.GameSpeed speed, string messageID, string functionName, int attachToIndex)[]
        {
            (app.training.GameSpeed.SPEED_60, "60%", "ENV_GAME_SPEED_60%", 2),
            (app.training.GameSpeed.SPEED_70, "70%", "ENV_GAME_SPEED_70%", 2),
            (app.training.GameSpeed.SPEED_80, "80%", "ENV_GAME_SPEED_80%", 2),
            (app.training.GameSpeed.SPEED_90, "90%", "ENV_GAME_SPEED_90%", 2),
            (app.training.GameSpeed.SPEED_110, "110%", "ENV_GAME_SPEED_110%", 1),
            (app.training.GameSpeed.SPEED_120, "120%", "ENV_GAME_SPEED_120%", 1),
            (app.training.GameSpeed.SPEED_130, "130%", "ENV_GAME_SPEED_130%", 1),
            (app.training.GameSpeed.SPEED_140, "140%", "ENV_GAME_SPEED_140%", 1),
            (app.training.GameSpeed.SPEED_150, "150%", "ENV_GAME_SPEED_150%", 1),
        };

        foreach (var (speed, messageID, functionName, attachToIndex) in speedOptionData)
        {
            var message = new CustomMessage(messageID);
            var initializer = new SpinnerOptionTextInitializer(message);
            var newElement = new UICustomElementNode(functionName, initializer);

            // Register the dispatcher for this game speed option
            FunctionDispatcherRequest dispatcherRequest = new((_, _, _) => ChangeGameSpeed(speed));
            newElement.AddDispatcher(dispatcherRequest);

            // Add the new element to the list with its index
            newElements.Add((attachToIndex, newElement));
        }

        return newElements;
    }

    /// <summary>
    /// Captured function to change the game speed. This function accesses the TrainingManager singleton and modifies the game speed settings based on the provided speed index.
    /// </summary>
    /// <param name="speedIndex"></param>
    private static void ChangeGameSpeed(app.training.GameSpeed speedIndex)
    {
        if (GameSingletonRegistry.TrainingManager == null)
        {
            API.LogError("TrainingManager is null. Cannot initialize GameSpeedPlus module.");
            return;
        }

        // get nested members of the singleton for easier access
        var tfFuncs = GameSingletonRegistry.TrainingManager._tfFuncs as IObject;
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
                return;
            }
        }

        API.LogError("Failed to change game speed. Could not access the necessary fields.");
    }

    // delegate for the custom guide dispatcher
    private static void SetCustomGuide(ref REFrameworkNET.Collections.IList<app.InputGuideData> outInputGuideDataList, ref REFrameworkNET.Collections.IList<string> outStringList)
    {
        var customGuideDataMo = app.InputGuideData.REFType.CreateInstance(0);
        customGuideDataMo.Globalize();

        var customGuideData = customGuideDataMo.As<app.InputGuideData>();
        customGuideData.Type = app.InputGuideDataType.DigitalConfig;
        customGuideData.DigitalConfigId = app.InputAssign.Digital.ConfigId.UI_BACK;
        var newMessage = new CustomMessage("Restore to Standard");
        MessageManager.SetGuid(customGuideDataMo, "<MessageId>k__BackingField", newMessage.Id);

        outInputGuideDataList.Add(customGuideData);
    }

    private static int GetCurrentGameSpeedFunctionName(app.training.UIFlowTrainingMenu.Param _)
    {
        int defaultGameSpeed = (int)app.training.TrainingFuncType.ENV_GAME_SPEED_0;

        if (GameSingletonRegistry.TrainingManager == null)
        {
            API.LogError("TrainingManager is null. Cannot retrieve current game speed.");
            return defaultGameSpeed;
        }

        // get nested members of the singleton for easier access
        var tfFuncs = GameSingletonRegistry.TrainingManager._tfFuncs as IObject;
        var entriesArray = (tfFuncs?.GetField("_entries") as ManagedObject)?.As<_System.Array>();

        if (entriesArray == null || entriesArray.Length <= 10)
        {
            API.LogError("Failed to get current game speed index. Could not access the necessary fields.");
            return defaultGameSpeed;
        }

        var entry10 = entriesArray.GetValue(10) as ManagedObject;
        var tf_OS = (entry10 as IObject)?.GetField("value") as ManagedObject;
        var gameData = ((tf_OS as IObject)?.GetField("_GameData") as ManagedObject)?.As<app.training.tf_OtherSetting.GameLocalData>();

        if (gameData == null)
        {
            API.LogError("Failed to get current game speed index. Could not access the necessary fields.");
            return defaultGameSpeed;
        }

        if (gameData.IsMenuPause)
        {
            return (int)app.training.TrainingFuncType.ENV_GAME_SPEED_2;
        }

        if (GameSingletonRegistry.TrainingManager?.TData.OtherSetting.Is_Speed_Setting == true)
        {
            var currentSpeed = GameSingletonRegistry.TrainingManager.TData.OtherSetting.OS_Game_Speed;

            string resultingFunctionName;

            switch (currentSpeed)
            {
                case app.training.GameSpeed.SPEED_50:
                    return (int)app.training.TrainingFuncType.ENV_GAME_SPEED_1;
                case app.training.GameSpeed.SPEED_60:
                    resultingFunctionName = "ENV_GAME_SPEED_60%";
                    break;
                case app.training.GameSpeed.SPEED_70:

                    resultingFunctionName = "ENV_GAME_SPEED_70%";
                    break;
                case app.training.GameSpeed.SPEED_80:
                    resultingFunctionName = "ENV_GAME_SPEED_80%";
                    break;
                case app.training.GameSpeed.SPEED_90:
                    resultingFunctionName = "ENV_GAME_SPEED_90%";
                    break;
                case app.training.GameSpeed.SPEED_110:
                    resultingFunctionName = "ENV_GAME_SPEED_110%";
                    break;
                case app.training.GameSpeed.SPEED_120:
                    resultingFunctionName = "ENV_GAME_SPEED_120%";
                    break;
                case app.training.GameSpeed.SPEED_130:
                    resultingFunctionName = "ENV_GAME_SPEED_130%";
                    break;
                case app.training.GameSpeed.SPEED_140:
                    resultingFunctionName = "ENV_GAME_SPEED_140%";
                    break;
                case app.training.GameSpeed.SPEED_150:
                    resultingFunctionName = "ENV_GAME_SPEED_150%";
                    break;
                default:
                    API.LogWarning($"Unknown game speed: {currentSpeed}. Defaulting to index 0.");
                    return defaultGameSpeed;
            }

            if (FunctionTypeRegistry.TryGetFunctionType(resultingFunctionName, out int funcType))
            {
                return funcType;
            }
            else
            {
                API.LogWarning($"Function name '{resultingFunctionName}' not found in FunctionTypeRegistry. Defaulting to index 0.");
                return defaultGameSpeed;
            }
        }

        return defaultGameSpeed;
    }

    private static void ResetToDefaultGameSpeed(app.training.BaseParam baseParam, app.training.UIFlowTrainingMenu.Param.ViewData viewData, int rowIndex)
    {
        // we first want to find the index of the default game speed
        var childArray = viewData.Data.ChildData;
        int index = -1;
        for (int i = 0; i < childArray.Count; i++)
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
        {
            // already at default, no need to change
            return;
        }

        uipart.Num = index;
        var scrollList = ManagedProxy<app.UIPartsScrollList>.Create(uipart.GetChild(0));
        scrollList.SetSelectedIndex(index, false);
        uiFlowParam.UpdateSpinBox(rowIndex, true, true);
        uiFlowParam.OnUpdateSpin();
    }

}