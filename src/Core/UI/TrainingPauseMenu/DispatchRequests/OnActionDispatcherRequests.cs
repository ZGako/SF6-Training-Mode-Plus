using SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_TMP.Core.UI.TrainingPauseMenu.DispatchRequests;

public class FunctionDispatcherRequest(TrainingFunctionDispatcher.FunctionDelegate functionDelegate) : SingleUseModificationRequest, IUIDispatcherRequest
{
    private readonly TrainingFunctionDispatcher.FunctionDelegate _functionDelegate = functionDelegate;

    private string? _functionName;

    public void AddDispatcher(string functionName)
    {
        _functionName = functionName;
        TrainingFunctionDispatcher.RegisterCustomFunction(functionName, _functionDelegate);
    }

    public void ClearDispatcher()
    {
        if (_functionName is null)
        {
            API.LogError("Cannot clear dispatcher because it was never added.");
            throw new InvalidOperationException("Cannot clear dispatcher because it was never added.");
        }
        TrainingFunctionDispatcher.UnregisterCustomFunction(_functionName);
    }

    public override FunctionDispatcherRequest Clone()
    {
        return new FunctionDispatcherRequest(_functionDelegate);
    }
}

public class OptionSelectDispatcherRequest(TrainingFunctionDispatcher.FunctionDelegate functionDelegate) : SingleUseModificationRequest, IUIDispatcherRequest
{
    private readonly TrainingFunctionDispatcher.FunctionDelegate _functionDelegate = functionDelegate;

    private string? _functionName;

    public void AddDispatcher(string functionName)
    {
        _functionName = functionName;
        TrainingFunctionDispatcher.RegisterCustomOptionSelectFunction(functionName, _functionDelegate);
    }

    public void ClearDispatcher()
    {
        if (_functionName is null)
        {
            API.LogError("Cannot clear dispatcher because it was never added.");
            throw new InvalidOperationException("Cannot clear dispatcher because it was never added.");
        }
        TrainingFunctionDispatcher.UnregisterCustomOptionSelectFunction(_functionName);
    }

    public override OptionSelectDispatcherRequest Clone()
    {
        return new OptionSelectDispatcherRequest(_functionDelegate);
    }
}