using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_TMP.Modules;
// FIXME: remove this eventually

public class TestingRefactor : ITrainingModePlusModule
{
    private static readonly Guid TEST_ROW_BUTTON = Guid.NewGuid();
    private static readonly Guid TOGGLE_OTHER_BUTTON = Guid.NewGuid();
    private static readonly Guid TEST_TAB = Guid.NewGuid();

    private static readonly Guid AA_SPINNER = Guid.NewGuid();
    private static readonly Guid TEST_OPTION_BUTTON2 = Guid.NewGuid();
    private readonly List<IUIModificationRequest> _optionsModificationRequests = [];

    private readonly List<IUIModificationRequest> _pauseModificationRequests = [];

    private bool _buttonVisible = true;

    private PauseMenuAppendElementsRequest? _randomizerButtonAppendRequest;

    private TestingRefactor()
    {
    }

    public static TestingRefactor Instance { get; } = new();

    public void Init()
    {
    }

    public void Unload()
    {
        try
        {
            foreach (var request in _pauseModificationRequests) PauseMenuManager.UnregisterModification(request);
        }
        catch (Exception ex)
        {
            API.LogError($"Error unregistering UI modifications during unload: {ex.Message}");
        }
    }
}