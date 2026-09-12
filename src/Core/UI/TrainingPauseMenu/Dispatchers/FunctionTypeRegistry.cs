using System.Collections.Generic;

using REFrameworkNET;

namespace SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Dispatchers;

public static class FunctionTypeRegistry
{
    // Store all new allocated functionTypes here
    private static readonly Dictionary<string, int> CustomFunctionTypes = [];

    public static int RegisterNewFunctionType(string name)
    {
        if (!CustomFunctionTypes.TryAdd(name, CustomFunctionTypes.Count + (int)app.training.TrainingFuncType.MAX + 1))
        {
            API.LogWarning($"Custom function '{name}' is already registered.");
        }
        return CustomFunctionTypes[name];
    }

    public static bool TryGetFunctionType(string name, out int funcType)
    {
        return CustomFunctionTypes.TryGetValue(name, out funcType);
    }

    public static void Clear()
    {
        CustomFunctionTypes.Clear();
    }
}