// Core usings

using SF6_TMP.Core;
using SF6_TMP.Core.UI.TrainingPauseMenu.Dispatchers;
using SF6_TMP.Core.UI.TrainingPauseMenu.ModificationRequests;

namespace SF6_TMP.Core.UI.TrainingPauseMenu;

/// <summary>
/// 
/// </summary>
public static class PauseMenuManager
{
    private static app.training.TrainingMenuData_Array1D GetUIData()
    {
        return GameSingletonRegistry.TrainingManager == null
            ? throw new InvalidOperationException("TrainingManager is not initialized. Cannot retrieve UI data.")
            : GameSingletonRegistry.TrainingManager.UIData.MenuData;
    }

    private static ManagedObject? GetUIDataMo()
    {
        var trainingManagerMo = API.GetManagedSingleton("app.training.TrainingManager");
        if (trainingManagerMo == null)
            throw new InvalidOperationException("TrainingManager is not initialized.");

        // Traverse the fields to get the ManagedObject of the array
        var uiDataMo = (trainingManagerMo as IObject)?.GetField("_UIData") as ManagedObject;
        return (uiDataMo as IObject)?.GetField("_MenuData") as ManagedObject;
    }

    private class UICachedDataNode(ManagedObject? cachedElement)
    {
        public ManagedObject? CachedElement { get; set; } = cachedElement;

        private List<(int, UICachedDataNode)> ChildNodes { get; set; } = []; // child nodes for the tree structure
        public System.Collections.Generic.IReadOnlyList<(int Index, UICachedDataNode Node)> Children => ChildNodes;

        public ReorderRequest? ReorderRequest { get; private set; } = null; // optional reorder request

        private List<AppendElements> AppendRequests { get; } = []; // list of append requests
        public System.Collections.Generic.IReadOnlyList<AppendElements> Appends => AppendRequests;

        private List<IUIDispatcherRequest> DispatcherRequests { get; } = []; // list of dispatcher requests
        public System.Collections.Generic.IReadOnlyList<IUIDispatcherRequest> Dispatchers => DispatcherRequests;

        public UICachedDataNode? this[int index]
        {
            get => GetChild(index);
            set => SetChild(index, value!);
        }

        private UICachedDataNode? GetChild(int index)
        {
            int match = ChildNodes.BinarySearch((index, null!), Comparer);
            return match >= 0 ? ChildNodes[match].Item2 : null;
        }

        private void SetChild(int index, UICachedDataNode childNode)
        {
            int match = ChildNodes.BinarySearch((index, null!), Comparer);
            if (match >= 0)
            {
                ChildNodes[match] = (index, childNode);
            }
            else
            {
                ChildNodes.Insert(~match, (index, childNode));
            }
        }

        private static readonly Comparer<(int index, UICachedDataNode node)> Comparer = Comparer<(int index, UICachedDataNode node)>.Create((a, b) => a.index.CompareTo(b.index));

        public bool RequestReorder(ReorderRequest reorderRequest)
        {
            if (ReorderRequest != null)
            {
                // A reorder request already exists; cannot add another.
                return false;
            }

            ReorderRequest = reorderRequest;
            return true;
        }

        public void ClearReorderRequest()
        {
            ReorderRequest = null;
        }

        public bool RequestAppend(AppendElements appendRequest)
        {
            AppendRequests.Add(appendRequest);
            return true;
        }

        public void ClearAppendRequest(AppendElements appendRequest)
        {
            appendRequest.Reset();
            AppendRequests.Remove(appendRequest);
        }

        public bool RequestDispatcher(IUIDispatcherRequest dispatcherRequest)
        {
            Type incomingType = dispatcherRequest.GetType();
            bool hasSameDispatcherClass = DispatcherRequests.Any(r => r.GetType() == incomingType);

            if (hasSameDispatcherClass)
            {
                // A dispatcher request of this specific concrete class already exists.
                return false;
            }

            DispatcherRequests.Add(dispatcherRequest);
            return true;
        }

        public void ClearDispatcherRequest(IUIDispatcherRequest dispatcherRequest)
        {
            dispatcherRequest.ClearDispatcher(); // Clear the dispatcher before removal
            DispatcherRequests.Remove(dispatcherRequest);
        }
    }

    public class ReorderRequest(List<int> newOrder) : IUIModificationRequest
    {
        private List<int> NewOrder { get; init; } = newOrder;

