using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using StarterAssets;

// Handles death and sound effects of the player. The player movement, collision and camera is handled by
// the FirstPersonController component from the Unity asset store package.

[RequireComponent(typeof(FirstPersonController))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(Actor))]
public class PlayerController : MonoBehaviour
{
    [Tooltip("UI Image that gets faded in when the player dies.")]
    public Image fadeOutRect;
    [Tooltip("Audio that is played when jumping.")]
    public AudioClip jumpSfx;
    [Tooltip("Audio that is played when landing on the ground.")]
    public AudioClip landSfx;
    [Tooltip("Audio that is played randomly when getting hit.")]
    public AudioClip[] hitSfx;
    [Tooltip("Audio that is played when dying.")]
    public AudioClip deathSfx;
    [Tooltip("Audio that is played periodically when moving.")]
    public AudioClip footstepSfx;
    [Tooltip("Interval in seconds to play footstepSfx audio when walking.")]
    [Min(0f)]
    public float footstepSfxPeriodWalking = 0.5f;
    [Tooltip("Interval in seconds to play footstepSfx audio when running.")]
    [Min(0f)]
    public float footstepSfxPeriodRunning = 0.4f;
    // CharacterController Component of this GameObject.
    private CharacterController characterController;
    // FirstPersonController Component of this GameObject.
    private FirstPersonController fpsController;
    // AudioSource Component of this GameObject that shall play the sound effects.
    private AudioSource audioSource;
    // Actor Component of this GameObject.
    private Actor actor;
    // Timer for the footstep audio
    private float footstepTimer = 0;
    // true if player is running. Used for choosing the footstep sfx period.
    private bool isRunning = false;
    // true if player was on the ground in the previous frame. Used for playing the landing sfx.
    private bool wasGrounded = true;
    // Movement direction used to check if the player is moving to play the footstep sfx.
    private Vector2 moveValue = Vector2.zero;

    void Start()
    {
        Damageable damageable = GetComponent<Damageable>();
        if (damageable)
            damageable.OnReceiveDamage += OnReceiveDamage;
        actor = GetComponent<Actor>();
        actor.OnFallDown += (object sender, Type _) => OnDeath();
        fpsController = GetComponent<FirstPersonController>();
        audioSource = GetComponent<AudioSource>();
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Footstep sfx
        if (moveValue != Vector2.zero && fpsController.Grounded)
        {
            float footstepSfxPeriod = isRunning ? footstepSfxPeriodRunning : footstepSfxPeriodWalking;
            float footstepSfxVolume = isRunning ? 2.5f : 2;
            footstepTimer += Time.deltaTime;
            if (footstepTimer > footstepSfxPeriod)
            {
                footstepTimer = 0;
                audioSource.PlayOneShot(footstepSfx, footstepSfxVolume);
            }
        }
        // Landing sfx
        if (fpsController.Grounded && !wasGrounded)
        {
            audioSource.PlayOneShot(landSfx, 2);
        }
        wasGrounded = fpsController.Grounded;
    }

    // Go back to the title screen.
    void GoToTitleScreen()
    {
        DontDestroy.UndoDontDestroyOnLoad();
        SceneManager.LoadScene("Title");
    }

    // Wait a few seconds for the death animation before ending the game.
    IEnumerator WaitForDeathAnimation()
    {
        yield return new WaitForSeconds(3);
        GoToTitleScreen();
    }

    // Called after taking damage.
    void OnReceiveDamage(object sender, DamageSource damageSource)
    {
        audioSource.PlayOneShot(hitSfx[UnityEngine.Random.Range(0, hitSfx.Length)], 1f);
        if (((Damageable)sender).health <= 0)
            OnDeath();
    }

    // Called when falling down or when killed by attacks.
    void OnDeath()
    {
        if (actor.isDead)
            return;
        actor.isDead = true;
        audioSource.PlayOneShot(deathSfx, 2);
        // Disable movement
        fpsController.MoveSpeed = 0;
        fpsController.SprintSpeed = 0;
        fpsController.JumpHeight = 0;
        // Lower the CharacterController and therefore the camera.
        if (characterController)
            characterController.height = 0.5f;
        // Tween the alpha of the CanvasRenderer of the fadeOutRect Image.
        if (fadeOutRect)
        {
            fadeOutRect.canvasRenderer.SetAlpha(0);
            fadeOutRect.color = new Color(fadeOutRect.color.r, fadeOutRect.color.g, fadeOutRect.color.b, 1);
            fadeOutRect.CrossFadeAlpha(0.3f, 0.5f, false);
        }
        StartCoroutine(WaitForDeathAnimation());
    }

    // Called on jump player input
    public void OnJump(InputValue _)
    {
        if (fpsController.Grounded)
            audioSource.PlayOneShot(jumpSfx, 2);
    }

    // Called on move player input (W, A, S, D)
    public void OnMove(InputValue value)
    {
        moveValue = value.Get<Vector2>();
    }

    // Called on sprint player input
    public void OnSprint(InputValue value)
    {
        isRunning = value.isPressed;
    }

    // Called when pressing the CloseGame button.
    void OnCloseGame(InputValue _)
    {
        GoToTitleScreen();
    }
}
