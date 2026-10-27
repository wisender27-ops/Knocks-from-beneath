using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    [DefaultExecutionOrder(-10000)]
    public sealed class PauseMenuController : MonoBehaviour
    {
        private PlayerController player;
        private Canvas canvas;
        private GameObject menu, confirmation;
        private SettingsMenuUI settings;
        private TextMeshProUGUI counter;
        private UiMenuItem[] menuItems, confirmItems;
        private UiKeyHints hints;
        private bool paused, previousCameraLock, previousAudioPause, previousCursorVisible;
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private static int resumeFrame = -1;
        public static bool IsPaused { get; private set; }
        public static bool InputBlocked => IsPaused || Time.frameCount == resumeFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            IsPaused = false;
            resumeFrame = -1;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "Main" || FindFirstObjectByType<PauseMenuController>() != null) return;
            new GameObject("PauseMenu").AddComponent<PauseMenuController>();
        }

        private void Awake()
        {
            player = FindFirstObjectByType<PlayerController>();
            canvas = CreateCanvas(transform, "PauseCanvas", 32000);
            UiStyle.Fill(canvas.transform, "Dim", UiStyle.Dim).raycastTarget = true;
            UiStyle.Scanlines(canvas.transform);
            TMP_FontAsset font = FindFont();
            menu = ScreenLayer(canvas.transform, "Pause");
            Heading(menu.transform, "▌▌ ПАУЗА", font, 64f, UiStyle.Amber, -96f);
            counter = Heading(menu.transform, string.Empty, font, 26f, UiStyle.Ash, -178f);
            menuItems = Column(menu.transform, font, -270f,
                ("Продолжить", Resume), ("Настройки", OpenSettings), ("В главное меню", ConfirmReturnToMenu));
            settings = SettingsMenuUI.BuildRuntime(canvas.transform, font, ShowMenu);
            confirmation = ScreenLayer(canvas.transform, "ConfirmExit");
            Heading(confirmation.transform, "Выйти в главное меню?", font, 48f, UiStyle.Ivory, -96f);
            Heading(confirmation.transform, "Текущий прогресс будет потерян.", font, 28f, UiStyle.Ash, -170f);
            confirmItems = Column(confirmation.transform, font, -270f, ("Остаться", ShowMenu), ("Выйти в меню", ReturnToMainMenu));
            hints = UiKeyHints.Create(canvas.transform, "PauseHints", font, 1.5f);
            hints.RectTransform.anchorMin = hints.RectTransform.anchorMax = hints.RectTransform.pivot = new Vector2(1f, 0f);
            hints.RectTransform.anchoredPosition = new Vector2(-45f, 45f);
            ShowMenu();
            canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (!paused) Pause();
            else if (settings.gameObject.activeSelf || confirmation.activeSelf) ShowMenu();
            else Resume();
        }

        public void Pause()
        {
            // Выполняется раньше экранов: Esc закрывает инвентарь без открытия паузы в том же кадре.
            if (paused || DeathTapeEffect.IsActive || Time.timeScale <= 0f || MinigameWindow.IsAnyOpen || RetroInventoryScreen.IsAnyOpen ||
                QuestJournalScreen.IsAnyOpen || NarrativeReadingPanel.IsModal || FadeTransitionPresentation.IsActive) return;
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            previousCameraLock = player != null && player.isCameraLocked;
            paused = IsPaused = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (player != null) player.isCameraLocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            int seconds = Mathf.FloorToInt(Time.timeSinceLevelLoad);
            counter.text = $"СЧЁТЧИК {seconds / 3600}:{seconds / 60 % 60:00}:{seconds % 60:00}";
            canvas.gameObject.SetActive(true);
            ShowMenu();
            GameAudio.HookSelectables(canvas.transform);
            GameAudio.PlayUi(UiSound.PauseOpen);
        }

        public void Resume()
        {
            if (!paused) return;
            paused = IsPaused = false;
            resumeFrame = Time.frameCount;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            if (player != null) player.isCameraLocked = previousCameraLock;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            if (canvas != null) canvas.gameObject.SetActive(false);
            GameAudio.PlayUi(UiSound.PauseClose);
            PlayerPrefs.Save();
        }

        public void OpenSettings()
        {
            menu.SetActive(false);
            confirmation.SetActive(false);
            settings.gameObject.SetActive(true);
            hints.Set("ESC", "НАЗАД");
        }

        public void ShowMenu()
        {
            menu.SetActive(true);
            settings.gameObject.SetActive(false);
            confirmation.SetActive(false);
            hints.Set("↑ ↓", "ВЫБОР", "ENTER", "OK", "ESC", "ПРОДОЛЖИТЬ");
            UiMenuItem.Select(menuItems[0]);
        }

        public void ConfirmReturnToMenu()
        {
            menu.SetActive(false);
            confirmation.SetActive(true);
            hints.Set("↑ ↓", "ВЫБОР", "ENTER", "OK", "ESC", "НАЗАД");
            UiMenuItem.Select(confirmItems[0]);
        }

        public void ReturnToMainMenu()
        {
            Resume();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            SceneManager.LoadScene("MainMenu");
        }

        private void OnDisable() => Resume();
        private void OnApplicationFocus(bool focused)
        {
            if (!focused) Pause();
        }

        internal static Canvas CreateCanvas(Transform parent, string name, int order)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var result = root.GetComponent<Canvas>();
            result.renderMode = RenderMode.ScreenSpaceOverlay;
            result.sortingOrder = order;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("MenuEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            return result;
        }

        internal static TMP_FontAsset FindFont()
        {
            foreach (var text in FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.font != null && text.font.name.Contains("Cyr")) return text.font;
            return TMP_Settings.defaultFontAsset;
        }

        // Экраны паузы — колонка у левого края, как экранное меню видеомагнитофона.
        private const float Margin = 180f;

        private static GameObject ScreenLayer(Transform parent, string name) => UiStyle.Stretch(name, parent).gameObject;

        private static TextMeshProUGUI Heading(Transform parent, string text, TMP_FontAsset font, float size, Color color, float y)
        {
            var label = UiStyle.Text("Heading", parent, font, size, color, TextAlignmentOptions.TopLeft);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(Margin, y);
            label.rectTransform.sizeDelta = new Vector2(1400f, size * 1.4f);
            label.text = text;
            return label;
        }

        private static UiMenuItem[] Column(Transform parent, TMP_FontAsset font, float y, params (string caption, UnityAction action)[] entries)
        {
            var column = UiStyle.Rect("Items", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin - 22f, y), new Vector2(900f, 400f));
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.spacing = 10f;
            var items = new UiMenuItem[entries.Length];
            for (int i = 0; i < entries.Length; i++)
                items[i] = UiMenuItem.Create(column, entries[i].caption, font, 40f, entries[i].action);
            UiMenuItem.LinkVertical(items);
            return items;
        }
    }
}
