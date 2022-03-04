using UnityEngine;

// Represents a weapon that spawns a hitbox to deal damage to any GameObject with the Damageable Component.
public class Weapon : MonoBehaviour
{
    [Tooltip("Amount of damage to deal on every attack.")]
    [Min(0f)]
    public float attack = 1f;
    [Tooltip("Length of the attack hitbox in forward direction (local z-axis of GameObject).")]
    [Min(0f)]
    public float attackRange = 1f;
    [Tooltip("Length of the attack hitbox in right direction (local x-axis of GameObject).")]
    [Min(0f)]
    public float attackRangeWidth = 1f;
    [Tooltip("Length of the attack hitbox in up direction (local y-axis of GameObject).")]
    [Min(0f)]
    public float attackRangeLength = 1f;
    // Hitbox extends (half edge size) calculated from attack range parameters.
    public Vector3 HitboxExtends { get; set; }
    // Hitbox position calculated from the GameObject position.
    // The GameObject that has this Component attached should be a child of the actual Entity.
    // It should be positioned from where the hitbox should be spawned.
    public Vector3 HitboxPosition { get; set; }
    // Hitbox rotation depending on the GameObject rotation.
    public Quaternion HitboxRotation { get; set; }

    // Returns true if the other GameObject is within attackRange.
    // The margin (positive number) is used so the other GameObject must be closer by this amount before returning true.
    // This is useful so enemies come a little closer before attacking to ensure that the attack hits when performed.
    public bool IsInAttackRange(GameObject otherObject, float margin = 0)
    {
        // If a collider is found measure the distance from this position to the collider.
        // Otherwise measure the distance between both GameObject positions.
        Collider otherCollider = otherObject.GetComponentInChildren<Collider>();
        if (otherCollider)
        {
            Vector3 closestPoint = otherCollider.ClosestPointOnBounds(transform.position);
            float distance = Vector3.Distance(closestPoint, transform.position);
            if (distance <= attackRange - margin)
                return true;
        }
        else if (Vector3.Distance(otherObject.transform.position, transform.position) <= attackRange - margin)
        {
            return true;
        }
        return false;
    }

    // Spawn a hitbox and deal damage to Damageable components if intersecting the hitbox.
    public void Attack()
    {
        // The hitbox will have a length of attackRange in local z-axis direction starting from the GameObject position.
        HitboxExtends = new(attackRangeWidth / 2, attackRangeLength / 2, attackRange / 2);
        HitboxPosition = transform.position + transform.forward * attackRange / 2;
        HitboxRotation = transform.rotation;
        
        // Spawn the hitbox and get all intersecting colliders.
        // Optionally a layermask is used here to only check collisions on the same layer as the GameObject.
        // https://docs.unity3d.com/Manual/Layers.html
        Collider[] hitColliders = Physics.OverlapBox(HitboxPosition, HitboxExtends, HitboxRotation,
            LayerMask.GetMask(LayerMask.LayerToName(gameObject.layer)));
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject)
                continue;
            
            Damageable damageable = hitCollider.gameObject.GetComponent<Damageable>();
            if (damageable)
            {
                DamageSource damageSource = new() {Damage = attack};
                damageable.ReceiveDamage(damageSource);
            }
        }
    }
}
