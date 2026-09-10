using System;
using System.Collections.Generic;

using REFrameworkNET;
using REFrameworkNET.Attributes;

namespace SF6_Training_Mode_Plus.Core.TrainingPauseMenu;

public static class MenuDataElementFactory
{
    public static app.training.TrainingMenuData CreateTextElement(string message)
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
}