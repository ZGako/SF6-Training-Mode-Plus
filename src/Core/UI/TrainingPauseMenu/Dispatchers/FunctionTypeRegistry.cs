

using via.dynamics;

namespace SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

public static class FunctionTypeRegistry
{
    // Store all new allocated functionTypes here
    private static readonly Dictionary<string, int> ForwardLookup = [];
    private static readonly Dictionary<int, string> ReverseLookup = [];

    public static int RegisterNewFunctionType(string name)
    {
        var funcType = (int)app.training.TrainingFuncType.MAX + 1 + ForwardLookup.Count;

        // since the mappings have to be unique, check if the name is already registered
        if (ForwardLookup.TryGetValue(name, out int value))
        {
            API.LogWarning($"Custom Function type '{name}' is already registered.");
            return value;
        }

        ForwardLookup[name] = funcType;
        ReverseLookup[funcType] = name;

        return ForwardLookup[name];
    }

    public static string RegisterGameFunctionType(app.training.TrainingFuncType funcType)
    {
        int funcTypeInt = (int)funcType;
        string name = funcType.ToString();

        // since the mappings have to be unique, check if the name is already registered
        if (ForwardLookup.TryGetValue(name, out int _))
        {
            API.LogWarning($"Game Function type '{name}' is already registered.");
        }

        ForwardLookup[name] = funcTypeInt;
        ReverseLookup[funcTypeInt] = name;

        return name;
    }

    public static bool TryGetFunctionType(string name, out int funcType)
    {
        return ForwardLookup.TryGetValue(name, out funcType);
    }

    public static bool TryGetFunctionName(int funcType, out string? name)
    {
        return ReverseLookup.TryGetValue(funcType, out name);
    }

    public static void Clear()
    {
        ForwardLookup.Clear();
        ReverseLookup.Clear();
    }
}