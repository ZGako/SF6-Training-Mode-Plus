
using SF6_TMP.Core;
using SF6_TMP.Core.UI;
using SF6_TMP.Core.UI.TrainingPauseMenu;
using SF6_TMP.Core.UI.TrainingPauseMenu.CustomElements;
using SF6_TMP.Core.UI.TrainingPauseMenu.DispatchRequests;
using SF6_TMP.Core.UI.TrainingPauseMenu.ModificationRequests;

namespace SF6_TMP.TrainingModePlus.Modules;

public class TestingRefactor : ITrainingModePlusModule
{
    public static TestingRefactor Instance { get; private set; } = new TestingRefactor();

    private TestingRefactor() { }

    private readonly List<IUIModificationRequest> _modificationRequests = [];

    private bool _buttonVisible = true;

    private AppendElements? _randomizerButtonAppendRequest;

    public void Init()
    {
        FunctionDispatcherRequest dispatcherRequest = new((_, _, _) => { });
        UICustomElementNode newElement = new("TestRowButton",
                                            new RowButtonInitializer(new CustomMessage("Restart Battle and Randomize"),
                                                                     new CustomMessage("Apply current settings, randomizing the appropriate settings, and restart the battle.")));

        newElement.AddDispatcher(dispatcherRequest);

        AppendElements appendRequest = new([(1, newElement)]);
        _randomizerButtonAppendRequest = appendRequest;

        // create button in another tab, that toggles the first buttons existance
        FunctionDispatcherRequest toggleDispatcherReq = new((_, _, _) => ToggleButtonVisibility());
        UICustomElementNode newElement2 = new("TOGGLE_OTHER_BUTTON",
                                            new RowButtonInitializer(new CustomMessage("Toggle Other Button"),
                                                                     new CustomMessage("Toggles the visibility of the other button.")));

        newElement2.AddDispatcher(toggleDispatcherReq);

        AppendElements toggleAppendRequest = new([(-1, newElement2)]);

        try
        {
            PauseMenuManager.RegisterModification([1], appendRequest);
            PauseMenuManager.RegisterModification([0], toggleAppendRequest);
        }
        catch (Exception ex)
        {
            API.LogError($"Error registering modification: {ex.Message}");
        }
        _modificationRequests.Add(appendRequest);
        _modificationRequests.Add(toggleAppendRequest);

        API.LogInfo("TestingRefactor module initialized!");
    }

    private void ToggleButtonVisibility()
    {
        _buttonVisible = !_buttonVisible;

        if (_buttonVisible)
        {
            // Show the button
            PauseMenuManager.RegisterModification([1], _randomizerButtonAppendRequest!);
        }
        else
        {
            PauseMenuManager.UnregisterModification(_randomizerButtonAppendRequest!);
        }

        PauseMenuManager.RebuildUI();
    }

