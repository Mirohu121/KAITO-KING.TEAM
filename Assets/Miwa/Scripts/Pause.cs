using UnityEngine;
using UnityEngine.InputSystem;

public class Pause : MonoBehaviour
{
    [SerializeField] 
    private GameObject pausPanel;

    private bool isPaused = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
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
        pausPanel.SetActive(true); // Show the pause panel
        Time.timeScale = 0f; // Pause the game by setting time scale to 0
        isPaused = true; // Set the pause state to true
    }

    public void ResumeGame()
    {
        pausPanel.SetActive(false); // Hide the pause panel
        Time.timeScale = 1f; // Resume the game by setting time scale to 1
        isPaused = false; // Set the pause state to false
    }
}
