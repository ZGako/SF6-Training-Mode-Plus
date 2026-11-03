using SF6_Plugin_Core.Services;
using SF6_Plugin_Core.UI.Helpers;
using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;
using SF6_Plugin_Core.UI.TrainingPauseMenu.CustomElements;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Requests;
using SF6_TMP.Modules.TrainingParametersAndRandomizer.Core;

namespace SF6_TMP.Modules.TrainingParametersAndRandomizer.Features;

public class BlockRandomChance : ITrainingFeature
{
    private static readonly List<int> PathToBlockRow = [2, 0, 0, 2, 4];

    private static readonly Guid BLOCK_RANDOM_CHANCE_SPINNER = Guid.NewGuid();
    private static readonly Guid BLOCK_RANDOM_CHANCE_SPINNER2 = Guid.NewGuid();
    private static readonly Guid TEST_1 = Guid.NewGuid();
    private static readonly Guid TEST_2 = Guid.NewGuid();

    [ThreadStatic] private static bool _isInitializing;

    private static readonly ThreadLocal<Queue<app.training.UIFlowTrainingMenu.Param.ViewData>> PerThreadViewData =
        new(() => new Queue<app.training.UIFlowTrainingMenu.Param.ViewData>(14));

    // since this is only affected by UI stuff, I'll assume there's no thread related race condition
    // (everything we deal with is serialized on a single thread it seems)
    private static readonly OrderedDictionary<ulong, app.training.UIFlowTrainingMenu.Param.ViewData>
        ViewDataForUIPartAddress =
            new();

    [ThreadStatic] private static int _pendingIndex;

    [ThreadStatic] private static ulong _hasPendingUpdateAddress;

    [ThreadStatic] private static bool _pendingIsAction;

    private readonly List<IUIModificationRequest> _modificationRequests = [];

    private BlockRandomChance()
    {
    }

    public static BlockRandomChance Instance { get; private set; } = new();

    public void Init()
    {
        var changeToGuidTypeReq = new FieldOverrideRequest
        {
            Type = app.training.ItemType.SPIN_ITEM_GUIDE,
            GuidIcon = app.InputAssign.Digital.Id.UITrainingOptionY
        };

        var submenuSliderNode = new UICustomElementRequestNode(BLOCK_RANDOM_CHANCE_SPINNER,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Block Random Chance"),
                new CustomMessage("hi"),
                (0, 20)));
        var functionHook = new FunctionHookRequest((_, _, index) =>
        {
            API.LogInfo($"Block Random Chance function called with parameter {index}!");
            return false;
        });
        submenuSliderNode.AddHook(functionHook);

        var submenuSliderNode2 = new UICustomElementRequestNode(BLOCK_RANDOM_CHANCE_SPINNER2,
            new SubMenuSpinnerInitializer(
                new CustomMessage("Block Random Chance fdsf"),
                new CustomMessage("his sfdsfdsfds"),
                (0, 20)));

        var appendReq = new PauseMenuAppendElementsRequest([(0, submenuSliderNode), (0, submenuSliderNode2)]);

        var optionYHook = new OptionYHookRequest((baseParam, viewData, _) =>
        {
            // elaborate check if the option is the correct one (more generalized and ensures adding options and reordering won't break anything)
            var index = viewData.Index;
            var selectedChildFuncType = viewData.Data.ChildData[index].FuncType;
            if (selectedChildFuncType != app.training.TrainingFuncType.DUMMY_GUARD_4) return false;
            GameSingletonManager.TrainingManager?.OpenMenu(app.training.TrainingManager.MenuType.SPINLIST,
                baseParam);
            GameSingletonManager.TrainingManager?.RequestSE(
                app.training.TrainingManager.SETriggerType.DECIDE_OPTION,
                0);

            return false;
        });

        try
        {
            PauseMenuManager.ModifyUI(() =>
            {
                PauseMenuManager.RegisterModification(PathToBlockRow, changeToGuidTypeReq);
                PauseMenuManager.RegisterModification(PathToBlockRow, appendReq);
                PauseMenuManager.RegisterModification([2, 0, 0, 2], optionYHook);
            });
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in BlockRandomChange: {ex.Message}");
        }

