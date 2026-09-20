namespace SF6_TMP.Core.UI.TrainingPauseMenu;

public interface IUIDynamicModifier
{
    void Restore();
}

public interface IUIModificationRequest
{
    IUIModificationRequest Clone();

}

public interface IUICustomElementInitialization
{
    app.training.TrainingMenuData InitializeCustomElement(string functionName);

    // message strings handling and stuff. Also Release the managed object created
    void Clear();

    public app.training.TrainingMenuData GetElementData();
}

public interface IUIDispatcherRequest : IUIModificationRequest
{
    void AddDispatcher(string functionName);

    /// TODO: do I even need this if I clear everything from MenuManager on rebuild
    void ClearDispatcher();
}


public abstract class SingleUseModificationRequest : IUIModificationRequest
{
    private bool _isConsumed = false;

    public abstract IUIModificationRequest Clone();

    public void Consume()
    {
        if (_isConsumed)
        {
            throw new InvalidOperationException("This modification request has already been consumed.");
        }

        _isConsumed = true;
    }

    public void Unconsume()
    {
        _isConsumed = false;
    }
}