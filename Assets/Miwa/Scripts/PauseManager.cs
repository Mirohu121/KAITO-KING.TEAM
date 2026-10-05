using UnityEngine;
using UnityEngine.InputSystem;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private PauseHudBinder pauseHudBinder;


    private bool isPaused = false;

    public void OnPause(InputAction.CallbackContext context)
    {
        if (context.performed) // Check if the pause action was performed
        {
            if (isPaused)
            {
                ResumeGame(); // Resume the game if it is currently paused
            }
            else
            {
                PauseGame(); // Pause the game if it is currently running
            }
        }
    }

    public void PauseGame()
    {
        pauseHudBinder.PausePanel.SetActive(true); // Show the pause panel
        Time.timeScale = 0f; // Pause the game by setting time scale to 0
        isPaused = true; // Set the pause state to true
    }

    public void ResumeGame()
    {
        pauseHudBinder.PausePanel.SetActive(false); // Hide the pause panel
        Time.timeScale = 1f; // Resume the game by setting time scale to 1
        isPaused = false; // Set the pause state to false
    }
}

