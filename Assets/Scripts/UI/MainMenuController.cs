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
    private Canvas runtimeGalleryCanvas;

    // Кнопки меню — копии кнопки «Начать игру», чтобы совпадали стиль и вёрстка; сцену меню не трогаем.
    // Главный экран: «Играть», «Концовки», «Настройки», «Выход»; язык — флагом в правом верхнем углу.
    // «Играть» открывает «Продолжить» / «Новая игра» / «Выбор главы», «Выбор главы» — список глав.
    private readonly List<GameObject> mainButtons = new List<GameObject>();
    private readonly List<GameObject> playButtons = new List<GameObject>();
    private readonly List<GameObject> chapterButtons = new List<GameObject>();
    private List<GameObject> openGroup;
    private const float LockedAlpha = 0.45f;
    private MenuLanguagePicker languagePicker;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        // Menu should have free cursor.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SettingsStore.ApplyMasterVolume();
        ChapterCheckpoints.CommitReached(); // вышли из игры в меню — открываем пройденные главы
        BuildMenu();
        if (mainPanel != null)
        {
            TMP_FontAsset font = PauseMenuController.FindFont();
            MenuCredits.Build(mainPanel.transform, font);
            languagePicker = MenuLanguagePicker.Build(mainPanel.transform, font, SelectLanguage);
        }
        ShowMain();
    }

    // L-02: смена языка перезагружает меню, чтобы все подписи собрались заново.
    void SelectLanguage(string code)
    {
        if (code == Loc.Current) return;
        Loc.Select(code);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

    public void OpenPlay() => ShowGroup(playButtons);

    public void OpenChapters() => ShowGroup(chapterButtons);

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

    public void OpenGallery()
    {
        if (runtimeGalleryCanvas != null) Destroy(runtimeGalleryCanvas.gameObject);
        // Корневой канвас: вложенный в канвас меню не получил бы размер экрана, а галерея растягивается на весь экран.
        runtimeGalleryCanvas = PauseMenuController.CreateCanvas(null, "EndingGalleryCanvas", 200);
        EndingGallery.Build(runtimeGalleryCanvas.transform, PauseMenuController.FindFont(), ShowMain);
        if (mainPanel != null) mainPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
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
        if (runtimeGalleryCanvas != null) { Destroy(runtimeGalleryCanvas.gameObject); runtimeGalleryCanvas = null; }
        if (mainPanel != null) mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        ShowGroup(mainButtons);
    }

    // На панели видна одна группа кнопок: главная, «Играть» или главы.
    void ShowGroup(List<GameObject> group)
    {
        openGroup = group;
        SetActive(mainButtons, group == mainButtons);
        SetActive(playButtons, group == playButtons);
        SetActive(chapterButtons, group == chapterButtons);
    }

    static void SetActive(List<GameObject> group, bool active)
    {
        foreach (GameObject item in group) item.SetActive(active);
    }

    void BuildMenu()
    {
        Transform start = mainPanel != null ? mainPanel.transform.Find("BtnStart") : null;
        if (start == null) return;
        int slot = start.GetSiblingIndex();

        // Главная группа; «Настройки» и «Выход» из сцены стоят ниже вставленных кнопок.
        mainButtons.Add(Place(CloneButton(start, "BtnPlay", Loc.T("Играть"), OpenPlay), ref slot));
        mainButtons.Add(Place(CloneButton(start, "BtnEndings", Loc.T("Концовки"), OpenGallery), ref slot));
        mainButtons.Add(mainPanel.transform.Find("BtnSettings").gameObject);
        mainButtons.Add(mainPanel.transform.Find("BtnQuit").gameObject);

        if (ChapterCheckpoints.HasSave)
        {
            playButtons.Add(Place(CloneButton(start, "BtnContinue", Loc.T("Продолжить"), ContinueGame), ref slot));
            SetLabel(start, Loc.T("Новая игра"));
        }
        playButtons.Add(Place(start.gameObject, ref slot));
        playButtons.Add(Place(CloneButton(start, "BtnChapters", Loc.T("Выбор главы"), OpenChapters), ref slot));
        playButtons.Add(Place(CloneButton(start, "BtnPlayBack", Loc.T("Назад"), ShowMain), ref slot));

        BuildChapterButtons(start, ref slot);
    }

    // Открытые главы, за ними одна следующая — серая и неактивная, дальше только подсказка.
    void BuildChapterButtons(Transform start, ref int slot)
    {
        bool lockedShown = false;
        foreach (ChapterCheckpoints.ChapterInfo info in ChapterCheckpoints.Chapters)
        {
            StoryChapter chapter = info.Chapter;
            bool open = ChapterCheckpoints.IsUnlocked(chapter);
            if (!open && lockedShown)
            {
                chapterButtons.Add(Place(CloneHint(start, Loc.T("Следующие главы откроются по мере прохождения")), ref slot));
                break;
            }
            GameObject button = CloneButton(start, "BtnChapter" + (int)chapter, Loc.T(info.Title), () => StartChapter(chapter));
            if (!open)
            {
                lockedShown = true;
                button.GetComponent<Button>().interactable = false;
                button.AddComponent<CanvasGroup>().alpha = LockedAlpha;
            }
            chapterButtons.Add(Place(button, ref slot));
        }
        chapterButtons.Add(Place(CloneButton(start, "BtnChaptersBack", Loc.T("Назад"), OpenPlay), ref slot));
    }

    static GameObject Place(GameObject item, ref int slot)
    {
        item.transform.SetSiblingIndex(slot++);
        return item;
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

    // Строка-пояснение в стиле кнопок, но мельче, курсивом и без нажатия.
    static GameObject CloneHint(Transform source, string text)
    {
        GameObject copy = Instantiate(source.gameObject, source.parent, false);
        copy.name = "ChaptersHint";
        Destroy(copy.GetComponent<Button>());
        LayoutElement layout = copy.GetComponent<LayoutElement>();
        if (layout != null) layout.preferredHeight *= 0.6f;
        TMP_Text label = copy.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = text;
            label.fontSize *= 0.5f;
            label.fontStyle |= FontStyles.Italic;
            label.raycastTarget = false;
            Color color = label.color;
            label.color = new Color(color.r * 0.8f, color.g * 0.8f, color.b * 0.8f, LockedAlpha);
        }
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
        if (languagePicker != null && languagePicker.IsOpen) languagePicker.Close();
        else if (runtimeGalleryCanvas != null) ShowMain();
        else if (runtimeSettingsCanvas != null && runtimeSettingsCanvas.gameObject.activeSelf) BackFromSettings();
        else if (openGroup == chapterButtons) OpenPlay();
        else if (openGroup != mainButtons) ShowMain();
    }
}
}
