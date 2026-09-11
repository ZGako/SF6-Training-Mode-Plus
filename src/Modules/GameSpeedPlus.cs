using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Collections.Generic;

using REFrameworkNET;
using REFrameworkNET.Attributes;

using SF6_Training_Mode_Plus.Core;
using SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu;
using SF6_Training_Mode_Plus.Core.UI;
using SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.ElementFactories;
using SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Modifiers;
using static SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Dispatchers.SpinBoxDispatcher;
using app.battle.ai.learning;
using static SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Dispatchers.TrainingFunctionDispatcher;
using app;

namespace SF6_Training_Mode_Plus.Modules;

public class GameSpeedPlus : ITrainingModePlusModule
{
    private readonly Stack<IUIDynamicModifier> _appliedModifiers = new();

    // convenience enum to be able to change permutations in a readable way
    private enum GameSpeed
    {
        SPEED_50,
        SPEED_60,
        SPEED_70,
        SPEED_80,
        SPEED_90,
        SPEED_100,
        SPEED_110,
        SPEED_120,
        SPEED_130,
        SPEED_140,
        SPEED_150,
        PAUSE
    }

    // change mapping here to change the order of the elements in the spinbox
    private static readonly GameSpeed[] GameSpeedToIndex = [
        GameSpeed.PAUSE,
        GameSpeed.SPEED_50,
        GameSpeed.SPEED_60,
        GameSpeed.SPEED_70,
        GameSpeed.SPEED_80,
        GameSpeed.SPEED_90,
        GameSpeed.SPEED_100,
        GameSpeed.SPEED_110,
        GameSpeed.SPEED_120,
        GameSpeed.SPEED_130,
        GameSpeed.SPEED_140,
        GameSpeed.SPEED_150,
    ];

    private static readonly GameSpeed[] OriginalGameSpeedOrder = [
        GameSpeed.SPEED_100,
        GameSpeed.SPEED_50,
        GameSpeed.PAUSE,
        GameSpeed.SPEED_60,
        GameSpeed.SPEED_70,
        GameSpeed.SPEED_80,
        GameSpeed.SPEED_90,
        GameSpeed.SPEED_110,
        GameSpeed.SPEED_120,
        GameSpeed.SPEED_130,
        GameSpeed.SPEED_140,
        GameSpeed.SPEED_150
    ];

    public void Init()
    {
        // Initialize the GameSpeedPlus module
        API.LogInfo("Initializing GameSpeedPlus module...");

        // Null stuff guard (annoying to type ?)
        if (TrainingModePlus.TrainingManager == null)
        {
            API.LogError("TrainingManager is null. Cannot initialize GameSpeedPlus module.");
            return;
        }

        // Add new UI element to the training pause menu
        const int spinnerIndex = 3;

        var orderArray = GameSpeedToIndex.Select((_, index) => Array.IndexOf(GameSpeedToIndex, OriginalGameSpeedOrder[index])).ToArray();

        // Create a new TrainingDataArrayModifier to add the new element to the menu
        var menuModifier = new SpinBoxModifier(TrainingModePlus.TrainingManager._UIData._MenuData[1]._ChildData[spinnerIndex],
                                                CreateGameSpeedElements(),
                                                orderArray,
                                                new(app.training.TrainingFuncType.ENVIRONMENT, spinnerIndex),
                                                GetCurrentGameSpeedIndex);

        RegisterCustomOptionSelectFunction(app.training.TrainingFuncType.ENV_GAME_SPEED, ResetToDefaultGameSpeed);

        _appliedModifiers.Push(menuModifier);

        API.LogInfo("GameSpeedPlus module initialized successfully.");
    }

    public void Unload()
    {
        while (_appliedModifiers.Count > 0)
        {
            var uiModifier = _appliedModifiers.Pop();
            uiModifier.Restore();
        }
    }

    private static void ChangeGameSpeed(app.training.GameSpeed speedIndex)
    {
        if (TrainingModePlus.TrainingManager == null)
        {
            API.LogError("TrainingManager is null. Cannot initialize GameSpeedPlus module.");
            return;
        }

        // get nested members of the singleton for easier access
        var tfFuncs = TrainingModePlus.TrainingManager._tfFuncs as IObject;
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


    private static int GetCurrentGameSpeedIndex()
    {
        if (TrainingModePlus.TrainingManager == null)
        {
            API.LogError("TrainingManager is null. Cannot initialize GameSpeedPlus module.");
            return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_100);
        }

        // get nested members of the singleton for easier access
        var tfFuncs = TrainingModePlus.TrainingManager._tfFuncs as IObject;
        var entriesArray = (tfFuncs?.GetField("_entries") as ManagedObject)?.As<_System.Array>();

        if (entriesArray == null || entriesArray.Length <= 10)
        {
            API.LogError("Failed to get current game speed index. Could not access the necessary fields.");
            return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_100);
        }

