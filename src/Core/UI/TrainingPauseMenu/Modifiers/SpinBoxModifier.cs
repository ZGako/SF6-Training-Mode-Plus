
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using static SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Dispatchers.SpinBoxDispatcher;
using SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Dispatchers;

namespace SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Modifiers;

// wrapper class around MenuDataArrayModifier to handle the specific case of modifying a spinbox from the training pause menu.
public class SpinBoxModifier : ModifierWrapper
{
    private readonly TrainingDataArrayModifier _arrayModifier;

    public SpinBoxModifier(app.training.TrainingMenuData parentMenu, List<app.training.TrainingMenuData> newElements, SpinInfoLocation location, SpinBoxInitDelegate spinBoxInitCallback)
    {
        // apply the base arrayModifier to add the new elements to the parent menu
        _arrayModifier = new TrainingDataArrayModifier(parentMenu, newElements);

        // register the init callback for the spinbox
        RegisterCustomSpinBoxInitialization(location, spinBoxInitCallback);
    }

    public SpinBoxModifier(app.training.TrainingMenuData parentMenu, List<app.training.TrainingMenuData> newElements, int[] orderElements, SpinInfoLocation location, SpinBoxInitDelegate spinBoxInitCallback)
    {
        // apply the base arrayModifier to add the new elements to the parent menu
        _arrayModifier = new TrainingDataArrayModifier(parentMenu, newElements, orderElements);

        // register the init callback for the spinbox
        RegisterCustomSpinBoxInitialization(location, spinBoxInitCallback);
    }

    public override void Restore()
    {
        _arrayModifier.Restore();
    }
}