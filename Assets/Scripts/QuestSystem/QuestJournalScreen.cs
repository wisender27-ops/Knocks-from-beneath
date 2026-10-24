using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    public sealed class QuestJournalScreen : MonoBehaviour
    {
        [SerializeField] private QuestManager manager;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerInteraction playerInteraction;
        [SerializeField] private KeyCode toggleKey = KeyCode.J;

        private static readonly Color Ivory = UiStyle.Ivory;
        private static readonly Color Amber = UiStyle.Amber;
        private static readonly Color Ash = UiStyle.Ash;
        private readonly List<TextMeshProUGUI> historyRows = new List<TextMeshProUGUI>();
        private CanvasGroup group;
        private RectTransform panel;
        private RectTransform historyContent;
        private ScrollRect historyScroll;
        private TextMeshProUGUI currentTitle;
        private TextMeshProUGUI currentProgress;
        private TextMeshProUGUI completedHeading;
        private UiKeyHints hint;
        private TMP_FontAsset font;
        private float transition;
        private bool closing;
        private float previousTimeScale;
        private bool previousAudioPause;
        private bool previousInteractionEnabled;
        private bool previousCameraLock;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        public static bool IsAnyOpen { get; private set; }
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (manager == null) manager = FindAnyObjectByType<QuestManager>();
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerInteraction == null && playerController != null)
                playerInteraction = playerController.GetComponent<PlayerInteraction>();
            if (manager != null && manager.questUiText != null) font = manager.questUiText.font;
            BuildInterface();
            SetHidden();
        }

        private void OnEnable()
        {
            if (manager != null) manager.OnQuestChanged += Refresh;
        }

        private void OnDisable()
        {
            if (manager != null) manager.OnQuestChanged -= Refresh;
            Close();
            if (group != null) SetHidden();
            if (hint != null) hint.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (CornerPeekSequence.IsPlaying) return;
            if (Input.GetKeyDown(toggleKey))
            {
                if (IsOpen) Close(); else Open();
            }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();

            bool showHint = !InteractionPresentation.HasControlHints && !IsOpen && !closing && Time.timeScale > 0f &&
                !RetroInventoryScreen.IsAnyOpen &&
                (playerController == null || !playerController.isCameraLocked) &&
                (playerInteraction == null || !playerInteraction.IsEatingPie);
            if (hint.gameObject.activeSelf != showHint) hint.gameObject.SetActive(showHint);

            if (!IsOpen && !closing) return;
            transition = Mathf.MoveTowards(transition, IsOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.18f);
            float ease = Mathf.SmoothStep(0f, 1f, transition);
            group.alpha = ease;
            panel.anchoredPosition = new Vector2(0f, -14f * (1f - ease));
            if (closing && transition <= 0f) SetHidden();
            if (!IsOpen) return;

            float direction = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            float scrollable = historyContent.rect.height - historyScroll.viewport.rect.height;
            if (scrollable > 0f && direction != 0f)
                historyScroll.verticalNormalizedPosition = Mathf.Clamp01(
                    historyScroll.verticalNormalizedPosition + direction * 350f * Time.unscaledDeltaTime / scrollable);
        }

        public void Open()
        {
            if (FadeTransitionPresentation.IsActive || manager == null || IsOpen || closing || IsAnyOpen || Time.timeScale == 0f ||
                RetroInventoryScreen.IsAnyOpen ||
                (playerController != null && playerController.isCameraLocked) ||
                (playerInteraction != null && playerInteraction.IsEatingPie)) return;
            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            previousCameraLock = playerController != null && playerController.isCameraLocked;
            previousInteractionEnabled = playerInteraction != null && playerInteraction.enabled;
            IsOpen = true;
            IsAnyOpen = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (playerController != null) playerController.isCameraLocked = true;
            if (playerInteraction != null) playerInteraction.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            group.gameObject.SetActive(true);
            group.interactable = true;
            group.blocksRaycasts = true;
            Refresh();
            Canvas.ForceUpdateCanvases();
            historyScroll.verticalNormalizedPosition = 1f;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            IsAnyOpen = false;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            if (playerController != null) playerController.isCameraLocked = previousCameraLock;
            if (playerInteraction != null) playerInteraction.enabled = previousInteractionEnabled;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            group.interactable = false;
            group.blocksRaycasts = false;
            closing = true;
        }

        private void SetHidden()
        {
            transition = 0f;
            closing = false;
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(false);
        }

        private void Refresh()
        {
            if (!IsOpen || manager == null) return;
            QuestManager.QuestData quest = manager.GetActiveQuest();
            currentTitle.text = quest != null ? quest.questTitle : "Сейчас нет активного задания";
            currentProgress.text = quest != null && quest.requiredAmount > 1
                ? $"Прогресс: {Mathf.Clamp(quest.currentAmount, 0, quest.requiredAmount)} из {quest.requiredAmount}"
                : quest != null ? string.Empty : "Новые цели появятся по ходу истории";
            int count = 0;
            for (int i = manager.questList.Count - 1; i >= 0; i--)
            {
                QuestManager.QuestData completed = manager.questList[i];
                if (completed == null || !completed.IsCompleted) continue;
                SetHistoryRow(count++, completed.questTitle, true);
            }
            completedHeading.text = count > 0 ? $"Выполнено: {count}" : "Выполнено";
            if (count == 0) SetHistoryRow(count++, "Выполненные задания появятся здесь", false);
            for (int i = count; i < historyRows.Count; i++) historyRows[i].gameObject.SetActive(false);
        }

        private void SetHistoryRow(int index, string title, bool done)
        {
            if (index == historyRows.Count)
            {
                TextMeshProUGUI row = Text("CompletedQuest", historyContent, string.Empty, 21f, Ash);
                row.alignment = TextAlignmentOptions.TopLeft;
                row.margin = new Vector4(0f, 6f, 12f, 6f);
                row.gameObject.AddComponent<LayoutElement>().minHeight = 40f;
                historyRows.Add(row);
            }
            historyRows[index].text = title;
            historyRows[index].fontStyle = done ? FontStyles.Strikethrough : FontStyles.Normal;
            historyRows[index].gameObject.SetActive(true);
        }

        private void BuildInterface()
        {
            Canvas canvas = UiStyle.OverlayCanvas(transform, "QuestJournalCanvas", 500, new Vector2(1280f, 720f), true);
            GameObject canvasObject = canvas.gameObject;
            RectTransform root = Rect("JournalOverlay", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image backdrop = root.gameObject.AddComponent<Image>();
            backdrop.color = UiStyle.Dim;
            UiStyle.Scanlines(root);
            group = root.gameObject.AddComponent<CanvasGroup>();
            panel = Rect("JournalPanel", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            PositionedText("JournalTitle", "► ЗАДАНИЯ", 30f, Amber, 120f, -64f, 780f, 40f);
            PositionedText("ActiveHeading", "Текущая цель", 17f, Ash, 120f, -134f, 800f, 26f);
            currentTitle = PositionedText("ActiveTitle", "", 30f, Ivory, 120f, -164f, 860f, 84f);
            currentProgress = PositionedText("ActiveProgress", "", 18f, Ash, 120f, -250f, 860f, 28f);
            RectTransform divider = Rect("Divider", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -298f), new Vector2(860f, 2f));
            divider.pivot = new Vector2(0f, 1f);
            divider.gameObject.AddComponent<Image>().color = new Color(Ash.r, Ash.g, Ash.b, .25f);
            completedHeading = PositionedText("CompletedHeading", "Выполнено", 17f, Ash, 120f, -318f, 820f, 26f);
            RectTransform scroll = Rect("HistoryScroll", panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -356f), new Vector2(860f, 250f));
            scroll.pivot = new Vector2(0f, 1f);
            historyScroll = scroll.gameObject.AddComponent<ScrollRect>();
            historyScroll.horizontal = false;
            historyScroll.movementType = ScrollRect.MovementType.Clamped;
            historyScroll.scrollSensitivity = 32f;
            RectTransform viewport = Rect("Viewport", scroll, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            historyScroll.viewport = viewport;
            historyContent = Rect("HistoryContent", viewport, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            historyContent.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup layout = historyContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 6f;
            ContentSizeFitter fitter = historyContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            historyScroll.content = historyContent;
            UiKeyHints controls = UiKeyHints.Create(root, "Controls", font);
            controls.RectTransform.anchorMin = controls.RectTransform.anchorMax = controls.RectTransform.pivot = new Vector2(1f, 0f);
            controls.RectTransform.anchoredPosition = new Vector2(-30f, 30f);
            controls.Set("J", "ЗАКРЫТЬ", "↑ ↓", "ЛИСТАТЬ");
            hint = UiKeyHints.Create(canvasObject.transform, "JournalHint", font, .85f, new Color(Ivory.r, Ivory.g, Ivory.b, .8f));
            hint.RectTransform.anchorMin = hint.RectTransform.anchorMax = hint.RectTransform.pivot = new Vector2(1f, 0f);
            hint.RectTransform.anchoredPosition = new Vector2(-30f, 30f);
            hint.Set("J", "ЗАДАНИЯ", "H", "ИСТОРИЯ");
        }

        private TextMeshProUGUI PositionedText(string name, string value, float size, Color color, float x, float y, float width, float height)
        {
            TextMeshProUGUI text = Text(name, panel, value, size, color);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return text;
        }

        private TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            text.font = UiStyle.Font(font);
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.richText = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }
    }
}

