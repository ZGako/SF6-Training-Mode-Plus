
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
        FunctionDispatcherRequest dispatcherRequest = new((_, _, _) => API.LogInfo("Less go"));
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
}