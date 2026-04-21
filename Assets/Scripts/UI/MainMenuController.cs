using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Main";

    void Awake()
    {
        // Menu should have free cursor.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SettingsStore.ApplyMasterVolume();
        ShowMain();
    }

    public void StartGame()
    {
        SettingsStore.ApplyMasterVolume();
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void BackFromSettings()
    {
        ShowMain();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    void ShowMain()
    {
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }
}

