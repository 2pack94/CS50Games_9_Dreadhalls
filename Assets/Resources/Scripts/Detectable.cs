using UnityEngine;

// Makes a GameObject detectable by enemies that have a DetectionModule Component.
public class Detectable : MonoBehaviour
{
    [Tooltip("Points that are used as targets for enemy target-detection raycasts.")]
    public Transform[] detectionTargetPoints;
}
