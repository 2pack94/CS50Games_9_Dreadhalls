using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Defines the behavior of the monster that roams the maze and can chase and attack the player.

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(AudioSource))]
public class MonsterController : MonoBehaviour
{
    [Tooltip("Weapon Component used to perform attacks.")]
    public Weapon weapon;
    [Tooltip("AudioSource with a soundtrack that shall be played when the monster is chasing.")]
    public AudioSource chaseAudio;
    [Tooltip("Audio that is played when the monster enters idle state.")]
    public AudioClip[] idleSfx;
    [Tooltip("Audio that is played when the monster detected a target.")]
    public AudioClip detectedTargetSfx;
    [Tooltip("Audio that is played periodically when the monster moves.")]
    public AudioClip footstepSfx;
    [Tooltip("Speed of the NavMeshAgent when the monster is in patrol state.")]
    [Min(0f)]
    public float walkingSpeed = 2;
    [Tooltip("Speed of the NavMeshAgent when the monster is in chasing state.")]
    [Min(0f)]
    public float runningSpeed = 5;
    [Tooltip("Minimum amount of time to wait after entering idle state before patrolling again.")]
    [Min(0f)]
    public float idleWaitMin = 3;
    [Tooltip("Maximum amount of time to wait after entering idle state before patrolling again.")]
    [Min(0f)]
    public float idleWaitMax = 5;
    [Tooltip("Interval in seconds to play footstepSfx audio when patrolling.")]
    // The Sfx interval should be in sync with the walking/ running animation.
    [Min(0f)]
    public float footstepSfxPeriodWalking = 0.83f;
    [Tooltip("Interval in seconds to play footstepSfx audio when chasing.")]
    [Min(0f)]
    public float footstepSfxPeriodRunning = 0.38f;
    // Timer for the footstep audio
    private float footstepTimer = 0;
    // The AudioSource component attached to the monster GameObject is used to play the monster sfx.
    // It has 3D spatial blend set, so the audio volume decreases with greater distance according
    // to the rolloff function.
    private AudioSource audioSource;
    // Monster Animations:
    // Triggers are used as animation parameters to change animations.
    // All animations are connected to each other in the animation state machine, so an animation state
    // can be switched to any other state using the corresponding trigger.
    // The animation transitions are configured to not have an exit time, so the next animation plays immediately.
    // When transitioning, the monster motion is interpolated from the current to the next animation
    // in the configured transition duration.
    private Animator animator;
    // Hash values for the animation triggers. Using hash values instead of strings is more performant.
    private int triggerIdleHash, triggerWalkHash, triggerRunHash, triggerAttackHash;
    // Hash value for the attack animation state.
    private int attackAnimationFullNameHash;
    // NavMeshAgent Component of this GameObject.
    private NavMeshAgent agent;
    // States for the monster state machine.
    private enum State
    {
        Idle,
        Patrolling,
        Chasing,
        Attacking
    }
    // Current monster state.
    private State state = State.Idle;
    // Monster state of the previous frame.
    private State lastState = State.Idle;
    // Delegate that defines the method signature of the state transition functions.
    private delegate void StateTransition();
    // Dictionary to link the state enum to state transition functions.
    private Dictionary<State, StateTransition> Transition;
    // Optional DetectionModule of the GameObject.
    private DetectionModule detectionModule;
    // Used to store the currently detected target.
    private Detectable detectedTarget;
    // Level reference to find patrol points for the monster.
    private LevelGenerator level;

    void Start()
    {
        // Get attached components
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        detectionModule = GetComponent<DetectionModule>();
        if (detectionModule)
        {
            detectionModule.OnDetect += DetectedTarget;
            detectionModule.OnLostTarget += LostTarget;
        }
        // Set up fall down event handler. Even though the monster cannot walk off the map because the NavMeshAgent
        // prevents from leaving the NavMesh. It also cannot fall down because it doesn't have a Rigidbody.
        Actor actor = GetComponent<Actor>();
        if (actor)
            actor.OnFallDown += (object sender, Type _) => Destroy(gameObject);
        // Find LevelGenerator in the scene
        level = UnityEngine.Object.FindObjectOfType<LevelGenerator>();
        // Set up the Transition dictionary
        Transition = new();
        Transition.Add(State.Idle, TransitionIdle);
        Transition.Add(State.Patrolling, TransitionPatrolling);
        Transition.Add(State.Chasing, TransitionChasing);
        Transition.Add(State.Attacking, TransitionAttacking);
        // Set up animation hash values
        triggerIdleHash = Animator.StringToHash("triggerIdle");
        triggerWalkHash = Animator.StringToHash("triggerWalk");
        triggerRunHash = Animator.StringToHash("triggerRun");
        triggerAttackHash = Animator.StringToHash("triggerAttack");
        string attackAnimationName = "Monster_Attack_2";
        string attackAnimationFullName = animator.GetLayerName(0) + "." + attackAnimationName;
        attackAnimationFullNameHash = Animator.StringToHash(attackAnimationFullName);
        // Call the state transition function of the initial state.
        Transition[state]();
    }