        public int GetMapping(int originalIndex)
        {
            return originalIndex < 0 || originalIndex >= NewOrder.Count
                ? throw new ArgumentOutOfRangeException(nameof(originalIndex), $"Original index {originalIndex} is out of bounds for the new order list.")
                : NewOrder[originalIndex];
        }

        public int GetInverseMapping(int index)
        {
            return index < 0 || index >= NewOrder.Count
                ? throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of bounds for the new order list.")
                : NewOrder.IndexOf(index);
        }
    }

    private static readonly Dictionary<IUIModificationRequest, List<int>> ModificationRequestPaths = []; // maps modification requests to their traversal paths in the cached data tree

    private static readonly UICachedDataNode RootNode = new(null); // root node of the cached data tree

    public static void RebuildUI()
    {
        // Clear the existing UI:
        // - Clear all dispatches
        // - Clear created custom elements

        // Clear the function type registry (the functionTypes we want them to be incremental values, so we clear them to refill them properly)
        // Messages are instead cleared by the ModificationRequests themselves, since they don't need to be refilled from scratch and thus rebuild each time the UI is rebuilt
        FunctionTypeRegistry.Clear();
        // FIXME I don't think clearing the Function dispatchers is needed since they depend on a string and ask the registry for the value
        // TrainingFunctionDispatcher.Clear();
        // FIXME spinboxdispatcher instead relies on the traversal path and thus needs to be cleared each time the UI is rebuilt
        // TODO ^ this comment is out of date, since the spinboxdispatcher now relies on the function type and index instead of the traversal path, so it doesn't need to be cleared each time the UI is rebuilt
        // SpinBoxDispatcher.Clear();

        // traverse the cached data tree as a post-order DFS

        // during the DFS, we want to keep the gameUIData half an iteration behind on each call to be able to "restore" the original element's data before applying the modification requests
        Stack<(
            UICachedDataNode Node,
            ManagedObject? CurrentElement,
            ManagedObject? ChildArray,
            int ChildIterator
        )> nodeStack = new();

        // Seed the stack. RootNode has no specific element, just the top-level MenuData array.
        nodeStack.Push((RootNode, null, GetUIDataMo(), 0));

        while (nodeStack.Count > 0)
        {
            var (currentNode, currentElementMo, childArrayMo, childIterator) = nodeStack.Pop();

            // ------------------------------------------
            // PRE-ORDER / TRAVERSAL PHASE
            // ------------------------------------------
            if (childIterator < currentNode.Children.Count)
            {
                // Push CURRENT node back, incrementing its iterator
                nodeStack.Push((currentNode, currentElementMo, childArrayMo, childIterator + 1));

                // Get next child
                var childPair = currentNode.Children[childIterator];
                int childIndex = childPair.Index;
                UICachedDataNode childNode = childPair.Node;

                // Get the child element MO from the array
                var childArraySys = childArrayMo?.As<_System.Array>();
                var childElementMo = childArraySys?.GetValue(childIndex) as ManagedObject;

                // Get the child Game Array MO
                var childGameArrayMo = (childElementMo as IObject)?.GetField("_ChildData") as ManagedObject;

                // Push CHILD node onto the stack
                nodeStack.Push((childNode, childElementMo, childGameArrayMo, 0));
            }
            // ------------------------------------------
            // POST-ORDER / PROCESSING PHASE
            // ------------------------------------------
            else
            {
                if (currentNode == RootNode) continue; // Skip root as it doesn't represent an element


                if (currentNode.CachedElement != null && currentElementMo != null)
                {

                    // Restore the original fields of the current element from the cached element before applying any modifications

                    // released the childArray of the ingame object, since we might've modified it (by creating a new one) and we don't want to leak memory when reassigning it the cached one.
                    childArrayMo?.ReleaseIfGlobalized();

                    var typeDef = app.training.TrainingMenuData.REFType;

                    ulong cachedAddress = currentNode.CachedElement.GetAddress();
                    ulong targetAddress = currentElementMo.GetAddress();

                    typeDef.GetFields().ForEach(field =>
                    {
                        if (field.IsStatic()) return;

                        try
                        {
                            // API.LogInfo($"Copying field {field.Name}");
                            if (field.Type.IsValueType())
                            {
                                uint size = field.Type.ValueTypeSize;
                                byte[] buffer = new byte[size];
                                Marshal.Copy((IntPtr)(cachedAddress + field.OffsetFromBase), buffer, 0, (int)size);
                                Marshal.Copy(buffer, 0, (IntPtr)(targetAddress + field.OffsetFromBase), (int)size);

                            }
                            else
                            {
                                object value = field.GetDataBoxed(cachedAddress, false);
                                field.SetDataBoxed(targetAddress, value, false);
                            }
                        }
                        catch (Exception ex)
                        {

                            API.LogWarning($"Failed to copy field {field.Name}: {ex.Message}");

                        }
                    });

                    // If there are no modifications, we can release the cached element to free up memory (only after we restored it, which we just did)

                    bool hasModifications = currentNode.ReorderRequest != null ||
                        currentNode.Appends.Count > 0 ||
                        currentNode.Dispatchers.Count > 0;
                    if (!hasModifications)
                    {
                        // Release logic
                        currentNode.CachedElement.Release();
                        currentNode.CachedElement = null;
                        API.LogInfo($"Released cached element at node with {currentNode.Children.Count} children as it has no modifications.");
                    }

                    var currentElement = currentElementMo.As<app.training.TrainingMenuData>();

                    // Apply the dispatcher requests to the current element.
                    foreach (var dispatcherRequest in currentNode.Dispatchers)
                    {
                        if (FunctionTypeRegistry.TryGetFunctionName((int)currentElement!.FuncType, out string? functionName))
                        {
                            dispatcherRequest.AddDispatcher(functionName!);
                        }
                        else
                        {
                            API.LogWarning($"Function type {currentElement!.FuncType} not found in registry. Cannot add dispatcher.");
                        }
                    }

                    // Gather all the appendElement requests inside a single array of lists
                    List<app.training.TrainingMenuData>[] allNewElements = new List<app.training.TrainingMenuData>[currentElement.ChildData.Length + 1];
                    // initialize the lists to avoid null reference exceptions
                    for (int i = 0; i < allNewElements.Length; i++)
                    {
                        allNewElements[i] = [];
                    }

                    foreach (var appendRequest in currentNode.Appends)
                    {
                        var newElements = appendRequest.GetAppendList(currentElement.ChildData.Length);

                        for (int i = 0; i < newElements.Length; i++)
                        {
                            var elementsForIndex = newElements[i];

                            if (elementsForIndex != null && i < allNewElements.Length)
                            {
                                allNewElements[i].AddRange(elementsForIndex);
                            }
                        }
                    }

                    // Create a new array
                    var newArrMo = app.training.TrainingMenuData.REFType.CreateManagedArray((uint)(currentElement.ChildData.Length + allNewElements.Sum(list => list.Count)));
                    newArrMo.Globalize();

                    var newArr = newArrMo.As<app.training.TrainingMenuData_Array1D>();

                    // Copy the original elements and append the new elements after each one.
                    // if there is a ReorderRequest, we reorder the original elements according to the new order whilst interleaving the new elements at their respective original indices.
                    int newIndex = 0;

                    // first we insert the elements at index 0 (which go before the first original element)
                    foreach (var newElement in allNewElements[0])
                    {
                        newArr[newIndex++] = newElement;
                    }

                    for (int originalIndex = 0; originalIndex < currentElement.ChildData.Length; originalIndex++)
                    {
                        int mappedIndex = currentNode.ReorderRequest?.GetMapping(originalIndex) ?? originalIndex;

                        // Copy the original element to the new array at the mapped index
                        newArr[newIndex++] = currentElement.ChildData[mappedIndex];

                        // Append any new elements for this original index
                        foreach (var newElement in allNewElements[originalIndex + 1])
                        {
                            newArr[newIndex++] = newElement;
                        }
                    }

                    // Replace the original array with the new array
                    currentElement.ChildData = newArr;

                }
            }
        }
    }

