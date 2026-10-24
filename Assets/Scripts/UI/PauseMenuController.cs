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
        private DinnerCookingWindow cooking;
        private Canvas canvas;
        private GameObject menu, confirmation;
        private SettingsMenuUI settings;
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
            cooking = FindFirstObjectByType<DinnerCookingWindow>();
            canvas = CreateCanvas(transform, "PauseCanvas", 32000);
            var background = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)background.transform);
            background.GetComponent<Image>().color = new Color(0, 0, 0, .85f);
            TMP_FontAsset font = FindFont();
            menu = Panel(canvas.transform, "Pause", new Vector2(600, 470));
            Label(menu.transform, "ПАУЗА", font, new Vector2(0, 155), 38);
            Button(menu.transform, "Продолжить", font, 65, Resume);
            Button(menu.transform, "Настройки", font, -5, OpenSettings);
            Button(menu.transform, "В главное меню", font, -75, ConfirmReturnToMenu);
            settings = SettingsMenuUI.BuildRuntime(canvas.transform, font, ShowMenu);
            confirmation = Panel(canvas.transform, "ConfirmExit", new Vector2(760, 360));
            Label(confirmation.transform, "ВЫЙТИ В ГЛАВНОЕ МЕНЮ?\nТекущий прогресс будет потерян.", font, new Vector2(0, 80), 26);
            Button(confirmation.transform, "Отмена", font, -30, ShowMenu);
            Button(confirmation.transform, "Выйти в меню", font, -100, ReturnToMainMenu);
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
            if (cooking == null) cooking = FindFirstObjectByType<DinnerCookingWindow>();
            // Выполняется раньше экранов: Esc закрывает инвентарь без открытия паузы в том же кадре.
            if (paused || Time.timeScale <= 0f || (cooking != null && cooking.IsOpen) || RetroInventoryScreen.IsAnyOpen ||
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
            ShowMenu();
            canvas.gameObject.SetActive(true);
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
            PlayerPrefs.Save();
        }

        public void OpenSettings()
        {
            menu.SetActive(false);
            confirmation.SetActive(false);
            settings.gameObject.SetActive(true);
        }

        public void ShowMenu()
        {
            menu.SetActive(true);
            settings.gameObject.SetActive(false);
            confirmation.SetActive(false);
        }

        public void ConfirmReturnToMenu()
        {
            menu.SetActive(false);
            confirmation.SetActive(true);
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

        private static GameObject Panel(Transform parent, string name, Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = size;
            root.GetComponent<Image>().color = new Color32(24, 24, 25, 255);
            return root;
        }

        private static TextMeshProUGUI Label(Transform parent, string text, TMP_FontAsset font, Vector2 position, float size)
        {
            var root = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            root.transform.SetParent(parent, false);
            var label = root.GetComponent<TextMeshProUGUI>();
            label.rectTransform.sizeDelta = new Vector2(700, 110);
            label.rectTransform.anchoredPosition = position;
            label.font = font;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        private static void Button(Transform parent, string text, TMP_FontAsset font, float y, UnityAction action)
        {
            var root = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(470, 56);
            rect.anchoredPosition = new Vector2(0, y);
            root.GetComponent<Image>().color = new Color32(55, 55, 58, 255);
            var button = root.GetComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            button.onClick.AddListener(action);
            Label(root.transform, text, font, Vector2.zero, 27).rectTransform.sizeDelta = rect.sizeDelta;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
