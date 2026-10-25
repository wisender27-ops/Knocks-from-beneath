using TMPro;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Общий контракт оконной мини-игры: гейты открытия, захват и восстановление
    // камеры/взаимодействия/курсора, ввод только вне паузы, VHS-корень UI.
    public abstract class MinigameWindow : MonoBehaviour
    {
        public static MinigameWindow Current { get; private set; }
        public static bool IsAnyOpen => Current != null;

        private PlayerController player;
        private PlayerInteraction interaction;
        private bool cameraLocked, interactionEnabled, cursorVisible;
        private CursorLockMode cursorLock;
        protected GameObject Root { get; private set; }
        public bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => Current = null;

        protected abstract string WindowName { get; }
        protected abstract void Build(Transform root);
        protected abstract void Tick();
        // Esc, отключение и уничтожение компонента закрывают окно через этот метод.
        protected virtual void Cancel() => CloseWindow();
        protected virtual bool CanOpen => true;

        // Пауза и тайм-аут кадра после неё не пропускают ввод в мини-игру.
        protected static bool InputAllowed => Time.timeScale != 0f && !PauseMenuController.InputBlocked;
        protected static bool CancelPressed => Input.GetKeyDown(KeyCode.Escape);

        protected bool TryOpen()
        {
            if (FadeTransitionPresentation.IsActive || IsOpen || IsAnyOpen || !CanOpen || Time.timeScale == 0f ||
                QuestJournalScreen.IsAnyOpen || RetroInventoryScreen.IsAnyOpen)
                return false;
            player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.isCameraLocked) return false;
            interaction = FindFirstObjectByType<PlayerInteraction>();
            if (Root == null) CreateRoot();
            cameraLocked = player != null && player.isCameraLocked;
            interactionEnabled = interaction != null && interaction.enabled;
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            if (player != null) player.isCameraLocked = true;
            if (interaction != null) interaction.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            IsOpen = true;
            Current = this;
            Root.SetActive(true);
            return true;
        }

        // Возвращает false, если окно уже было закрыто.
        protected bool CloseWindow()
        {
            if (!IsOpen) return false;
            IsOpen = false;
            if (Current == this) Current = null;
            if (Root != null) Root.SetActive(false);
            if (player != null) player.isCameraLocked = cameraLocked;
            if (interaction != null) interaction.enabled = interactionEnabled;
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
            return true;
        }

        protected virtual void Update()
        {
            if (!IsOpen || !InputAllowed) return;
            Tick();
        }

        protected virtual void OnDisable() => Cancel();

        protected virtual void OnDestroy()
        {
            Cancel();
            if (Root == null) return;
            if (Application.isPlaying) Destroy(Root);
            else DestroyImmediate(Root);
        }

        private void CreateRoot()
        {
            Root = new GameObject(WindowName, typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var canvas = Root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = Root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            var shade = Rect("Shade", Root.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.62f);
            Build(Root.transform);
        }

        protected static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        protected TextMeshProUGUI Label(string name, float y, float size)
        {
            var text = Rect(name, Root.transform, new Vector2(0f, y), new Vector2(1000f, 70f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            var manager = FindFirstObjectByType<QuestManager>();
            text.font = UiStyle.Font(manager != null && manager.questUiText != null ? manager.questUiText.font : null);
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = UiStyle.Ivory;
            text.raycastTarget = false;
            return text;
        }

        protected UiKeyHints KeyHints(TMP_FontAsset font, float y)
        {
            var keys = UiKeyHints.Create(Root.transform, "Keys", font);
            keys.RectTransform.anchorMin = keys.RectTransform.anchorMax = keys.RectTransform.pivot = new Vector2(0.5f, 0.5f);
            keys.RectTransform.anchoredPosition = new Vector2(0f, y);
            return keys;
        }
    }
}
