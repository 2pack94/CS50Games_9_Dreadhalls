using UnityEngine;
using UnityEngine.SceneManagement;

// Attach this component to a GameObject to prevent that it (and its children) gets destroyed when loading another scene.
// Also make the GameObject (and its children) a singleton. That means only 1 instance of it can exist at a time.
public class DontDestroy : MonoBehaviour
{
    void Awake() {
        // Destroy GameObject if another copy of it already exists. 
        // Find copies by Tag. The GameObject needs a unique tag for this to work.
        // This approach works when instantiating the GameObject via code or via the Editor.
        GameObject[] objs = GameObject.FindGameObjectsWithTag(this.tag);
        if (objs.Length > 1)
        {
            Destroy(this.gameObject);
        }
        else
        {
            // https://docs.unity3d.com/ScriptReference/Object.DontDestroyOnLoad.html
            // This method moves the GameObjects to a special scene called "DontDestroyOnLoad".
            // This scene will then always be present also on scene change.
            DontDestroyOnLoad(this.gameObject);
        }
    }

    // Move all GameObjects with a DontDestroy component back to the current scene.
    public static void UndoDontDestroyOnLoad()
    {
        foreach(var obj in GameObject.FindObjectsOfType<DontDestroy>())
        {
            SceneManager.MoveGameObjectToScene(obj.gameObject, SceneManager.GetActiveScene());
        }
    }
}
