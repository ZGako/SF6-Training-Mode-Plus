
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

    public void Init()
    {
        OnActionDispatcherInstruction dispatcherRequest = new((baseParam, viewData, rowIndex) => API.LogInfo("Less go"));
        UICustomElementNode newElement = new("TestRowButton",
                                            new RowButtonInitializer(new CustomMessage("Restart Battle and Randomize"),
                                                                     new CustomMessage("Apply current settings, randomizing the appropriate settings, and restart the battle.")));
        newElement.AddDispatcher(dispatcherRequest);

        AppendElements appendRequest = new([(1, newElement)]);

        try
        {
            PauseMenuManager.RegisterModification([1], appendRequest);
        }
        catch (Exception ex)
        {
            API.LogError($"Error registering modification: {ex.Message}");
        }
        _modificationRequests.Add(appendRequest);

        PauseMenuManager.RebuildUI();

        API.LogInfo("TestingRefactor module initialized!");
    }

    public void Unload()
    {

        try
        {
            foreach (var request in _modificationRequests)
            {
                PauseMenuManager.UnregisterModification(request);
            }
            PauseMenuManager.RebuildUI();
        }
        catch (Exception ex)
        {
            API.LogError($"Error rebuilding UI during unload: {ex.Message}");
        }
    }
}