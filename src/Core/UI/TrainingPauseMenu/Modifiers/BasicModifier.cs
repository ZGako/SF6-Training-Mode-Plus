namespace SF6_TMP.Core.UI.TrainingPauseMenu.Modifiers;

// wrapper class around MenuDataArrayModifier to handle the specific case of modifying a spinbox from the training pause menu.
public class BasicModifier : ModifierWrapper
{
    private readonly TrainingDataArrayModifier _arrayModifier;

    public BasicModifier(app.training.TrainingMenuData parentMenu, List<app.training.TrainingMenuData> newElements)
    {
        // apply the base arrayModifier to add the new elements to the parent menu
        _arrayModifier = new TrainingDataArrayModifier(parentMenu, newElements);
    }

    public BasicModifier(app.training.TrainingMenuData parentMenu, List<app.training.TrainingMenuData> newElements, int[] orderElements)
    {
        // apply the base arrayModifier to add the new elements to the parent menu
        _arrayModifier = new TrainingDataArrayModifier(parentMenu, newElements, orderElements);
    }

    public override void Restore()
    {
        _arrayModifier.Restore();
    }
}