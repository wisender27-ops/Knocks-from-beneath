using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnocksFromBeneath
{

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Scene")]
    [SerializeField] private string gameSceneName = "Main";
    private Canvas runtimeSettingsCanvas;
    private SettingsMenuUI runtimeSettings;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        // Menu should have free cursor.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SettingsStore.ApplyMasterVolume();
        ShowMain();
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SettingsStore.ApplyMasterVolume();
        StoryFlags.Reset(); // новая игра не наследует флаги прошлого прохождения
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenSettings()
    {
        if (runtimeSettings == null)
        {
            runtimeSettingsCanvas = PauseMenuController.CreateCanvas(transform, "SettingsCanvas", 200);
            runtimeSettings = SettingsMenuUI.BuildRuntime(runtimeSettingsCanvas.transform, PauseMenuController.FindFont(), BackFromSettings);
        }
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        runtimeSettingsCanvas.gameObject.SetActive(true);
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
        if (runtimeSettingsCanvas != null) runtimeSettingsCanvas.gameObject.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && runtimeSettingsCanvas != null && runtimeSettingsCanvas.gameObject.activeSelf)
            BackFromSettings();
    }
}
}
