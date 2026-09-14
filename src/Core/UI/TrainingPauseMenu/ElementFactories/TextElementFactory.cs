using SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_TMP.Core.UI.TrainingPauseMenu.ElementFactories;

public static class TextElementFactory
{
    // Dummy test function, don't use
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

        newElement.FuncType = (app.training.TrainingFuncType)FunctionTypeRegistry.RegisterNewFunctionType("TEST_FUNCTION");
        TrainingFunctionDispatcher.RegisterCustomFunction("TEST_FUNCTION", (baseParam, viewData, rowIndex) => API.LogInfo("test function called"));
        newElement.IsEnabled = true;

        return newElement;
    }

    public static app.training.TrainingMenuData Create(string message, string functionName, TrainingFunctionDispatcher.FunctionDelegate action)
    {
        // create instance as ManagedObject
        var newElementMo = app.training.TrainingMenuData.REFType.CreateInstance(0);
        newElementMo.Globalize();

        // create a new message for the element
        var newMessage = new CustomMessage(message);
        MessageManager.SetGuid(newElementMo, "_MessageID", newMessage.Id);

        // convert to array type and set the other fields
        var newElement = newElementMo.As<app.training.TrainingMenuData>();


        newElement.FuncType = (app.training.TrainingFuncType)FunctionTypeRegistry.RegisterNewFunctionType(functionName);
        TrainingFunctionDispatcher.RegisterCustomFunction(functionName, action);
        newElement.IsEnabled = true;

        return newElement;
    }

}