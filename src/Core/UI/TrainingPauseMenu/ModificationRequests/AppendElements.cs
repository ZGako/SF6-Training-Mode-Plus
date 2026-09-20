

using System.Diagnostics;

using via.gui;

namespace SF6_TMP.Core.UI.TrainingPauseMenu.ModificationRequests;


// TODO rewrite this whole thing to output a list of lists with the appended elements.
// one other thing is to define a convention for saying "append first and append last", since those aren't necessarily
// attached to the original array.

/// <summary>
/// 
/// </summary>
/// <param name="newElements">This parameter contains the list of new elements to append with the index of the element they'll attach to.
/// <para>FOR THIS SPECIFIC CASE</para> we use indexing starting from 1. So if you put 0, you want the element at the top of the page. 
/// -1 is the convetion for "add to the end" </param>
public class AppendElements(List<(int, UICustomElementNode)> newElements) : SingleUseModificationRequest
{

    // sort the new elements by their order index
    private List<(int, UICustomElementNode)> NewElements { get; init; } = newElements;

    public List<app.training.TrainingMenuData>[] GetAppendList(int originalArrayLength)
    {
        List<app.training.TrainingMenuData>[] appendList = new List<app.training.TrainingMenuData>[originalArrayLength + 1];

        foreach (var (index, elementNode) in NewElements)
        {
            var actualIndex = index == -1 ? originalArrayLength : index; // if index is -1, append to the end
            if (appendList[actualIndex] == null)
            {
                appendList[actualIndex] = [];
            }

            // Assuming elementNode has a method to get the TrainingMenuData instance
            var trainingMenuData = elementNode.Build();
            appendList[actualIndex].Add(trainingMenuData);
        }

        return appendList;
    }

    public void Reset()
    {
        for (int i = 0; i < NewElements.Count; i++)
        {
            var (_, elementNode) = NewElements[i];
            elementNode.Clear();
        }
    }

    public override AppendElements Clone()
    {
        var clonedElements = NewElements.Select(e => (e.Item1, e.Item2)).ToList();
        return new AppendElements(clonedElements);
    }
}