        var entry10 = entriesArray.GetValue(10) as ManagedObject;
        var tf_OS = (entry10 as IObject)?.GetField("value") as ManagedObject;
        var gameData = ((tf_OS as IObject)?.GetField("_GameData") as ManagedObject)?.As<app.training.tf_OtherSetting.GameLocalData>();

        if (gameData == null)
        {
            API.LogError("Failed to get current game speed index. Could not access the necessary fields.");
            return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_100);
        }

        if (gameData.IsMenuPause)
        {
            return Array.IndexOf(GameSpeedToIndex, GameSpeed.PAUSE);
        }

        if (TrainingModePlus.TrainingManager.TData.OtherSetting.Is_Speed_Setting)
        {
            var currentSpeed = TrainingModePlus.TrainingManager.TData.OtherSetting.OS_Game_Speed;
            switch (currentSpeed)
            {
                case app.training.GameSpeed.SPEED_50:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_50);
                case app.training.GameSpeed.SPEED_60:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_60);
                case app.training.GameSpeed.SPEED_70:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_70);
                case app.training.GameSpeed.SPEED_80:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_80);
                case app.training.GameSpeed.SPEED_90:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_90);
                case app.training.GameSpeed.SPEED_110:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_110);
                case app.training.GameSpeed.SPEED_120:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_120);
                case app.training.GameSpeed.SPEED_130:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_130);
                case app.training.GameSpeed.SPEED_140:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_140);
                case app.training.GameSpeed.SPEED_150:
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_150);
                default:
                    API.LogWarning($"Unknown game speed: {currentSpeed}. Defaulting to index 0.");
                    return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_100);
            }
        }

        return Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_100);
    }

    private static void ResetToDefaultGameSpeed(app.training.BaseParam baseParam, app.training.UIFlowTrainingMenu.Param.ViewData viewData, int rowIndex)
    {
        var uiFlowParam = ManagedProxy<app.training.UIFlowTrainingMenu.Param>.Create(baseParam);
        var uipart = ManagedProxy<UIPartsSpin>.Create(uiFlowParam.SecondaryList.GetFocusItem());
        uipart.Num = Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_100);
        var scrollList = ManagedProxy<UIPartsScrollList>.Create(uipart.GetChild(0));
        scrollList.SetSelectedIndex(Array.IndexOf(GameSpeedToIndex, GameSpeed.SPEED_100), false);
        uiFlowParam.UpdateSpinBox(rowIndex, true, true);
        uiFlowParam.OnUpdateSpin();
    }

    private static List<app.training.TrainingMenuData> CreateGameSpeedElements()
    {
        var elements = new List<app.training.TrainingMenuData>
        {
            // Create a new TrainingMenuData element for the game speed adjustment
            TextElementFactory.Create("60%", "GAMESPEED_60", (baseParam, viewData, rowIndex) => ChangeGameSpeed(app.training.GameSpeed.SPEED_60)),
            TextElementFactory.Create("70%", "GAMESPEED_70", (baseParam, viewData, rowIndex) => ChangeGameSpeed(app.training.GameSpeed.SPEED_70)),
            TextElementFactory.Create("80%", "GAMESPEED_80", (baseParam, viewData, rowIndex) => ChangeGameSpeed(app.training.GameSpeed.SPEED_80)),
            TextElementFactory.Create("90%", "GAMESPEED_90", (baseParam, viewData, rowIndex) => ChangeGameSpeed(app.training.GameSpeed.SPEED_90)),
            TextElementFactory.Create("110%", "GAMESPEED_110", (baseParam, viewData, rowIndex) => ChangeGameSpeed(app.training.GameSpeed.SPEED_110)),
            TextElementFactory.Create("120%", "GAMESPEED_120", (baseParam, viewData, rowIndex) => ChangeGameSpeed(app.training.GameSpeed.SPEED_120)),
            TextElementFactory.Create("130%", "GAMESPEED_130", (baseParam, viewData, rowIndex) => ChangeGameSpeed(app.training.GameSpeed.SPEED_130)),
            TextElementFactory.Create("140%", "GAMESPEED_140", (baseParam, viewData, rowIndex) => ChangeGameSpeed(app.training.GameSpeed.SPEED_140)),
            TextElementFactory.Create("150%", "GAMESPEED_150", (baseParam, viewData, rowIndex) => ChangeGameSpeed(app.training.GameSpeed.SPEED_150))
        };

        return elements;
    }
}
