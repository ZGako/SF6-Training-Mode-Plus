using System;
using System.Collections.Generic;

using REFrameworkNET;
using REFrameworkNET.Attributes;

namespace SF6_Training_Mode_Plus.Core.UI.TrainingPauseMenu.Modifiers;

public class TrainingDataArrayModifier : IUIDynamicModifier
{

    private readonly app.training.TrainingMenuData _parentMenu;

    private readonly app.training.TrainingMenuData_Array1D _originalArray;

    public TrainingDataArrayModifier(app.training.TrainingMenuData parentMenu, List<app.training.TrainingMenuData> newElements)
    {
        _parentMenu = parentMenu;

        // copy the original array's data to a new array to store the original state
        _originalArray = _parentMenu.ChildData;

        // create a new array
        var newArrMo = app.training.TrainingMenuData.REFType.CreateManagedArray((uint)(_originalArray.Length + newElements.Count));
        newArrMo.Globalize();

        var newArr = newArrMo.As<app.training.TrainingMenuData_Array1D>();

        // copy the original elements to the new array
        for (int i = 0; i < _originalArray.Length; i++)
        {
            newArr[i] = _originalArray[i];
        }

        // add the new elements to the new array
        for (int i = 0; i < newElements.Count; i++)
        {
            newArr[_originalArray.Length + i] = newElements[i];
        }

        // replace the original array with the new array
        _parentMenu.ChildData = newArr;
    }

    public void Restore()
    {
        // restore the original array
        if (_parentMenu != null && _originalArray != null)
        {
            _parentMenu._ChildData = _originalArray;
        }
    }
}