        _modificationRequests.Add(changeToGuidTypeReq);
        _modificationRequests.Add(appendReq);
        _modificationRequests.Add(optionYHook);
    }

    public void Unload()
    {
        if (_modificationRequests.Count > 0)
        {
            PauseMenuManager.ModifyUI(() =>
            {
                foreach (var request in _modificationRequests) PauseMenuManager.UnregisterModification(request);
            });

            _modificationRequests.Clear();
        }
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "Initialize", MethodHookType.Pre)]
    public static PreHookResult OnSpinListInitializePre(Span<ulong> args)
    {
        // keep track when an element is being initialized
        _isInitializing = true;

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "CreateViewData", MethodHookType.Post)]
    public static void OnCreateViewDataPost(ref ulong retval)
    {
        // we check the view data in the post, to make sure that our "custom" viewData is present
        var tm = GameSingletonManager.TrainingManager ?? throw new NullReferenceException("TrainingManager not found");

        var paramData = ((tm._UITrainingMenu_SpinList as IObject)?.GetField("_ParamData") as ManagedObject)
                        .As<app.training.UIFlowTrainingMenu_SpinList.Param>() ??
                        throw new NullReferenceException("Could not get field _ParamData of UITrainingMenu_SpinList");

        // clear the list just in case something in the flow broke
        PerThreadViewData.Value?.Clear();
        // clear the dictionary as well (could be done in Initialize, but both get called in the same parts so it doesn't really matter)
        ViewDataForUIPartAddress.Clear();

        // explicit for loop as foreach sometimes breaks
        for (var i = 0; i < paramData.ViewDataList.Count; i++)
        {
            var viewData = paramData.ViewDataList[i];
            // add all viewDataList elements, we'll let the InitList function deal with them
            PerThreadViewData.Value?.Enqueue(viewData);
        }
    }

    [MethodHook(typeof(app.UIPartsScrollList), "InitList", MethodHookType.Pre)]
    public static PreHookResult OnInitList(Span<ulong> args)
    {
        try
        {
            var address = args[1];
            // so if we are initializing
            if (_isInitializing)
            {
                if (PerThreadViewData.Value!.Count == 0) return PreHookResult.Continue;
                // pop the queue, read its values and apply it. 
                var viewData = PerThreadViewData.Value.Dequeue();
                var data = viewData.Data;
                if (data.FuncType > app.training.TrainingFuncType.MAX)
                {
                    // if it is a custom thing, then we change the args and also 
                    ViewDataForUIPartAddress.TryAdd(address, viewData);
                    // compute the difference between max and min
                    args[2] = (ulong)(data.LimitValue.y - data.LimitValue.x);
                    // TODO: fix this below by taking some initialized value or something
                    args[3] = 1; // for now keep it at 1, later on the user will determine this somehow
                }
            }
            else
            {
                // in the case where we're not initializing (meaning it is the changing focus operation) we compare the address to the dictionary we have.
                if (ViewDataForUIPartAddress.TryGetValue(address, out var viewData))
                {
                    var data = viewData.Data;
                    // compute the difference between max and min
                    args[2] = (ulong)(data.LimitValue.y - data.LimitValue.x);
                    // TODO: check if this is correct
                    args[3] = (ulong)viewData.Index;
                }
            }
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in InitList hook: {ex.Message}");
        }

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "UpdateSpinBox(System.Int32, System.Boolean)",
        MethodHookType.Pre)]
    public static PreHookResult OnUpdateSpinBoxPre(Span<ulong> args)
    {
        // args[2] corresponds to the first parameter of the method

        _pendingIndex = (int)args[2];
        _pendingIsAction = args[3] == 1;
        // use the address as a way to self determine if you have a pending something (as address of 0 is unusable)
        _hasPendingUpdateAddress = args[1];

        return PreHookResult.Continue;
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "UpdateSpinBox(System.Int32, System.Boolean)",
        MethodHookType.Post)]
    public static void OnUpdateSpinBoxPost(ref ulong retval)
    {
        if (_hasPendingUpdateAddress == 0) return;

        var param = ManagedObject.FromAddress(_hasPendingUpdateAddress)
            .As<app.training.BaseParam>();
        _hasPendingUpdateAddress = 0; // Always clear to prevent stale state on subsequent calls

        try
        {
            if (_pendingIndex > ViewDataForUIPartAddress.Count - 1) return;
            var (address, viewData) = ViewDataForUIPartAddress.GetAt(_pendingIndex);

            if (_pendingIsAction)
                // here call the training menu func
                GameSingletonManager.TrainingManager!._UIFunc.Function(viewData.Data.FuncType, param, viewData,
                    viewData.Index);

            var partsScrollList = ManagedObject.FromAddress(address).As<app.UIPartsScrollList>();
            var selectItemText = partsScrollList._List.GetGuiChild("c_selectItem").As<via.gui.SelectItem>();
            var textItem = selectItemText.GetGuiChild("e_txt_0").As<via.gui.Text>();

            var str = VM.CreateString(viewData.Index.ToString())
                      ?? throw new InvalidOperationException("Failed to create engine string.");
            str.Globalize(); // Prevents garbage collection of the newly created engine string[cite: 1]

            (textItem as IObject)?.Call("set_Message", str);
        }
        catch (Exception ex)
        {
            API.LogError($"Exception in UpdateSpinBoxPost hook: {ex.Message}");
        }
    }

    [MethodHook(typeof(app.training.UIFlowTrainingMenu_SpinList.Param), "Initialize", MethodHookType.Post)]
    public static void OnSpinListInitializePost(ref ulong retval)
    {
        _isInitializing = false;
    }
}