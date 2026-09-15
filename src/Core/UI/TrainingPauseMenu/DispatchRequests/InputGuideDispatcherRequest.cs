
using SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_TMP.Core.UI.TrainingPauseMenu.DispatchRequests;

public class InputGuideDispatcherRequest(InputGuideDispatcher.InputGuideDelegate action) : IUIDispatcherRequest
{
    private readonly InputGuideDispatcher.InputGuideDelegate _action = action;

    private string? _functionName;

    public void AddDispatcher(string functionName)
    {
        _functionName = functionName;
        InputGuideDispatcher.RegisterCustomInputGuideFunction(functionName, _action);
    }

    public void ClearDispatcher()
    {
        if (_functionName is null)
        {
            API.LogError("Cannot clear dispatcher because it was never added.");
            throw new InvalidOperationException("Cannot clear dispatcher because it was never added.");
        }
        InputGuideDispatcher.UnregisterCustomInputGuideFunction(_functionName);
    }
}