    private static void TestForItem()
    {
        // This is where you can intercept the ChangeState method of the TrainingManager.
        // You can modify the arguments or perform actions before the state change occurs.
        // For example, you could log the state change or modify parameters based on your module's logic.

        try
        {
            var uiAgentManager = GameSingletonRegistry.UIAgentManager;
            if (uiAgentManager == null)
            {
                API.LogWarning("UIAgentManager is not initialized yet.");
                return;
            }
            var thing = GameSingletonRegistry.UIAgentManager!._Entries;

            if (thing[64] == null)
            {
                API.LogWarning("UIAgentManager._Entries[64] is null.");
                return;
            }

            var uiAgent = thing[64].Agent._PartsManager;
            if (uiAgent == null)
            {
                API.LogWarning("UIAgentManager._Entries[64].Agent._PartsManager is null.");
                return;
            }

            API.LogInfo($"UIAgentManager._Entries[64].Agent._PartsManager address: {(uiAgent as IObject).GetAddress()} and {(uiAgent as IObject).GetAddress():X}");

            var partsList = uiAgent._List;
            if (partsList == null)
            {
                API.LogWarning("UIAgentManager._Entries[64].Agent._PartsManager._List is null.");
                return;
            }

            API.LogInfo($"UIAgentManager._Entries[64].Agent._PartsManager._List has address: {(partsList as IObject).GetAddress()}");

            var partItem4 = partsList[4];
            if (partItem4 == null)
            {
                API.LogWarning("UIAgentManager._Entries[64].Agent._PartsManager._List[4] is null.");
                return;
            }

            API.LogInfo($"UIAgentManager._Entries[64].Agent._PartsManager._List[4] has address: {(partItem4 as IObject).GetAddress()} and {(partItem4 as IObject).GetAddress():X}");

            // get partItem4 as a app.UIPartsGroupScroll
            // then cast it to app.UIPartsGroup
            var scrollGroup = (partItem4 as IObject)?.As<app.UIPartsGroup>();
            if (scrollGroup == null)
            {
                API.LogWarning("UIAgentManager._Entries[64].Agent._PartsManager._List[4] is not a UIPartsGroup.");
                return;
            }

            var children = scrollGroup._Children;
            if (children == null)
            {
                API.LogWarning("UIAgentManager._Entries[64].Agent._PartsManager._List[4]._Children is null.");
                return;
            }

            // get _Children[index we want] as app.UIPartsGroup (right now we want index 2)
            var childItem2 = (children[2] as IObject)?.As<app.UIPartsGroup>();
            if (childItem2 == null)
            {
                API.LogWarning("UIAgentManager._Entries[64].Agent._PartsManager._List[4]._Children[2] is null.");
                return;
            }
            API.LogInfo($"UIAgentManager._Entries[64].Agent._PartsManager._List[4]._Children[2] has address: {(childItem2 as IObject).GetAddress()} and {(childItem2 as IObject).GetAddress():X}");
            // from that, we have _FocusIndex
            var focusIndex = childItem2._FocusIndex;
            // then we get _Children[_FocusIndex] as app.UIPartsSpin (only in this case)  
            var spinItem = (childItem2._Children[focusIndex] as IObject)?.As<app.UIPartsGroupItem>();
            if (spinItem == null)
            {
                API.LogWarning("UIAgentManager._Entries[64].Agent._PartsManager._List[4]._Children[2]._Children[_FocusIndex] is not a UIPartsSpin.");
                return;
            }
            // then we cast to app.UIPartsGroupItem, and we get _SelectItem as via.gui.SelectItem (which we can cast to via.gui.Control)
            var selectItem = (spinItem._SelectItem as IObject)?.As<via.gui.Control>();
            if (selectItem == null)
            {
                API.LogWarning("UIAgentManager._Entries[64].Agent._PartsManager._List[4]._Children[2]._Children[_FocusIndex]._SelectItem is not a via.gui.Control.");
                return;
            }

            API.LogInfo($"Final address up until now: {(selectItem as IObject).GetAddress()} and {(selectItem as IObject).GetAddress():X}");

            // from that we can try to start the walks
            // 1. Invoke the native method
            // We cast to IObject to call the method by its specific signature string.
            // Passing null for the System.Type argument generally returns all children.
            // 1. Get the System.Type for via.gui.Control using the typed proxy's REFType field
            var controlType = via.gui.PlayObject.REFType.RuntimeType; //

            // 2. Pass it into the method
            var childrenObj = (selectItem as IObject)?.Call("getChildren(System.Type)", controlType) as ManagedObject;

            // If passing null throws an exception in SF6, you can pass the explicit System.Type:
            // var controlType = via.gui.Control.REFType.RuntimeType; // Gets the System.Type instance
            // var childrenObj = (selectItem as IObject)?.Call("getChildren(System.Type)", controlType) as ManagedObject;

            if (childrenObj == null)
            {
                API.LogWarning("selectItem.getChildren() returned null.");
                return;
            }

            // 2. Treat the returned object as a managed array
            // REFramework exposes managed arrays (T[]) through the _System.Array proxy.
            var childrenArray = childrenObj.As<_System.Array>();

            // Note: If the method returns a List<T> instead of a raw array, you must grab its 
            // internal "_items" field first, then cast THAT to _System.Array[cite: 3].
            if (childrenArray == null)
            {
                var itemsField = (childrenObj as IObject)?.GetField("_items") as ManagedObject;
                childrenArray = itemsField?.As<_System.Array>();
            }

            if (childrenArray == null)
            {
                API.LogWarning("Could not resolve children into a _System.Array.");
                return;
            }

            API.LogInfo($"selectItem contains {childrenArray.Length} children.");

            // 3. Iterate over the elements
            for (int i = 0; i < childrenArray.Length; i++)
            {
                // GetValue returns a ManagedObject for reference-type elements[cite: 3].
                var childMo = childrenArray.GetValue(i) as ManagedObject;
                if (childMo != null)
                {
                    var childName = (childMo as IObject)?.Call("get_Name") as string;
                    var childType = childMo.GetTypeDefinition()?.GetFullName();

                    API.LogInfo($"Child [{i}]: Name = {childName}, Type = {childType}");

                    // You can now safely cast childMo to via.gui.Control or other specific proxy types
                    // var childControl = childMo.As<via.gui.Control>();
                }
            }

            var textChildMo = childrenArray.GetValue(5) as ManagedObject;
            if (textChildMo == null)
            {
                API.LogWarning("Child at index 5 is null or not a ManagedObject.");
                return;
            }

            var textChild = textChildMo.As<via.gui.Text>();
            if (textChild == null)
            {
                API.LogWarning("Child at index 5 is not a via.gui.Text.");
                return;
            }
            API.LogInfo($"Child has {textChildMo.GetTypeDefinition().GetName()} type.");
            // 1. Create your new UI element (let's use a Panel as a container)
            var newChildMo = TDB.Get().FindType("via.gui.Text").CreateInstance(0);

            // 2. CRITICAL: Tell the engine to keep this object alive permanently.
            // If you skip this, the C# GC will eventually sweep it, and the game will hard crash[cite: 3].
            newChildMo.Globalize();

            // (Optional) Cast it to a typed proxy so you can easily configure it before attaching
            var newText = newChildMo.As<via.gui.Text>();
            if (newText != null)
            {
                textChild.copyProperties(newText);
                newText.Message = "Testing this new child injection!";
            }

            // 3. Invoke the addChild method you found on your selectItem!
            // We pass the exact signature to ensure the engine resolves the correct native function[cite: 3].
            // (selectItem as IObject).Call("addChild(via.gui.PlayObject)", newChildMo);


        }
        catch (Exception ex)
        {
            API.LogError($"Error in OnChangeStatePost: {ex.Message}");
        }
    }

