
using SF6_Plugin_Core;
using SF6_Plugin_Core.UI;
using SF6_Plugin_Core.UI.TrainingPauseMenu;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.DispatchRequests;
using SF6_Plugin_Core.UI.TrainingPauseMenu.ModificationRequests;

namespace SF6_TMP.Modules;

public class TestingPrefab : ITrainingModePlusModule
{
    public static TestingPrefab Instance { get; private set; } = new TestingPrefab();

    private TestingPrefab() { }

    ManagedObject? _gameObjectMo;

    public void Init()
    {

        API.LogInfo("initializing TestingPrefab module...");

        try
        {
            var uiagentManager = GameSingletonRegistry.UIAgentManager;
            if (uiagentManager == null)
            {
                API.LogError("UIAgentManager is not initialized. Cannot register custom prefab.");
                return;
            }

            var resMgr = API.GetResourceManager();
            var prefabRes = resMgr.CreateResource("via.PrefabResource", "Product/GUI/gm/ui11200/ui11254/ui11254.pfb");
            if (prefabRes == null)
            {
                API.LogError("Failed to load prefab resource.");
                return;
            }

            // 2. Create the holder and grab the active scene's folder
            var prefabHolder = prefabRes.CreateHolder("via.PrefabResourceHolder")?.As<via.PrefabResourceHolder>();

            if (prefabHolder == null)
            {
                API.LogError("Failed to create prefab holder.");
                return;
            }

            // create a Prefab and instantiate it
            var prefabMo = via.Prefab.REFType.CreateInstance(0);

            var prefab = prefabMo.As<via.Prefab>();
            prefab.Path = prefabHolder.ResourcePath;

            if (prefab == null)
            {
                API.LogError("Failed to create prefab.");
                return;
            }

            API.LogInfo("prefab address is: " + prefabMo.GetAddress());

            // 0,0,0 position required for parameter
            var pos = via.vec3.REFType.CreateValueType().As<via.vec3>();

            _gameObjectMo = (prefab as IObject)?.Call("instantiate(via.vec3)", pos) as ManagedObject;
            if (_gameObjectMo == null)
            {
                API.LogError("Failed to instantiate prefab.");
                return;
            }
            _gameObjectMo.Globalize();

            // after having created the prefab, dispose of the resource to not leak memory (the holder is a managed object that wasn't globalized, so the GC will clean it up when it runs)
            prefabRes.Dispose();

            API.LogInfo($"Instantiated prefab at address: {_gameObjectMo.GetAddress()}");

            // var uiAgentMo = _gameObjectMo.Call("getComponent(System.Type)", app.UIAgent.REFType.RuntimeType.As<_System.Type>()) as ManagedObject;

            var uiAgentComponent = _gameObjectMo.As<via.GameObject>()?.getComponent(app.UIAgent.REFType.RuntimeType.As<_System.Type>());

            var uiAgent = ManagedProxy<app.UIAgent>.Create(uiAgentComponent);

            if (uiAgentComponent == null)
            {
                API.LogError("Failed to get UIAgent component from the instantiated prefab.");
                return;
            }

            var stateManager = uiAgent._StateManager;

            var showState = app.UIAgent.State.Show.REFType.CreateInstance(0);

            stateManager._Next = showState.As<app.UIStateBase>();

            // uiAgent.onDisable();
            // Assuming 'uiPartsManager' is your given UIPartsManager instance
            var uiPartsManager = uiAgent._PartsManager;

            // 1. Grab index 0 from the parts list and cast it to app.UIPartsScrollList
            var firstListItem = uiPartsManager._List[0];
            var scrollListPart = (firstListItem as IObject)?.As<app.UIPartsScrollList>();
            if (scrollListPart == null)
            {
                API.LogWarning("Failed to resolve app.UIPartsScrollList at List[0].");
                return;
            }

            // 2. Access the internal _List field and cast to via.gui.ScrollList
            var guiScrollList = (scrollListPart._List as IObject)?.As<via.gui.ScrollList>();
            if (guiScrollList == null)
            {
                API.LogWarning("Failed to resolve via.gui.ScrollList from _List.");
                return;
            }

            // Prepare the RuntimeType argument required for the getChildren native call
            var controlType = via.gui.PlayObject.REFType.RuntimeType;

            // 3. Retrieve the children of the via.gui.ScrollList as a managed array, then get index 19
            var scrollChildrenObj = (guiScrollList as IObject)?.Call("getChildren(System.Type)", controlType) as ManagedObject;
            var scrollChildrenArray = scrollChildrenObj?.As<_System.Array>();

            var selectItemMo = scrollChildrenArray?.GetValue(19) as ManagedObject;
            var selectItem = selectItemMo?.As<via.gui.SelectItem>();
            if (selectItem == null)
            {
                API.LogWarning("Child at index 19 is null or not a via.gui.SelectItem.");
                return;
            }

            // 4. Retrieve the children of the via.gui.SelectItem, then extract index 4 as via.gui.Text
            var selectChildrenObj = (selectItem as IObject)?.Call("getChildren(System.Type)", controlType) as ManagedObject;
            var selectChildrenArray = selectChildrenObj?.As<_System.Array>();

            var textChildMo = selectChildrenArray?.GetValue(4) as ManagedObject;
            var targetTextElement = textChildMo?.As<via.gui.Text>();

            if (targetTextElement != null)
            {
                // targetTextElement is now a fully typed proxy. 
                // You can directly interact with its properties (e.g., targetTextElement.Message)
                API.LogInfo($"Successfully resolved via.gui.Text proxy.");

                targetTextElement.Message = "1";
            }
            else
            {
                API.LogWarning("Child at index 4 is null or not a via.gui.Text.");
            }

        }
        catch (Exception ex)
        {
            API.LogError($"Exception during TestingPrefab initialization: {ex.Message}");
            return;
        }

        API.LogInfo("Registering custom prefab with UIAgentManager...");

    }

    public void Unload()
    {
        try
        {
            var uiagentManager = GameSingletonRegistry.UIAgentManager;
            if (uiagentManager == null)
            {
                API.LogError("UIAgentManager is not initialized. Cannot register custom prefab.");
                return;
            }

            var uiAgentComponent = _gameObjectMo?.As<via.GameObject>()?.getComponent(app.UIAgent.REFType.RuntimeType.As<_System.Type>());

            var uiAgent = ManagedProxy<app.UIAgent>.Create(uiAgentComponent);

            _gameObjectMo?.Release();
            via.GameObject.destroy(_gameObjectMo?.As<via.GameObject>());

            _gameObjectMo = null;
        }
        catch (Exception ex)
        {
            API.LogError($"Exception during TestingPrefab unload: {ex.Message}");
            return;
        }
    }
}