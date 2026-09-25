
using SF6_TMP.Core;
using SF6_TMP.Core.UI;
using SF6_TMP.Core.UI.TrainingPauseMenu;
using SF6_TMP.Core.UI.TrainingPauseMenu.CustomElements;
using SF6_TMP.Core.UI.TrainingPauseMenu.DispatchRequests;
using SF6_TMP.Core.UI.TrainingPauseMenu.ModificationRequests;
using System.Runtime.InteropServices;

namespace SF6_TMP.TrainingModePlus.Modules;

public class TestingPrefab : ITrainingModePlusModule
{
    public static TestingPrefab Instance { get; private set; } = new TestingPrefab();

    private TestingPrefab() { }

    public void Init()
    {
        var prefabManager = GameSingletonRegistry.UIPrefabManager;
        if (prefabManager == null)
        {
            API.LogError("UIPrefabManager is not initialized. Cannot register custom prefab.");
            return;
        }
    }

    public void Unload()
    {
    }
}