    public void Unload()
    {

        try
        {
            foreach (var request in _modificationRequests)
            {
                PauseMenuManager.UnregisterModification(request);
            }
        }
        catch (Exception ex)
        {
            API.LogError($"Error rebuilding UI during unload: {ex.Message}");
        }
    }

    [MethodHook(typeof(app.UIPartsItem), nameof(app.UIPartsItem.SetupLayout), MethodHookType.Pre)]
    private static PreHookResult OnSetupLayoutPre(Span<ulong> args)
    {
        // return PreHookResult.Continue;
        var instance = ManagedObject.ToManagedObject(args[1]);
        if (instance == null) return PreHookResult.Continue;

        var spinItem = instance.As<app.UIPartsSpin>();
        if (spinItem == null) return PreHookResult.Continue;

        try
        {
            var selectItemMo = spinItem._SelectItem as IObject;
            var selectItem = selectItemMo?.As<via.gui.Control>();
            if (selectItem == null) return PreHookResult.Continue;

            // NEW: Check the name of the via.gui.Control to ensure it's the exact one we want
            var selectItemName = selectItemMo.Call("get_Name") as string;
            if (selectItemName != "p_Spin_1_h")
            {
                // This is a UIPartsSpin, but not the p_Spin_1_h one. Skip it.
                return PreHookResult.Continue;
            }

            API.LogInfo($"[SetupLayout Pre] Found target UIPartsSpin for {selectItemName} @ sdk.to_managed_object({instance.GetAddress()}) or 0x{instance.GetAddress():X}");

            var controlType = via.gui.PlayObject.REFType.RuntimeType;
            var childrenObj = selectItemMo.Call("getChildren(System.Type)", controlType) as ManagedObject;
            var childrenArray = childrenObj?.As<_System.Array>();

            if (childrenArray != null)
            {
                API.LogInfo($"  -> _SelectItem currently has {childrenArray.Length} native children.");

                // Proceed with your cloning and injection logic here!
                var textChildMo = childrenArray.GetValue(5) as ManagedObject;
                if (textChildMo == null)
                {
                    API.LogWarning("Child at index 5 is null or not a ManagedObject.");
                    return PreHookResult.Continue;
                }

                var textChild = textChildMo.As<via.gui.Text>();
                if (textChild == null)
                {
                    API.LogWarning("Child at index 5 is not a via.gui.Text.");
                    return PreHookResult.Continue;
                }
                API.LogInfo($"Child has {textChildMo.GetTypeDefinition().GetName()} type.");

                var panelChildMo = childrenArray.GetValue(4) as ManagedObject;
                if (panelChildMo == null)
                {
                    API.LogWarning("Child at index 4 is null or not a ManagedObject.");
                    return PreHookResult.Continue;
                }
                var panelChild = panelChildMo.As<via.gui.Panel>();
                if (panelChild == null)
                {
                    API.LogWarning("Child at index 4 is not a via.gui.Panel.");
                    return PreHookResult.Continue;
                }

                var scrollChildMo = childrenArray.GetValue(2) as ManagedObject;
                if (scrollChildMo == null)
                {
                    API.LogWarning("Child at index 2 is null or not a ManagedObject.");
                    return PreHookResult.Continue;
                }

                var scrollListChild = scrollChildMo.As<via.gui.ScrollList>();
                if (scrollListChild == null)
                {
                    API.LogWarning("Child at index 2 is not a via.gui.ScrollList.");
                    return PreHookResult.Continue;
                }

                // get the schollListChild's _Children[2] as via.gui.Panel
                var scrollListChildChildrenMo = scrollChildMo.Call("getChildren(System.Type)", controlType) as ManagedObject;
                var scrollListChildChildrenArray = scrollListChildChildrenMo?.As<_System.Array>();
                if (scrollListChildChildrenArray == null)
                {
                    API.LogWarning("Failed to get children of scrollListChild.");
                    return PreHookResult.Continue;
                }

                var panelChild2Mo = scrollListChildChildrenArray.GetValue(2) as ManagedObject;
                if (panelChild2Mo == null)
                {
                    API.LogWarning("Child at index 2 of scrollListChild is null or not a ManagedObject.");
                    return PreHookResult.Continue;
                }
                var panelChild2 = panelChild2Mo.As<via.gui.Panel>();
                if (panelChild2 == null)
                {
                    API.LogWarning("Child at index 2 of scrollListChild is not a via.gui.Panel.");
                    return PreHookResult.Continue;
                }

                // (Optional) Cast it to a typed proxy so you can easily configure it before attaching
                via.gui.Element? newText = null;
                via.gui.Control? newPanel = null;


                newText = textChild.duplicate("e_txt_0");
                newText.Priority = 1;
                var te = ManagedProxy<via.gui.Text>.Create(newText);
                te.Message = "This is a test message for the new text child!";
                // newText.Message = "This is a test message for the new text child!";
                newPanel = panelChild2.duplicate("othericon");
                // CompareGUIProperties(textChildMo, newChildMo);
                // (newText as via.gui.Text).Message = "fkdajsf;ldksajf;ldaskjfdas <ICON KeyQ>";
                var newPos = via.vec3.REFType.CreateValueType().As<via.vec3>();
                newPos.x = 320;
                newPos.y = 0;
                newPos.z = 0;
                newPanel.Position = newPos;

                var childOfNewPanelMo = (newPanel as IObject)?.Call("getChildren(System.Type)", controlType) as ManagedObject;
                var childOfNewPanelArray = childOfNewPanelMo?.As<_System.Array>();

                var childOfNewPanel = childOfNewPanelArray?.GetValue(0) as ManagedObject;
                if (childOfNewPanel != null)
                {
                    var childOfNewPanelText = childOfNewPanel.As<via.gui.Text>();
                    if (childOfNewPanelText != null)
                    {
                        childOfNewPanelText.Message = "<INPT id=\"UIRemove\" type=\"d\">";
                    }
                }

                if (newText == null)
                {
                    API.LogWarning("Failed to duplicate child for injection.");
                }

                // textChild.remove();


                if (newPanel == null)
                {
                    API.LogWarning("Failed to create new via.gui.Panel.");
                    return PreHookResult.Continue;
                }

                // CompareGUIProperties(textChildMo, newChildMo);
                var res1 = selectItem.addChild(newText);
                API.LogInfo($"  -> Added new child 1: {res1}");

                var res2 = scrollListChild.addChild(newPanel);
                API.LogInfo($"  -> Added new child 2: {res2}");



            }
        }
        catch (Exception ex)
        {
            API.LogError($"Error inspecting UIPartsSpin: {ex.Message}");
        }

        return PreHookResult.Continue;
    }
}