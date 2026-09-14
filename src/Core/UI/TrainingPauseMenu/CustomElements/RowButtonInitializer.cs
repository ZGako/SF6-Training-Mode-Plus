using SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_TMP.Core.UI.TrainingPauseMenu.CustomElements;

public class RowButtonInitializer(CustomMessage message, CustomMessage guideMessage) : IUICustomElementInitialization
{
    private CustomMessage Message { get; init; } = message;

    private CustomMessage GuideMessage { get; init; } = guideMessage;

    private ManagedObject? _customElementDataMo;

    public app.training.TrainingMenuData InitializeCustomElement(string functionName)
    {
        var newElementMo = app.training.TrainingMenuData.REFType.CreateInstance(0);
        newElementMo.Globalize();

        MessageManager.SetGuid(newElementMo, "_MessageID", Message.Id);
        MessageManager.SetGuid(newElementMo, "_GuideMessage", GuideMessage.Id);

        _customElementDataMo = newElementMo;

        var newElement = newElementMo.As<app.training.TrainingMenuData>();
        newElement.FuncType = (app.training.TrainingFuncType)FunctionTypeRegistry.RegisterNewFunctionType(functionName);

        newElement.IsEnabled = true;

        return newElement;
    }

    public void Clear()
    {
        Message.ClearCachedEngineStrings();
        _customElementDataMo?.Release();
    }

}