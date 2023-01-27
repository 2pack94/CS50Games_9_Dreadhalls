using System;
using UnityEngine;

// Describes a living Entity. All entities that can die should use this component.
public class Actor : MonoBehaviour
{
    // callback invoked when falling below the death plane
    public event EventHandler<Type> OnFallDown;
    [System.NonSerialized]
    public bool isDead = false;
    // y-coordinate of the death plane. All living entities should be killed when falling below it.
    private readonly float deathPlaneY = -10f;

    void Update()
    {
        if (transform.position.y < deathPlaneY && !isDead)
        {
            OnFallDown?.Invoke(this, null);
        }
    }

    // Call this function every frame to spherically interpolate transform.rotation, so the GameObject looks towards
    // a target point.
    public static void OrientTowards(Vector3 targetPosition, Transform transform)
    {
        // Specifies the rate at which transform.rotation changes every frame.
        float orientationSpeed = 10f;
        Vector3 lookDirection = Vector3.ProjectOnPlane(targetPosition - transform.position, Vector3.up).normalized;
        if (lookDirection.magnitude != 0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * orientationSpeed);
        }
    }
}
