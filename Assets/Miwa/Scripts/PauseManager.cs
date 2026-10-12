using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
        if (pauseHudBinder != null && pauseHudBinder.PausePanel != null)
            pauseHudBinder.PausePanel.SetActive(true);
        Time.timeScale = 0f;
        isPaused = true;
    }

    public void ResumeGame()
    {
        if (pauseHudBinder != null && pauseHudBinder.PausePanel != null)
            pauseHudBinder.PausePanel.SetActive(false);
        Time.timeScale = 1f;
        isPaused = false;
    }

    public void ReturnToSelect()
    {
        Time.timeScale = 1f;
        isPaused = false;
        if (pauseHudBinder != null && pauseHudBinder.PausePanel != null)
            pauseHudBinder.PausePanel.SetActive(false);

        // Soft return keeps EventSystem / Input wiring intact; full reload hid unit select.
        var boot = Object.FindFirstObjectByType<Robogee.Combat.CombatAlphaBootstrap>();
        if (boot != null)
        {
            boot.RestartToUnitSelect();
            return;
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

}

