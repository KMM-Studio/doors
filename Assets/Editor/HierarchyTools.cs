using UnityEngine;
using UnityEditor;

public class HierarchyTools
{
    // This adds the button to the right-click menu in the Hierarchy
    [MenuItem("GameObject/Go For Milk (Orphan Children)", false, 0)]
    static void GoForMilk()
    {
        // Loop through every object you currently have selected
        foreach (GameObject selectedObj in Selection.gameObjects)
        {
            Transform parentTransform = selectedObj.transform;

            // Loop backward through the children so the index doesn't break as we remove them
            for (int i = parentTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = parentTransform.GetChild(i);
                
                // Move the child out of the parent (makes it a sibling of the parent). 
                // The 'Undo' part lets you Ctrl+Z to reverse it.
                Undo.SetTransformParent(child, parentTransform.parent, "Go For Milk");
            }
        }
    }
}