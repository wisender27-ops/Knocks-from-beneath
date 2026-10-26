using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    // F-04: «Продолжить», «Выбор главы» и список глав — копии кнопки «Начать игру», чтобы
    // совпадали стиль и вёрстка; сцену меню не трогаем.
    private readonly List<GameObject> mainButtons = new List<GameObject>();
    private readonly List<GameObject> chapterButtons = new List<GameObject>();
    private bool chaptersOpen;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        // Menu should have free cursor.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SettingsStore.ApplyMasterVolume();
        ChapterCheckpoints.CommitReached(); // вышли из игры в меню — открываем пройденные главы
        BuildChapterMenu();
        ShowMain();
    }

    public void StartGame()
    {
        ChapterCheckpoints.ClearLaunch();
        LoadGame();
    }

    public void ContinueGame()
    {
        if (!ChapterCheckpoints.RequestContinue()) ChapterCheckpoints.ClearLaunch();
        LoadGame();
    }

    public void StartChapter(StoryChapter chapter)
    {
        if (chapter == StoryChapter.DayOne) ChapterCheckpoints.ClearLaunch();
        else ChapterCheckpoints.RequestChapter(chapter);
        LoadGame();
    }

    void LoadGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SettingsStore.ApplyMasterVolume();
        StoryFlags.Reset(); // новая игра не наследует флаги прошлого прохождения; глава вернёт свои
        SceneManager.LoadScene(gameSceneName);
    }

    public void OpenChapters()
    {
        chaptersOpen = true;
        foreach (GameObject button in mainButtons) if (button != null) button.SetActive(false);
        foreach (GameObject button in chapterButtons) if (button != null) button.SetActive(true);
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
        chaptersOpen = false;
        foreach (GameObject button in chapterButtons) if (button != null) button.SetActive(false);
        foreach (GameObject button in mainButtons) if (button != null) button.SetActive(true);
    }

    void BuildChapterMenu()
    {
        Transform start = mainPanel != null ? mainPanel.transform.Find("BtnStart") : null;
        if (start == null) return;
        int index = start.GetSiblingIndex();

        if (ChapterCheckpoints.HasSave)
        {
            GameObject resume = CloneButton(start, "BtnContinue", "Продолжить", ContinueGame);
            resume.transform.SetSiblingIndex(index);
            SetLabel(start, "Новая игра");
        }
        GameObject chapters = CloneButton(start, "BtnChapters", "Выбор главы", OpenChapters);
        chapters.transform.SetSiblingIndex(start.GetSiblingIndex() + 1);

        foreach (Transform child in mainPanel.transform)
            if (child.name.StartsWith("Btn") && child.GetComponent<Button>() != null) mainButtons.Add(child.gameObject);

        int slot = chapters.transform.GetSiblingIndex() + 1;
        foreach (ChapterCheckpoints.ChapterInfo info in ChapterCheckpoints.Chapters)
        {
            StoryChapter chapter = info.Chapter;
            bool open = ChapterCheckpoints.IsUnlocked(chapter);
            string label = open ? info.Title : ((int)chapter + 1) + ". ▒▒▒▒▒▒";
            GameObject button = CloneButton(start, "BtnChapter" + (int)chapter, label, () => StartChapter(chapter));
            button.GetComponent<Button>().interactable = open;
            button.transform.SetSiblingIndex(slot++);
            chapterButtons.Add(button);
        }
        GameObject back = CloneButton(start, "BtnChaptersBack", "Назад", ShowMain);
        back.transform.SetSiblingIndex(slot);
        chapterButtons.Add(back);
    }

    static GameObject CloneButton(Transform source, string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject copy = Instantiate(source.gameObject, source.parent, false);
        copy.name = name;
        Button button = copy.GetComponent<Button>();
        // Копия унаследовала бы вызов StartGame из сцены — заменяем событие целиком.
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(onClick);
        SetLabel(copy.transform, label);
        return copy;
    }

    static void SetLabel(Transform button, string label)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        text.text = label;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (runtimeSettingsCanvas != null && runtimeSettingsCanvas.gameObject.activeSelf) BackFromSettings();
        else if (chaptersOpen) ShowMain();
    }
}
}