    public static void RegisterModification(List<int> traversalPath, IUIModificationRequest modificationRequest)
    {
        // Add the modification request to the parent node's list

        var currentNode = RootNode;
        var gameUIDataMo = GetUIDataMo();

        // traverse both trees and cache 
        for (int i = 0; i < traversalPath.Count; i++)
        {
            int index = traversalPath[i];

            var gameUIDataArray = gameUIDataMo?.As<_System.Array>() ?? throw new InvalidCastException("UIData is not a valid array. Cannot traverse.");

            if (i == traversalPath.Count - 1)
            {
                // cache the element by copying the entire element at the current index

                if (currentNode[index] == null)
                {
                    var elementCopyMo = app.training.TrainingMenuData.REFType.CreateInstance(0);
                    var typeDef = app.training.TrainingMenuData.REFType;
                    elementCopyMo.Globalize();

                    ManagedObject originalElementMo = gameUIDataArray.GetValue(index) as ManagedObject
                    ?? throw new InvalidOperationException($"Element at index {index} is null or not a ManagedObject. Cannot copy fields.");

                    ulong originalAddress = originalElementMo.GetAddress();
                    ulong copyAddress = elementCopyMo.GetAddress();

                    typeDef.GetFields().ForEach(field =>
                    {
                        if (field.IsStatic()) return;

                        try
                        {
                            // API.LogInfo($"Copying field {field.Name}");
                            if (field.Type.IsValueType())
                            {
                                uint size = field.Type.ValueTypeSize;
                                byte[] buffer = new byte[size];
                                Marshal.Copy((IntPtr)(originalAddress + field.OffsetFromBase), buffer, 0, (int)size);
                                Marshal.Copy(buffer, 0, (IntPtr)(copyAddress + field.OffsetFromBase), (int)size);

                            }
                            else
                            {
                                object value = field.GetDataBoxed(originalAddress, false);
                                field.SetDataBoxed(copyAddress, value, false);
                            }
                        }
                        catch (Exception ex)
                        {

                            API.LogWarning($"Failed to copy field {field.Name}: {ex.Message}");

                        }
                    });

                    // store the copied element in the cached node
                    currentNode[index] = new UICachedDataNode(elementCopyMo);
                }

                bool success = modificationRequest switch
                {
                    ReorderRequest reorderRequest => currentNode[index]!.RequestReorder(reorderRequest),
                    AppendElements appendRequest => currentNode[index]!.RequestAppend(appendRequest),
                    IUIDispatcherRequest dispatcherRequest => currentNode[index]!.RequestDispatcher(dispatcherRequest),
                    _ => throw new InvalidOperationException($"Unknown modification request type: {modificationRequest.GetType().Name}")
                };
                if (success) ModificationRequestPaths[modificationRequest] = traversalPath;
                else API.LogWarning($"Modification request of type {modificationRequest.GetType().Name} is already registered at index {index}. Cannot register again.");
                return;
            }

            // get the UIData note
            var nextElementMo = gameUIDataArray?.GetValue(index) as ManagedObject;
            gameUIDataMo = (nextElementMo as IObject)?.GetField("ChildData") as ManagedObject;

            // get the cached node at the current index

            // node should never be null here, but the compiler complains anyways
            if (currentNode == null)
                throw new InvalidOperationException($"Current node is null at index {index}. Cannot traverse further.");

            UICachedDataNode? childNode = currentNode[index];
            if (childNode == null)
            {
                childNode = new UICachedDataNode(null); // Replace with your actual variables
                currentNode[index] = childNode;
            }

            currentNode = childNode;
        }

    }

