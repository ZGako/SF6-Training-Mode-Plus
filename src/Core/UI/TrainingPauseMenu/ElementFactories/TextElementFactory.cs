using System;

using REFrameworkNET;

using SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.ElementFactories;

public static class TextElementFactory
{
    public static app.training.TrainingMenuData Create(string message)
    {
        // create instance as ManagedObject
        var newElementMo = app.training.TrainingMenuData.REFType.CreateInstance(0);
        newElementMo.Globalize();

        // create a new message for the element
        var newMessage = new CustomMessage(message);
        MessageManager.SetGuid(newElementMo, "_MessageID", newMessage.Id);

        // convert to array type and set the other fields
        var newElement = newElementMo.As<app.training.TrainingMenuData>();

        newElement.FuncType = (app.training.TrainingFuncType)TrainingFunctionDispatcher.RegisterCustomFunction("TestFunction", () => API.LogInfo("test function called"));
        newElement.IsEnabled = true;

        return newElement;
    }

    public static app.training.TrainingMenuData Create(string message, string functionName, Action action)
    {
        // create instance as ManagedObject
        var newElementMo = app.training.TrainingMenuData.REFType.CreateInstance(0);
        newElementMo.Globalize();

        // create a new message for the element
        var newMessage = new CustomMessage(message);
        MessageManager.SetGuid(newElementMo, "_MessageID", newMessage.Id);

        // convert to array type and set the other fields
        var newElement = newElementMo.As<app.training.TrainingMenuData>();

        newElement.FuncType = (app.training.TrainingFuncType)TrainingFunctionDispatcher.RegisterCustomFunction(functionName, action);
        newElement.IsEnabled = true;

        return newElement;
    }

}