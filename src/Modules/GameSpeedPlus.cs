using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

using REFrameworkNET;
using REFrameworkNET.Attributes;

using SF6_Training_Mode_Plus.Core;
using SF6_Training_Mode_Plus.Core.TrainingPauseMenu;

namespace SF6_Training_Mode_Plus.Modules;

public class GameSpeedPlus : ITrainingModePlusModule
{

    private readonly Stack<IDynamicUIModifier> _appliedModifiers = new();

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
        var newElement = MenuDataElementFactory.CreateTextElement("GameSpeedPlus: Adjust game speed in training mode.");

        // Create a new TrainingDataArrayModifier to add the new element to the menu
        var menuModifier = new TrainingDataArrayModifier(TrainingModePlus.TrainingManager._UIData._MenuData[1]._ChildData[3], [newElement]);

        _appliedModifiers.Push(menuModifier);
    }

    public void Unload()
    {
        while (_appliedModifiers.Count > 0)
        {
            var uiModifier = _appliedModifiers.Pop();
            uiModifier.Restore();
        }
    }
}