    public static void UnregisterModification(IUIModificationRequest modificationRequest)
    {
        // Remove the modification request from the parent node's list
        // TODO if the modification contrains a custom element creation request, unregister it as well

        if (!ModificationRequestPaths.TryGetValue(modificationRequest, out var traversalPath))
        {
            API.LogWarning($"Modification request of type {modificationRequest.GetType().Name} is not registered. Cannot unregister.");
            return;
        }

        // traverse the cached tree to find the modification request and remove it
        var currentNode = RootNode;
        for (int i = 0; i < traversalPath.Count; i++)
        {
            int index = traversalPath[i];

            if (i == traversalPath.Count - 1)
            {
                if (currentNode[index] == null)
                {
                    API.LogWarning($"Cached node at index {index} is null. Cannot unregister modification request.");
                    return;
                }

                switch (modificationRequest)
                {
                    case ReorderRequest _:
                        currentNode[index]!.ClearReorderRequest();
                        break;
                    case AppendElements appendRequest:
                        currentNode[index]!.ClearAppendRequest(appendRequest);
                        break;
                    case IUIDispatcherRequest dispatcherRequest:
                        currentNode[index]!.ClearDispatcherRequest(dispatcherRequest);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown modification request type: {modificationRequest.GetType().Name}");
                }

                return;
            }

            // traverse to the next child node
            if (currentNode[index] == null)
            {
                API.LogWarning($"Cached node at index {index} is null. Cannot traverse further.");
                return;
            }

            currentNode = currentNode[index]!;
        }

    }




}