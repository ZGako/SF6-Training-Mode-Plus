

namespace SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

// TODO change this dispatcher to consider the new implementation strategy
// FIXME find a way to track the element indices that are being used (this is probably gonna require a modifier of some kind)
// so actually this can get the FuncType through the viewlist[index].Data.FuncType, so I don't even need the location.
public static class SpinBoxDispatcher
{

    /// <summary>
    /// Delegate type for custom spinbox initialization logic. This should return the FunctionName of the selected option for the spinbox.
    /// </summary>
    /// <param name="param"></param>
    /// <returns></returns>
    public delegate int SpinBoxInitDelegate(app.training.UIFlowTrainingMenu.Param param);

    // since the initialization of the spinbox is determined by the game's logic, it doesn't match with our modified/added spinboxes, so we need to dispatch custom initialization logic
    private static readonly Dictionary<string, SpinBoxInitDelegate> CustomInitializations = [];

    public static void RegisterCustomSpinBoxInitialization(string name, SpinBoxInitDelegate initDelegate)
    {
        if (FunctionTypeRegistry.TryGetFunctionType(name, out int _))
        {
            if (!CustomInitializations.TryAdd(name, initDelegate))
            {
                API.LogWarning($"Custom initialization for '{name}' is already registered.");
            }
        }
        else
        {
            API.LogError($"Custom initialization '{name}' is not registered. Call RegisterNewFunctionType first.");
        }
    }

    public static void UnregisterCustomSpinBoxInitialization(string name)
    {
        if (!CustomInitializations.Remove(name))
        {
            API.LogWarning($"Custom initialization for '{name}' was not registered.");
        }
    }

    // FIXME eventually I'll have to relook at the whole caller function and indices thing which isn't very clear
    // but for now it's fine
    [MethodHook(typeof(app.training.UIFlowTrainingMenu.Param), "InitSpinBox(System.Int32)", MethodHookType.Pre)]
    private static PreHookResult OnInitSpinBoxPre(Span<ulong> args)
    {
        var currentObject = ManagedObject.ToManagedObject(args[1])?.As<app.training.UIFlowTrainingMenu.Param>();

        if (currentObject == null)
        {
            API.LogWarning("Failed to retrieve current UIFlowTrainingMenu.Param object.");
            return PreHookResult.Continue;
        }

        int currentIndex = (int)args[2];

        // get the current spinbox
        if (currentIndex < 0 || currentIndex >= currentObject.ViewDataList.Count)
        {
            return PreHookResult.Continue;
        }

        var currentSpinBox = currentObject.ViewDataList[currentIndex].Data;

        if (!FunctionTypeRegistry.TryGetFunctionName((int)currentSpinBox.FuncType, out string? functionName))
        {
            return PreHookResult.Continue;
        }

        if (CustomInitializations.TryGetValue(functionName!, out SpinBoxInitDelegate? initDelegate))
        {
            // Invoke the custom initialization logic
            int selectedFuncType = initDelegate.Invoke(currentObject);

            // search the childdata for the element of the selectedFuncType and set the index to that element
            for (int i = 0; i < currentSpinBox.ChildData.Count; i++)
            {
                if ((int)currentSpinBox.ChildData[i].FuncType == selectedFuncType)
                {
                    currentObject.ViewDataList[currentIndex].Index = i;
                    break;
                }
            }
        }

        return PreHookResult.Continue;
    }

    public static void Clear()
    {
        CustomInitializations.Clear();
    }

}