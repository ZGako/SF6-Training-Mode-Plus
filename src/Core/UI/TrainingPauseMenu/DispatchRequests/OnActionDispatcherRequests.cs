using SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_TMP.Core.UI.TrainingPauseMenu.DispatchRequests;

public class FunctionDispatcherRequest(TrainingFunctionDispatcher.FunctionDelegate functionDelegate) : IUIDispatcherRequest
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
}

public class OptionSelectDispatcherRequest(TrainingFunctionDispatcher.FunctionDelegate functionDelegate) : IUIDispatcherRequest
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
}