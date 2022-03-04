using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

// Script to handle the logic in the title screen scene.
public class TitleScreen : MonoBehaviour
{
    [Tooltip("Reference to the GameManager ScriptableObject to reset the level number.")]
    public GameManager gameManager;
    [Tooltip("Reference to the Press <Button> text.")]
    public TextMeshProUGUI textMeshEnter;

    void Start()
    {
        // Display the correct button text on the title screen.
        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput && textMeshEnter)
        {
            string confirmButtonText = GameManager.GetButtonsForAction("Submit", playerInput).FirstOrDefault();
            if (confirmButtonText != null)
                textMeshEnter.text = $"Press {confirmButtonText}";
        }
        if (gameManager)
            gameManager.levelNr = 0;
    }

    // Called when pressing the submit button.
    void OnSubmit(InputValue _)
    {
        SceneManager.LoadScene("Play");
    }

    // Called when pressing the CloseGame button.
    void OnCloseGame(InputValue _)
    {
        Application.Quit();
    }
}
