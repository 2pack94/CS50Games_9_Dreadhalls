using System;
using UnityEngine;
using UnityEngine.UI;

// Class to describe the type of received damage
public class DamageSource
{
    public float Damage { get; set; }
}

// This Component handles the health of a living Entity.
// Optionally a Slider that controls a UI health bar can be assigned.
// The player health bar consists of a 9-sliced sprite for the border and a solid color UI Image for the filling level.
// Setting up a sprite from an Image: https://docs.unity3d.com/Manual/Sprites.html
// 9-slicing a sprite: https://docs.unity3d.com/Manual/9SliceSprites.html
public class Damageable : MonoBehaviour
{
    [Tooltip("UI Slider that has FillRect set to the UI Image representing the health bar filling.")]
    public Slider healthBar;
    [Tooltip("Max health value.")]
    [Min(0f)]
    public float maxHealth = 100f;
    [Tooltip("Invincibility Period after taking damage in seconds.")]
    [Min(0f)]
    public float invincibilityPeriod = 0.3f;
    // Callback function invoked when taking damage.
    public event EventHandler<DamageSource> OnReceiveDamage;
    // current health value
    [System.NonSerialized]
    public float health;
    // Store the time when last received damage to check the invincibility period.
    private float receivedDamageLastTime = 0f;

    void Start()
    {
        health = maxHealth;
        SetUIMaxHealth(maxHealth);
        SetUIHealth(health);
    }

    // This method is invoked to deal damage. damageSource.Damage gets subtracted from the health.
    public void ReceiveDamage(DamageSource damageSource)
    {
        if (health > 0 && Time.time - receivedDamageLastTime > invincibilityPeriod)
        {
            health = Mathf.Max(0, health - damageSource.Damage);
            SetUIHealth(health);
            OnReceiveDamage?.Invoke(this, damageSource);
            receivedDamageLastTime = Time.time;
        }
    }

    // Set the max value of the slider. Slider values will now range from 0 to maxHealth.
    void SetUIMaxHealth(float maxHealth)
    {
        if (healthBar)
            healthBar.maxValue = maxHealth;
    }

    // After setting the slider maxValue, the health value can be used as slider value.
    void SetUIHealth(float health)
    {
        if (healthBar)
            healthBar.value = health;
    }
}
