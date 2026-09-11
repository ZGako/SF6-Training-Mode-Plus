using System;
using System.Collections.Generic;

using app;

using REFrameworkNET;
using REFrameworkNET.Attributes;

using SF6_Training_Mode_Plus.Core.UI;
namespace SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Dispatchers;

public static class TrainingFunctionDispatcher
{
    // Store all new allocated functionTypes here
    private static readonly Dictionary<string, int> CustomFunctionTypes = [];

    // A dictionary mapping our custom FuncType integers to C# lambdas
    private static readonly Dictionary<int, FunctionDelegate> CustomFunctions = [];

    private static readonly Dictionary<int, FunctionDelegate> CustomOptionSelectFunctions = [];

    // delegate type for all functions are the same signature
    public delegate void FunctionDelegate(app.training.BaseParam baseParam, app.training.UIFlowTrainingMenu.Param.ViewData viewData, int rowIndex);


    public static int RegisterNewFunctionType(string name)
    {
        if (!CustomFunctionTypes.TryAdd(name, CustomFunctionTypes.Count + (int)app.training.TrainingFuncType.MAX + 1))
        {
            API.LogWarning($"Custom function '{name}' is already registered.");
        }

        return CustomFunctionTypes[name];
    }

    // Function specific methods

    public static void RegisterCustomFunction(string name, FunctionDelegate action)
    {
        // Generate a unique FuncType integer for this custom function

        if (CustomFunctionTypes.TryGetValue(name, out int funcType))
        {
            if (!CustomFunctions.TryAdd(funcType, action))
            {
                API.LogWarning($"Custom function '{name}' is already registered.");
            }
        }
        else
        {
            API.LogError($"Custom function '{name}' is not registered. Call RegisterNewFunctionType first.");
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

        // Adjust the FuncType to match our custom range
        if (CustomFunctions.TryGetValue(funcType, out FunctionDelegate? action))
        {

            var baseParam = UIHelpers.GetArgAs<app.training.UIFlowTrainingMenu.Param>(args[3]);
            var viewData = UIHelpers.GetArgAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);

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

        if (CustomFunctionTypes.TryGetValue(name, out int funcType))
        {
            if (!CustomOptionSelectFunctions.TryAdd(funcType, action))
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
        int funcTypeInt = (int)funcType;

        if (!CustomOptionSelectFunctions.TryAdd(funcTypeInt, action))
        {
            API.LogWarning($"Custom option select function for FuncType '{funcType}' is already registered.");
        }
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc), "OptionSelectFunction(app.training.TrainingFuncType, app.training.BaseParam, app.training.UIFlowTrainingMenu.Param.ViewData, System.Int32)", MethodHookType.Pre)]
    private static PreHookResult OnOptionSelectFunctionPre(Span<ulong> args)
    {
        // FuncType argument
        int funcType = (int)args[2];

        // Adjust the FuncType to match our custom range
        if (CustomOptionSelectFunctions.TryGetValue(funcType, out FunctionDelegate? action))
        {
            try
            {
                int rowIndex = (int)args[5];

                var baseParam = UIHelpers.GetArgAs<app.training.UIFlowTrainingMenu.Param>(args[3]);
                var viewData = UIHelpers.GetArgAs<app.training.UIFlowTrainingMenu.Param.ViewData>(args[4]);

                // Invoke function
                action.Invoke(baseParam, viewData, rowIndex);
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
            // output is Boolean = true
            retval = 0;

            // Reset for the next call
            s_handledCustomOptionSelectFunction = false;
        }
    }


}