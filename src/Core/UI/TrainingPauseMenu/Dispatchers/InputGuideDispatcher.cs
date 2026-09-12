
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using REFrameworkNET;
using REFrameworkNET.Attributes;

namespace SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Dispatchers;

public static class InputGuideDispatcher
{

    private static readonly Dictionary<int, InputGuideDelegate> CustomInputGuideDelegates = [];

    // delegate type for pause menu input guide data modification
    public delegate void InputGuideDelegate(ref REFrameworkNET.Collections.IList<app.InputGuideData> outInputGuideDataList, ref REFrameworkNET.Collections.IList<string> outStringList);


    [ThreadStatic]
    private static ulong s_pendingOutInputGuideDataListPtr;

    [ThreadStatic]
    private static ulong s_pendingOutStringListPtr;

    [ThreadStatic]
    private static int s_handledCustomFunction;


    public static void RegisterCustomInputGuideFunction(string name, InputGuideDelegate action)
    {
        // Generate a unique FuncType integer for this custom function

        if (FunctionTypeRegistry.TryGetFunctionType(name, out int funcType))
        {
            if (!CustomInputGuideDelegates.TryAdd(funcType, action))
            {
                API.LogWarning($"Custom input guide function '{name}' is already registered.");
            }
        }
        else
        {
            API.LogError($"Custom input guide function '{name}' is not registered. Call RegisterNewFunctionType first.");
        }
    }

    public static void RegisterCustomInputGuideFunction(app.training.TrainingFuncType funcType, InputGuideDelegate action)
    {
        int funcTypeInt = (int)funcType;

        if (!CustomInputGuideDelegates.TryAdd(funcTypeInt, action))
        {
            API.LogWarning($"Custom input guide function for FuncType '{funcType}' is already registered.");
        }
    }


    [MethodHook(typeof(app.training.TrainingManager), "GetGuideData(app.training.TrainingMenuData, System.Collections.Generic.List`1<app.InputGuideData>, System.Collections.Generic.List`1<System.String>)", MethodHookType.Pre)]
    private static PreHookResult OnGetGuideDataPre(Span<ulong> args)
    {

        var currentMenuData = ManagedObject.ToManagedObject(args[2])?.As<app.training.TrainingMenuData>();

        if (currentMenuData == null)
        {
            API.LogWarning("Failed to retrieve current TrainingMenuData object.");
            return PreHookResult.Continue;
        }

        if (CustomInputGuideDelegates.ContainsKey((int)currentMenuData.FuncType))
        {
            s_handledCustomFunction = (int)currentMenuData.FuncType;
            s_pendingOutInputGuideDataListPtr = args[3];
            s_pendingOutStringListPtr = args[4];
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.TrainingManager), "GetGuideData(app.training.TrainingMenuData, System.Collections.Generic.List`1<app.InputGuideData>, System.Collections.Generic.List`1<System.String>)", MethodHookType.Post)]
    private static void OnGetGuideDataPost(ref ulong retval)
    {
        if (s_pendingOutInputGuideDataListPtr == 0 || s_pendingOutStringListPtr == 0 || s_handledCustomFunction == 0)
        {
            return;
        }

        if (CustomInputGuideDelegates.TryGetValue(s_handledCustomFunction, out InputGuideDelegate? action))
        {
            try
            {
                // Safely dereference the pointer using Marshal
                ulong dereferencedAddress1 = (ulong)Marshal.ReadInt64(new IntPtr((long)s_pendingOutInputGuideDataListPtr));
                ulong dereferencedAddress2 = (ulong)Marshal.ReadInt64(new IntPtr((long)s_pendingOutStringListPtr));

                var inputGuideDataList = UIHelpers.GetAddressAs<REFrameworkNET.Collections.IList<app.InputGuideData>>(dereferencedAddress1);
                var outStringList = UIHelpers.GetAddressAs<REFrameworkNET.Collections.IList<string>>(dereferencedAddress2);

                action.Invoke(ref inputGuideDataList, ref outStringList);
            }
            catch (Exception ex)
            {
                API.LogError($"Error occurred while retrieving the input guide data list: {ex.Message}");
                return;
            }
        }

        s_handledCustomFunction = 0; // Reset after use
        s_pendingOutInputGuideDataListPtr = 0; // Reset after use
        s_pendingOutStringListPtr = 0; // Reset after use
    }

    public static void Clear()
    {
        CustomInputGuideDelegates.Clear();
    }

}