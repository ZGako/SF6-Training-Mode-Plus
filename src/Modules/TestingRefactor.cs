using SF6_Plugin_Core.UI.InjectionShared;
using SF6_Plugin_Core.UI.Localization;
using SF6_Plugin_Core.UI.TrainingPauseMenu.Core;

namespace SF6_TMP.Modules;
// FIXME: remove this eventually

public class TestingRefactor : ITrainingModePlusModule
{
    private readonly List<IUIModificationRequest> _pauseModificationRequests = [];

    private bool _buttonVisible = true;

    private PauseMenuAppendElementsRequest? _randomizerButtonAppendRequest;

    private Guid? _shortcutGuid;

    private TestingRefactor()
    {
    }

    public static TestingRefactor Instance { get; } = new();

    public void Init()
    {
        _shortcutGuid = ShortcutManager.RegisterShortcut(
            new ShortcutManager.CustomShortcutNode
            {
                ItemMessage = CustomMessage.FromKey("RandomizerButtonTitle"),
                GuideMessage = CustomMessage.FromKey("RandomizerButtonDescription"),
                IsKeyInput = true
                // ShortcutType = existing type → adds a duplicate entry instead of a new type
            },
            () => { }, () => { }, () => { },
            liveNode =>
            {
                /* save liveNode.Button / Key / ... to your config */
                API.LogInfo($"saving node values {liveNode}");
            });
    }

    public void Unload()
    {
        try
        {
            if (_shortcutGuid != null)
                ShortcutManager.UnregisterShortcut(_shortcutGuid.Value);
            foreach (var request in _pauseModificationRequests) PauseMenuManager.UnregisterModification(request);
        }
        catch (Exception ex)
        {
            API.LogError($"Error unregistering UI modifications during unload: {ex.Message}");
        }
    }
}