    void Update()
    {
        // Monster state machine
        switch (state)
        {
        case State.Idle:
            break;
        case State.Patrolling:
            UpdateFootsteps();
            // If the agent destination is too far away, it might take a few frames to calculate the path
            // after agent.SetDestination(). When calculating the path, agent.pathPending will be true and
            // agent.hasPath will be false. When reaching the destination,
            // agent.hasPath will be set to false if agent.remainingDistance gets smaller than 0.01.
            // If the original destination is not reachable, the agent will stop at the closest reachable point.
            if (!agent.pathPending && !agent.hasPath)
                Transition[State.Idle]();
            break;
        case State.Chasing:
            // This state is entered from State.Idle or State.Patrolling after the DetectedTarget callback.
            UpdateFootsteps();
            if (detectedTarget)
            {
                agent.SetDestination(detectedTarget.transform.position);
                if (IsInAttackRange())
                    Transition[State.Attacking]();
            }
            // This section is entered after the LostTarget callback.
            // If the Monster lost its target, it still has the last seen position.
            else if (!agent.pathPending && !agent.hasPath)
            {
                Transition[State.Idle]();
            }
            break;
        case State.Attacking:
            if (detectedTarget)
            {
                // Because the NavMeshAgent is deactivated here, the monster must be manually oriented
                // towards its target.
                Actor.OrientTowards(detectedTarget.transform.position, transform);
                if (!IsInAttackRange())
                    Transition[State.Chasing]();
            }
            // Loosing the target while attacking is very rare.
            else
            {
                if (!agent.pathPending && !agent.hasPath)
                    Transition[State.Idle]();
                else
                    Transition[State.Chasing]();
            }
            break;
        }
        lastState = state;
    }

    // Callback invoked from DetectionModule Component when a target was detected.
    void DetectedTarget(object sender, Detectable detectable)
    {
        // Change state to chasing or attacking if not already in these states
        detectedTarget = detectable;
        if (state != State.Chasing && state != State.Attacking)
        {
            audioSource.PlayOneShot(detectedTargetSfx, 1.5f);
            if (IsInAttackRange())
                Transition[State.Attacking]();
            else
                Transition[State.Chasing]();
        }
    }

    // Callback invoked from DetectionModule Component when a target was lost.
    void LostTarget(object sender, Detectable detectable)
    {
        // After losing the target, the monster should still go to the last known target position.
        agent.SetDestination(detectedTarget.transform.position);
        detectedTarget = null;
    }

    // Play Footstep Sfx periodically when moving.
    void UpdateFootsteps()
    {
        if (lastState != state)
            footstepTimer = 0;
        float footstepSfxPeriod = state == State.Chasing ? footstepSfxPeriodRunning : footstepSfxPeriodWalking;
        float footstepSfxVolume = state == State.Chasing ? 1f : 0.7f;
        footstepTimer += Time.deltaTime;
        if (footstepTimer > footstepSfxPeriod)
        {
            footstepTimer = 0;
            audioSource.PlayOneShot(footstepSfx, footstepSfxVolume);
        }
    }

