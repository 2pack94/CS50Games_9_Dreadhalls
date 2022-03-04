using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

// Scriptable Objects: https://docs.unity3d.com/Manual/class-ScriptableObject.html
// Don't exist on a GameObject level like MonoBehaviour, but on a project level.
// Scriptable Object members don't reset when the scene changes or when leaving play mode in the editor.
// The values get reset when leaving the build version of the game however.
// Scriptable Objects can be used as Data Containers to share data across scenes.
// This is a better and more flexible alternative to static variables or singletons.
// It's also possible to create multiple instances with their own copy of the data with ScriptableObject.CreateInstance().
// This script is the template and an asset can be created from it with the right-click menu
// in the project window (see CreateAssetMenu attribute).

[CreateAssetMenu(menuName = "Game Manager")]
public class GameManager : ScriptableObject
{
    // Level number that gets incremented when loading the Play scene.
    public int levelNr;

    // Runs before first scene load (needs to be defined inside a non-MonoBehaviour class)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void OnBeforeSceneLoadRuntimeMethod()
    {
        Application.targetFrameRate = 60;
    }

    // actionName: Name of the Action in the Player Input Action Asset
    // playerInput: Input Action Asset for the Player Input
    // return: List of strings that contain the name of the Button that triggers the action (binding).
    public static IEnumerable<string> GetButtonsForAction(string actionName, PlayerInput playerInput)
    {
        List<string> buttonList = new();
        if (!playerInput)
            return buttonList;

        // Find the Action across all Action Maps
        InputAction inputAction = playerInput.actions.FindAction(actionName);

        if (inputAction == null)
            return buttonList;

        foreach (var binding in inputAction.bindings)
        {
            // An Input Control Scheme is for example "Mouse&Keyboard"
            // A binding group is used to divide bindings into InputControlSchemes.
            // Return only buttons that belong to the current Control Scheme.
            string[] bindingGroups = binding.groups.Replace(" ", "").Split(";");
            if (bindingGroups.Contains(playerInput.currentControlScheme))
            {
                buttonList.Add(binding.ToDisplayString());
            }
        }

        return buttonList;
    }
}
