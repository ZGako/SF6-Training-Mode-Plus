using REFrameworkNET;
using REFrameworkNET.Callbacks;
using REFrameworkNET.Attributes;
using REFrameworkNET.Collections;

using SF6_Training_Mode_Plus.Core;

namespace SF6_Training_Mode_Plus.Modules;

public class GameSpeedPlus : ITrainingModePlusModule
{
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


        // Add new UI element here
        var testing = TrainingModePlus.TrainingManager._UIData._MenuData[1]._ChildData[3];

        // var testingMo = ManagedObject.IsManagedObject(testing.Address());

        var oldArrayMo = (testing as IObject).GetField("_ChildData") as ManagedObject;
        var oldArray = oldArrayMo.As<_System.Array>();

        var newArr = app.training.TrainingMenuData.REFType.CreateManagedArray(4);
        newArr.Globalize();

        var arr = newArr.As<_System.Array>();

        for (int i = 0; i < oldArray.Count; i++)
        {
            // copy existing elements to new array
            arr.SetValue(oldArray.GetValue(i), i);
        }

        // Add new element to the end of the array

        var newElementMo = app.training.TrainingMenuData.REFType.CreateInstance(0);
        newElementMo.Globalize();

        var newElement = newElementMo.As<app.training.TrainingMenuData>();

        newElement._FuncType = (app.training.TrainingFuncType)500;

        arr.SetValue(newElement, oldArray.Count);

        testing._ChildData = newArr.As<app.training.TrainingMenuData_Array1D>();


        API.LogInfo("Finished");

    }

    [MethodHook(typeof(app.training.TrainingManager), nameof(app.training.TrainingManager.InitUIMenu), MethodHookType.Post)]
    public static void MyPostHook(ref ulong retval)
    {
        // Add new UI element here
        API.LogInfo("Adding GameSpeedPlus UI element...");
    }

}
