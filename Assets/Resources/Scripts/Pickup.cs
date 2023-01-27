using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Pickup that is placed inside the maze. It can be collected by the player to reload the scene
// and therefore generating a new maze.
public class Pickup : MonoBehaviour
{
    [Tooltip("AudioSource that should be played when the player collects the pickup.")]
    public AudioSource pickupSound;
    // Rotate speed around the y-axis in degrees / s
    private readonly float rotateSpeed = 180f;
    // Set to true when colliding with the pickup. To prevent from triggering multiple times in the same frame.
    private bool isTriggered = false;
    // Name of the pickupSound GameObject
    private string pickupSoundName;
    
    // The pickupSound GameObject has DontDestroyOnLoad set and is a Singleton.
    // DontDestroyOnLoad is needed so the pickup sound plays after the scene change in the next scene.
    // Because it is a Singleton, the pickupSound GameObject that this script references will be destroyed
    // in the first frame of the next scene.
    // In the Start method (called before the first frame) the pickupSound GameObject name can still be retrieved.
    // After waiting 1 frame this GameObject will be destroyed. Only the GameObject in the DontDestroyOnLoad scene
    // will exist with this name, so it can be searched for and assigned to pickupSound again.
    void Start()
    {
        if (pickupSound)
        {
            pickupSoundName = pickupSound.gameObject.name;
            StartCoroutine(FindPickupSound());
        }
    }

    IEnumerator FindPickupSound()
    {
        // Wait 1 frame
        yield return null;
        if (!pickupSound)
            pickupSound = GameObject.Find(pickupSoundName).GetComponent<AudioSource>();
    }

    void Update ()
    {
        transform.Rotate(0, rotateSpeed * Time.deltaTime, 0, Space.World);
    }

    // Called when the collider of this GameObject collides with another collider.
    void OnTriggerEnter(Collider other)
    {
        if (isTriggered || !other.gameObject.CompareTag("Player"))
            return;
        isTriggered = true;

        if (pickupSound)
            pickupSound.Play();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
