

namespace SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

public static class TrainingFunctionDispatcher
{
    // A dictionary mapping our custom FuncType integers to C# lambdas
    private static readonly Dictionary<string, FunctionDelegate> CustomFunctions = [];

    private static readonly Dictionary<string, FunctionDelegate> CustomOptionSelectFunctions = [];

    // delegate type for all functions are the same signature
    // TODO add a return value from the dispatcher that determines whether or not the menu should close. This would allow for more flexibility in custom functions.
    public delegate void FunctionDelegate(app.training.BaseParam baseParam, app.training.UIFlowTrainingMenu.Param.ViewData viewData, int rowIndex);

    // Function specific methods

    public static void RegisterCustomFunction(string name, FunctionDelegate action)
    {

        if (FunctionTypeRegistry.TryGetFunctionType(name, out int _))
        {
            if (!CustomFunctions.TryAdd(name, action))
            {
                API.LogWarning($"Custom function '{name}' is already registered.");
            }
        }
        else
        {
            API.LogError($"Custom function '{name}' is not registered. Call RegisterNewFunctionType first.");
        }
    }

    public static void UnregisterCustomFunction(string name)
    {
        // TODO double check if this all that is needed, I'm too tired to think right now.
        if (!CustomFunctions.Remove(name))
        {
            API.LogWarning($"Custom function '{name}' was not registered or already unregistered.");
        }
    }

    [ThreadStatic]
    private static bool s_handledCustomFunction;

    [MethodHook(typeof(app.training.TrainingMenuFunc), "Function", MethodHookType.Pre)]
    private static PreHookResult OnFunctionPre(Span<ulong> args)
    {
        // FuncType argument
        int funcType = (int)args[2];

        if (funcType <= (int)app.training.TrainingFuncType.MAX) return PreHookResult.Continue;

        if (!FunctionTypeRegistry.TryGetFunctionName(funcType, out string? functionName))
        {
            return PreHookResult.Continue;
        }

        if (CustomFunctions.TryGetValue(functionName!, out FunctionDelegate? action))
        {

            var baseParam = UIHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[3]);
            var viewData = UIHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);

            if (baseParam == null || viewData == null)
            {
                API.LogWarning("Failed to retrieve baseParam or viewData in OnFunctionPre.");
                return PreHookResult.Continue;
            }

            // Invoke function
            action.Invoke(baseParam, viewData, (int)args[5]);

            s_handledCustomFunction = true;

            // Skip the game's original handler
            return PreHookResult.Skip;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc), "Function", MethodHookType.Post)]
    private static void OnFunctionPost(ref ulong retval)
    {
        if (s_handledCustomFunction)
        {
            // output is Boolean = true
            retval = 0;

            // Reset for the next call
            s_handledCustomFunction = false;
        }
    }


    // OptionSelectFunction specific methods


    // ThreadStatic ensures thread-safety if the game calls this concurrently
    [ThreadStatic]
    private static bool s_handledCustomOptionSelectFunction;

    public static void RegisterCustomOptionSelectFunction(string name, FunctionDelegate action)
    {
        // Generate a unique FuncType integer for this custom function

        if (FunctionTypeRegistry.TryGetFunctionType(name, out int _))
        {
            if (!CustomOptionSelectFunctions.TryAdd(name, action))
            {
                API.LogWarning($"Custom option select function '{name}' is already registered.");
            }
        }
        else
        {
            API.LogError($"Custom option select function '{name}' is not registered. Call RegisterNewFunctionType first.");
        }
    }

    public static void RegisterCustomOptionSelectFunction(app.training.TrainingFuncType funcType, FunctionDelegate action)
    {
        if (FunctionTypeRegistry.TryGetFunctionName((int)funcType, out string? name))
        {
            if (!CustomOptionSelectFunctions.TryAdd(name!, action))
            {
                API.LogWarning($"Custom option select function for FuncType '{funcType}' is already registered.");
            }
        }
        else
        {
            API.LogError($"Game function type '{funcType}' is not registered. Call RegisterGameFunctionType first.");
        }
    }

    public static void UnregisterCustomOptionSelectFunction(string name)
    {
        if (!CustomOptionSelectFunctions.Remove(name))
        {
            API.LogWarning($"Custom option select function '{name}' was not registered or already unregistered.");
        }
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc), "OptionSelectFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)", MethodHookType.Pre)]
    private static PreHookResult OnOptionSelectFunctionPre(Span<ulong> args)
    {
        // FuncType argument
        int funcType = (int)args[2];

        if (!FunctionTypeRegistry.TryGetFunctionName(funcType, out string? functionName))
        {
            return PreHookResult.Continue;
        }

        // Adjust the FuncType to match our custom range
        if (CustomOptionSelectFunctions.TryGetValue(functionName!, out FunctionDelegate? action))
        {
            try
            {
                int rowIndex = (int)args[5];

                var baseParam = UIHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param>(args[3]);
                var viewData = UIHelpers.GetAddressAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);

                // Invoke function
                action!.Invoke(baseParam, viewData, rowIndex);
            }
            catch (Exception ex)
            {
                API.LogError($"Exception in OnOptionSelectFunctionPre: {ex.Message}");
                return PreHookResult.Continue;
            }

            // end of debugging

            s_handledCustomOptionSelectFunction = true;

            // Skip the game's original handler
            return PreHookResult.Skip;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc), "OptionSelectFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)", MethodHookType.Post)]
    private static void OnOptionSelectFunctionPost(ref ulong retval)
    {
        if (s_handledCustomOptionSelectFunction)
        {
            // output is Boolean, determines whether or not the menu should close.
            retval = 0;

            // Reset for the next call
            s_handledCustomOptionSelectFunction = false;
        }
    }

    public static void Clear()
    {
        CustomFunctions.Clear();
        CustomOptionSelectFunctions.Clear();
    }


}