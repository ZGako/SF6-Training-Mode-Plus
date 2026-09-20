

using SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_TMP.Core.UI.TrainingPauseMenu.DispatchRequests;

public class SpinBoxDispatcherRequest(SpinBoxDispatcher.SpinBoxInitDelegate action) : SingleUseModificationRequest, IUIDispatcherRequest
{
    private readonly SpinBoxDispatcher.SpinBoxInitDelegate _action = action;

    private string? _functionName;

    public void AddDispatcher(string functionName)
    {
        _functionName = functionName;
        SpinBoxDispatcher.RegisterCustomSpinBoxInitialization(functionName, _action);
    }

    public void ClearDispatcher()
    {
        if (_functionName is null)
        {
            API.LogError("Cannot clear dispatcher because it was never added.");
            throw new InvalidOperationException("Cannot clear dispatcher because it was never added.");
        }
        SpinBoxDispatcher.UnregisterCustomSpinBoxInitialization(_functionName);
    }

    public override SpinBoxDispatcherRequest Clone()
    {
        return new SpinBoxDispatcherRequest(_action);
    }
}