using System;
using System.Collections.Generic;

using REFrameworkNET;
using REFrameworkNET.Attributes;

namespace SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Dispatchers;

public static class SpinBoxDispatcher
{
    // needs to do some more complex things since the initspinbox function doesn't explicitly indicate which spinbox it is, so I basically have to keep track of which "tab" and "index" the spinbox is in.
    public struct SpinInfoLocation
    {
        public int TabIndex;
        public int SpinBoxIndex;

        public SpinInfoLocation(app.training.TrainingFuncType nativeTabFuncType, int spinBoxIndex)
        {
            TabIndex = (int)nativeTabFuncType;
            SpinBoxIndex = spinBoxIndex;
        }

        public SpinInfoLocation(int customTabFuncType, int spinBoxIndex)
        {
            TabIndex = customTabFuncType;
            SpinBoxIndex = spinBoxIndex;
        }
    }

    // delegate for the custom initialization logic
    public delegate int SpinBoxInitDelegate();

    // since the initialization of the spinbox is determined by the game's logic, it doesn't match with our modified/added spinboxes, so we need to dispatch custom initialization logic
    private static readonly Dictionary<SpinInfoLocation, SpinBoxInitDelegate> CustomInitializations = [];

    public static void RegisterCustomSpinBoxInitialization(SpinInfoLocation location, SpinBoxInitDelegate initDelegate)
    {
        if (!CustomInitializations.TryAdd(location, initDelegate))
        {
            API.LogWarning($"Custom initialization for tab {location.TabIndex}, index {location.SpinBoxIndex} is already registered.");
        }
    }


    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSpinBox(System.Int32)", MethodHookType.Pre)]
    private static PreHookResult OnInitSpinBoxPre(Span<ulong> args)
    {
        var currentObject = ManagedObject.ToManagedObject(args[1])?.As<app.training.UIFlowTrainingMenu.Param>();

        if (currentObject == null)
        {
            API.LogWarning("Failed to retrieve current UIFlowTrainingMenu.Param object.");
            return PreHookResult.Continue;
        }

        var currentTMD = currentObject.CurrentParentData;
        if (currentTMD == null)
        {
            API.LogWarning("Failed to retrieve current UIFlowTrainingMenu.Param object.");
            return PreHookResult.Continue;
        }

        // functabtype basically works as a tab index
        int currentTabFuncType = (int)currentTMD.FuncType;

        int currentIndex = (int)args[2];

        if (CustomInitializations.TryGetValue(new(currentTabFuncType, currentIndex), out SpinBoxInitDelegate? initDelegate))
        {
            // Invoke the custom initialization logic
            currentObject.ViewDataList[currentIndex].Index = initDelegate.Invoke();
        }

        return PreHookResult.Continue;
    }

}