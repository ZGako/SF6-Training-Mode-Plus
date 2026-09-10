using System;
using System.Collections.Generic;

using REFrameworkNET;
using REFrameworkNET.Attributes;

namespace SF6_Training_Mode_Plus.Core.TrainingPauseMenu;

public static class TrainingFunctionDispatcher
{
    // A dictionary mapping our custom FuncType integers to C# lambdas
    private static readonly Dictionary<int, Action> CustomActions = [];

    // ThreadStatic ensures thread-safety if the game calls this concurrently
    [ThreadStatic]
    private static bool s_handledCustomAction;

    [MethodHook(typeof(app.training.TrainingMenuFunc), "Function", MethodHookType.Pre)]
    private static PreHookResult OnFunctionPre(Span<ulong> args)
    {
        // FuncType argument
        int funcType = (int)args[3];

        if (funcType <= (int)app.training.TrainingFuncType.MAX) return PreHookResult.Continue;

        // Adjust the FuncType to match our custom range
        funcType -= (int)app.training.TrainingFuncType.MAX + 1;

        if (CustomActions.TryGetValue(funcType, out Action? action))
        {
            // Invoke function
            action.Invoke();

            s_handledCustomAction = true;

            // Skip the game's original handler
            return PreHookResult.Skip;
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingMenuFunc), "Function", MethodHookType.Post)]
    private static void OnFunctionPost(ref ulong retval)
    {
        if (s_handledCustomAction)
        {
            // output is Boolean = true
            retval = 1;

            // Reset for the next call
            s_handledCustomAction = false;
        }
    }

    public static int RegisterCustomFunction(string name, Action action)
    {
        // Generate a unique FuncType integer for this custom function
        int funcType = CustomActions.Count;

        // Store the action in the dictionary
        CustomActions[funcType] = action;

        return funcType + (int)app.training.TrainingFuncType.MAX + 1; // Return the adjusted FuncType for use in the menu
    }
}