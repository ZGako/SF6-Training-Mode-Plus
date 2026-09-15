

using app.network.api.Enum;

namespace SF6_TMP.Core.UI.TrainingPauseMenu;

public class UICustomElementNode(string functionName, IUICustomElementInitialization customInitializer)
{
    private readonly string _functionName = functionName;
    private readonly IUICustomElementInitialization _customInitializer = customInitializer;

    private readonly List<IUIDispatcherRequest> _dispatcherInstructions = [];

    public bool AddDispatcher(IUIDispatcherRequest dispatcherRequest)
    {
        Type incomingType = dispatcherRequest.GetType();
        bool hasSameDispatcherClass = _dispatcherInstructions.Any(r => r.GetType() == incomingType);

        if (hasSameDispatcherClass)
        {
            // A dispatcher request of this specific concrete class already exists.
            return false;
        }

        _dispatcherInstructions.Add(dispatcherRequest);
        return true;
    }

    public void RemoveDispatcher(IUIDispatcherRequest dispatcherRequest)
    {
        _dispatcherInstructions.Remove(dispatcherRequest);
    }

    private readonly List<UICustomElementNode> _childNodes = [];

    private ManagedObject? _childDataArrayMo = null;

    /// FIXME current implementation doesn't follow the plan of having the children support registering and unregistering and having their
    /// dispatchers added each time whilst not reinitializing the custom element each time.

    public app.training.TrainingMenuData Build()
    {
        // call the custom initializer to initialize the custom element
        // this way the function types will be numbered similarly to the game (doesn't actually matter)
        var customElementData = _customInitializer.InitializeCustomElement(_functionName);

        // now we traverse the child nodes recursively and build their data
        // on the way up we add the dispatchers and create the childData arrays
        if (_childNodes.Count > 0)
        {
            // if there's an existing childData array, release it first
            _childDataArrayMo?.Release();

            // create a new array
            _childDataArrayMo = app.training.TrainingMenuData.REFType.CreateManagedArray((uint)_childNodes.Count);
            _childDataArrayMo.Globalize();

            var childDataArray = _childDataArrayMo.As<app.training.TrainingMenuData_Array1D>();

            for (int i = 0; i < _childNodes.Count; i++)
            {
                var childData = _childNodes[i].Build();
                childDataArray[i] = childData;
            }

            // assign the childData array to the custom element
            customElementData.ChildData = childDataArray;
        }

        // add the dispatchers to the custom element
        foreach (var dispatcher in _dispatcherInstructions)
        {
            dispatcher.AddDispatcher(_functionName);
        }

        return customElementData;
    }

    /// TODO the new element adding requests when deleted have to clear out their own message UIs

    /// this is the full clear method that's called when the element gets completely removed from the tree
    public void Clear()
    {
        // traverse the children and clear them
        foreach (var child in _childNodes)
        {
            child.Clear();
        }
        _childDataArrayMo?.Release();
        _childDataArrayMo = null;

        _customInitializer.Clear();

        foreach (var dispatcher in _dispatcherInstructions)
        {
            dispatcher.ClearDispatcher();
        }
    }

}