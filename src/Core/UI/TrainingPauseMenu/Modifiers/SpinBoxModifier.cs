using SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_TMP.Core.UI.TrainingPauseMenu.Modifiers;

// wrapper class around MenuDataArrayModifier to handle the specific case of modifying a spinbox from the training pause menu.
public class SpinBoxModifier : ModifierWrapper
{
    private readonly TrainingDataArrayModifier _arrayModifier;

    public SpinBoxModifier(app.training.TrainingMenuData parentMenu, List<app.training.TrainingMenuData> newElements, SpinBoxDispatcher.SpinInfoLocation location, SpinBoxDispatcher.SpinBoxInitDelegate spinBoxInitCallback)
    {
        // apply the base arrayModifier to add the new elements to the parent menu
        _arrayModifier = new TrainingDataArrayModifier(parentMenu, newElements);

        // register the init callback for the spinbox
        SpinBoxDispatcher.RegisterCustomSpinBoxInitialization(location, spinBoxInitCallback);
    }

    public SpinBoxModifier(app.training.TrainingMenuData parentMenu, List<app.training.TrainingMenuData> newElements, int[] orderElements, SpinBoxDispatcher.SpinInfoLocation location, SpinBoxDispatcher.SpinBoxInitDelegate spinBoxInitCallback)
    {
        // apply the base arrayModifier to add the new elements to the parent menu
        _arrayModifier = new TrainingDataArrayModifier(parentMenu, newElements, orderElements);

        // register the init callback for the spinbox
        SpinBoxDispatcher.RegisterCustomSpinBoxInitialization(location, spinBoxInitCallback);
    }

    public override void Restore()
    {
        _arrayModifier.Restore();
    }
}