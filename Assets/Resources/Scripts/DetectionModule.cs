using System;
using System.Linq;
using UnityEngine;

// Cast rays from a source point (e.g. from the position of the eyes) to all detection target points
// of all GameObjects with a Detectable Component. Store the closest found target to be processed by other Components.
// This Component does not include detection by sound, which could be an useful addition. 
public class DetectionModule : MonoBehaviour
{
    [Tooltip("The point representing the source of target-detection raycasts.")]
    public Transform detectionSourcePoint;
    [Tooltip("Length of the raycasts used for target detection.")]
    [Min(0f)]
    public float visionDetectionRadius = 10f;
    [Tooltip("Only targets within this angle measured from the z-axis of detectionSourcePoint can be detected.")]
    [Range(0f, 180f)]
    public float visionHalfAngle = 30f;
    [Tooltip("If the distance from detectionSourcePoint to a target point is smaller than this, " +
        "ignore visionHalfAngle and detect the target anyways.")]
    [Min(0f)]
    public float nearFieldDetectionRadius = 1.5f;
    [Tooltip("Time in seconds before abandoning a known target that is not seen anymore.")]
    // Useful so an enemy does not abandon the player if he jumps, etc.
    public float detectedTargetTimeout = 2f;
    // Colliders in this GameObject (and in its children).
    private Collider[] selfColliders;
    // Get all detectable GameObjects before the game starts (There will only be the player).
    private Detectable[] detectables;
    // Reference to a target that was detected. null if no target was detected or target was lost.
    private Detectable detectedTarget;
    // Store the last detected target to know when a target was changed.
    private Detectable lastDetectedTarget;
    // true when a target is detected and detectedTargetTimeout is not counting down.
    private bool isSeeingTarget = false;
    // Time when a target was last detected. Used for detectedTargetTimeout count down.
    private float timeLastSeenTarget = Mathf.NegativeInfinity;
    // callback invoked when a target was detected or switched.
    public event EventHandler<Detectable> OnDetect;
    // callback invoked when a target was lost (detectedTarget was set to null).
    public event EventHandler<Detectable> OnLostTarget;

    void Start()
    {
        // Find all active loaded Components of that type
        detectables = UnityEngine.Object.FindObjectsOfType<Detectable>();
        selfColliders = GetComponentsInChildren<Collider>();
    }

    void Update()
    {
        HandleTargetDetection();
    }

    // Use raycasts to find detectedTarget from the list of detectables.
    // Do not leave the loop immediately if a detectable was found.
    // Loop through everything to choose the detectable with the smallest distance as detectedTarget.
    void HandleTargetDetection()
    {
        // Set detectedTarget to null if lost vision to target and after detectedTargetTimeout.
        if (detectedTarget && !isSeeingTarget && (Time.time - timeLastSeenTarget) > detectedTargetTimeout)
        {
            detectedTarget = null;
        }

        // Find the closest target point amongst all detectables.
        isSeeingTarget = false;
        float closestDistance = Mathf.Infinity;
        foreach (Detectable detectable in detectables)
        {
            foreach (Transform targetPoint in detectable.detectionTargetPoints)
            {
                float distance = Vector3.Distance(detectionSourcePoint.position, targetPoint.position);
                if (distance > visionDetectionRadius || distance > closestDistance)
                    continue;

                // Get Raycast direction
                Vector3 raycastNormal = (targetPoint.position - detectionSourcePoint.position).normalized;
                // Cast a ray on all layers from detectionSourcePoint with length visionDetectionRadius.
                // Return a hit for all colliders intersecting the ray.
                RaycastHit[] hits = Physics.RaycastAll(detectionSourcePoint.position,
                    raycastNormal, visionDetectionRadius, -1, QueryTriggerInteraction.Ignore);
                // Find the closest valid hit.
                RaycastHit closestValidHit = new();
                closestValidHit.distance = Mathf.Infinity;
                bool foundValidHit = false;
                foreach (var hit in hits)
                {
                    if (!selfColliders.Contains(hit.collider) && hit.distance < closestValidHit.distance &&
                        (hit.distance < nearFieldDetectionRadius ||
                        Vector3.Angle(detectionSourcePoint.forward, raycastNormal) < visionHalfAngle))
                    {
                        closestValidHit = hit;
                        foundValidHit = true;
                    }
                }
                if (!foundValidHit)
                    continue;

                // Check if the closest valid hit was a Detectable or an obstruction.
                Detectable hitDetectable = closestValidHit.collider.GetComponentInParent<Detectable>();
                if (hitDetectable == detectable)
                {
                    isSeeingTarget = true;
                    closestDistance = distance;
                    detectedTarget = hitDetectable;
                    timeLastSeenTarget = Time.time;
                }
            }
        }

        // Invoke callback if detected a new target or when switched the target.
        if (detectedTarget && lastDetectedTarget != detectedTarget)
        {
            OnDetect?.Invoke(this, detectedTarget);
        }
        // Invoke callback if lost target (not when switched target).
        else if (!detectedTarget && lastDetectedTarget)
        {
            OnLostTarget?.Invoke(this, lastDetectedTarget);
        }
        lastDetectedTarget = detectedTarget;
    }

    // Called in the Scene view of the Editor to visualize the vision range.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        float rayLength = visionDetectionRadius;
        Gizmos.DrawRay(detectionSourcePoint.position,
            Quaternion.AngleAxis(visionHalfAngle, detectionSourcePoint.up) * detectionSourcePoint.forward * rayLength);
        Gizmos.DrawRay(detectionSourcePoint.position,
            Quaternion.AngleAxis(-visionHalfAngle, detectionSourcePoint.up) * detectionSourcePoint.forward * rayLength);
        Gizmos.DrawRay(detectionSourcePoint.position,
            Quaternion.AngleAxis(visionHalfAngle, detectionSourcePoint.right) * detectionSourcePoint.forward * rayLength);
        Gizmos.DrawRay(detectionSourcePoint.position,
            Quaternion.AngleAxis(-visionHalfAngle, detectionSourcePoint.right) * detectionSourcePoint.forward * rayLength);
    }
}