    // Before setting a trigger to change the animation, all triggers should be reset.
    // When a trigger is set and it could not yet be consumed (reset) by a transition it will still be active.
    // This can happen if not all animation states are connected to each other with transitions.
    void ResetAllTriggers()
    {
        foreach (var param in animator.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Trigger)
                animator.ResetTrigger(param.name);
        }
    }

    // Set a trigger to change the animation state.
    void TriggerAnimation(int triggerHash)
    {
        ResetAllTriggers();
        animator.SetTrigger(triggerHash);
    }

    // Coroutine to wait in idle state for some time.
    IEnumerator IdleWait()
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(idleWaitMin, idleWaitMax));
        // Don't transition if already transitioned to another state (e.g. chasing) in the meantime.
        if (state == State.Idle)
            Transition[State.Patrolling]();
    }

    // Coroutine that runs while in attack state.
    // The Weapon Attack method will be called periodically according to the progress of the attack animation.
    IEnumerator TryAttack()
    {
        // The animator only has the base layer.
        int layer = 0;
        // Each list in this list has 2 values that determine when the weapon attack method should be started and
        // stopped to be called in normalized time (from 0 to 1).
        // In this case the monster hits with the right arm followed by the left arm in the attack animation clip.
        // When normalizedTime starts, depends on the transition duration (it will not start from 0).
        // The number before the decimal point specifies the number of loops that the animation clip has played.
        // The number after the decimal point is the progress of the current animation clip.
        double[,] normalizedActivePeriods = {{0.2, 0.3}, {0.7, 0.8}};

        // An alternative of using this Coroutine would be using Unity animation events:
        // https://docs.unity3d.com/Manual/script-AnimationWindowEvent.html

        while (true)
        {
            if (state != State.Attacking)
                break;

            // Wait until the transition to the attack animation is finished.
            if (animator.GetCurrentAnimatorStateInfo(layer).fullPathHash == attackAnimationFullNameHash)
            {
                double animationTime = animator.GetCurrentAnimatorStateInfo(layer).normalizedTime;
                bool isInActivePeriod = false;

                for (int i = 0; i < normalizedActivePeriods.GetLength(0); i++)
                {
                    if (animationTime % 1 >= normalizedActivePeriods[i, 0] &&
                        animationTime % 1 < normalizedActivePeriods[i, 1]
                    )
                    {
                        isInActivePeriod = true;
                        break;
                    }
                }
                // The attack method will be called every frame if isInActivePeriod is true.
                // An invincibility period prevents from receiving damage every frame.
                if (isInActivePeriod && weapon)
                    weapon.Attack();
            }
            yield return null;
        }
    }

    // Returns true if the weapon is in attack range. Used to check if the monster can go to attack state.
    bool IsInAttackRange()
    {
        if (detectedTarget && weapon && weapon.IsInAttackRange(detectedTarget.gameObject, 0.15f))
            return true;
        return false;
    }

    // Called to transition into idle state.
    void TransitionIdle()
    {
        state = State.Idle;
        agent.isStopped = true;
        TriggerAnimation(triggerIdleHash);
        StartCoroutine(IdleWait());
        audioSource.PlayOneShot(idleSfx[UnityEngine.Random.Range(0, idleSfx.Length)], 1.5f);
        if (chaseAudio && chaseAudio.isPlaying)
            chaseAudio.Stop();
    }

    // Called to transition into patrolling state.
    void TransitionPatrolling()
    {
        if (!level)
            return;
        state = State.Patrolling;
        agent.isStopped = false;
        agent.speed = walkingSpeed;
        Vector3 patrolPoint = level.GetRandomFloorPoint();
        agent.SetDestination(patrolPoint);
        TriggerAnimation(triggerWalkHash);
        if (chaseAudio && chaseAudio.isPlaying)
            chaseAudio.Stop();
    }

    // Called to transition into chasing state.
    void TransitionChasing()
    {
        state = State.Chasing;
        agent.isStopped = false;
        agent.speed = runningSpeed;
        TriggerAnimation(triggerRunHash);
        if (chaseAudio && !chaseAudio.isPlaying)
            chaseAudio.Play();
    }

    // Called to transition into attacking state.
    void TransitionAttacking()
    {
        state = State.Attacking;
        agent.isStopped = true;
        TriggerAnimation(triggerAttackHash);
        StartCoroutine(TryAttack());
    }

    // Called in the Scene view of the Editor to visualize the attack range of the weapon.
    void OnDrawGizmosSelected()
    {        
        if (!weapon || weapon.HitboxExtends == Vector3.zero || state != State.Attacking)
            return;

        Mesh primitiveMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (primitiveMesh == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireMesh(primitiveMesh, weapon.HitboxPosition, weapon.HitboxRotation, weapon.HitboxExtends * 2);
    }
}
