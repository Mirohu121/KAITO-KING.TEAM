using UnityEngine;

public class PauseHudBinder : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;

    public GameObject PausePanel => pausePanel;

}