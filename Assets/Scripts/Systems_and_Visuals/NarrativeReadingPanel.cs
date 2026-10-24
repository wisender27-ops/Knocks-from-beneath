using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    public sealed class NarrativeReadingPanel : MonoBehaviour
    {
        public static bool IsModal { get; private set; }
        private const string ScaleKey = "Narrative.TextScale";
        private const string AutoKey = "Narrative.AutoAdvance";
        private readonly List<string> history = new List<string>(200);
        private RectTransform dialogue, historyPanel, historyContent;
        private TextMeshProUGUI body, speakerText, hint, historyBody, autoLabel, sizeLabel;
        private ScrollRect scroll;
        private Canvas canvas;
        private TMP_FontAsset font;
        private PlayerController player;
        private PlayerInteraction interaction;
        private PlayerDoorOpen doorInput;
        private bool previousDoorInputEnabled;
        private int layoutVersion;
        private bool important, historyOpen, ownsInput, previousCameraLock, previousInteractionEnabled;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private float previousTimeScale, textScale = 1f;
        private bool autoAdvance;
        private int generation;
        
        public bool IsHistoryOpen => historyOpen;
        public bool AutoAdvance => autoAdvance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => IsModal = false;

        public void Initialize(TMP_FontAsset sourceFont)
        {
            if (canvas != null) return;
            font = sourceFont;
            textScale = Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, 1f), .85f, 1.3f);
            autoAdvance = PlayerPrefs.GetInt(AutoKey, 0) != 0;
            BuildInterface();
        }

        public IEnumerator BeginSequence(bool requireConfirmation)
        {
            while (ExternalScreenOpen || Time.timeScale <= 0f) yield return null;
            important = requireConfirmation;
            dialogue.sizeDelta = new Vector2(930, important ? 208 : 164);
            body.rectTransform.sizeDelta = new Vector2(866, important ? 106 : 62);
            hint.rectTransform.anchoredPosition = new Vector2(32, important ? -174 : -130);
            layoutVersion++;
            generation++;
            if (important) AcquireInput();
        }

        public IEnumerator ShowLine(string line, string speaker, float typingInterval, float minimumDuration)
        {
            if (string.IsNullOrWhiteSpace(line)) yield break;
            int version = generation;
            while (ExternalScreenOpen || historyOpen) yield return null;
            if (version != generation) yield break;
            history.Add(speaker + "\n" + line);
            if (history.Count > 200) history.RemoveAt(0);
            speakerText.text = speaker;
            dialogue.gameObject.SetActive(true);
            body.text = line;
            body.overflowMode = TextOverflowModes.Page;
            body.maxVisibleCharacters = int.MaxValue;
            body.ForceMeshUpdate();
            int pages = Mathf.Max(1, body.textInfo.pageCount);
            int layout = layoutVersion;
            for (int page = 0; page < pages && version == generation; page++)
            {
                body.pageToDisplay = page + 1;
                int first = body.textInfo.pageInfo[page].firstCharacterIndex;
                int end = body.textInfo.pageInfo[page].lastCharacterIndex + 1;
                int visible = important ? first : end;
                body.maxVisibleCharacters = visible;
                float typed = 0f, read = 0f, inputDelay = .18f;
                int wordCount = 1;
                for (int i = first; i < end; i++)
                    if (char.IsWhiteSpace(body.textInfo.characterInfo[i].character)) wordCount++;
                float readingSeconds = Mathf.Max(minimumDuration, .8f + wordCount / 3.2f);
                while (version == generation)
                {
                    bool suspended = historyOpen || ExternalScreenOpen || (!important && Time.timeScale <= 0f);
                    dialogue.gameObject.SetActive(!suspended);
                    if (suspended) { yield return null; continue; }
                    if (layout != layoutVersion)
                    {
                        body.ForceMeshUpdate();
                        pages = Mathf.Max(1, body.textInfo.pageCount);
                        page = Mathf.Min(page, pages - 1);
                        body.pageToDisplay = page + 1;
                        first = body.textInfo.pageInfo[page].firstCharacterIndex;
                        end = body.textInfo.pageInfo[page].lastCharacterIndex + 1;
                        visible = Mathf.Clamp(visible, first, end);
                        body.maxVisibleCharacters = visible;
                        layout = layoutVersion;
                    }
                    inputDelay -= Time.unscaledDeltaTime;
                    bool advance = important && inputDelay <= 0f && AdvancePressed();
                    if (visible < end)
                    {
                        typed += Time.unscaledDeltaTime;
                        int count = Mathf.FloorToInt(typed / Mathf.Clamp(typingInterval, .01f, .025f));
                        if (count > 0) { visible = Mathf.Min(end, visible + count); typed = 0f; }
                        if (advance) { visible = end; inputDelay = .18f; }
                        body.maxVisibleCharacters = visible;
                        hint.text = important ? "SPACE / ENTER / ЛКМ — ПОКАЗАТЬ ТЕКСТ     •     H — ИСТОРИЯ" : "";
                    }
                    else
                    {
                        read += Time.unscaledDeltaTime;
                        hint.text = important
                            ? (autoAdvance ? "АВТОПРОДОЛЖЕНИЕ     •     H — ИСТОРИЯ И НАСТРОЙКИ" :
                                "SPACE / ENTER / ЛКМ — ДАЛЕЕ     •     H — ИСТОРИЯ")
                            : "H — ПЕРЕЧИТАТЬ";
                        if ((important && advance) || ((!important || autoAdvance) && read >= readingSeconds)) break;
                    }
                    yield return null;
                }
                // Release input on a later frame so the advance click cannot interact with the world.
                yield return null;
            }
            body.maxVisibleCharacters = int.MaxValue;
            dialogue.gameObject.SetActive(false);
        }

        public void EndSequence()
        {
            generation++;
            important = false;
            if (dialogue != null) dialogue.gameObject.SetActive(false);
            if (!historyOpen) ReleaseInput();
        }

        public void StopPresentation()
        {
            historyOpen = false;
            if (historyPanel != null) historyPanel.gameObject.SetActive(false);
            EndSequence();
        }

        private static bool AdvancePressed() => Input.GetKeyDown(KeyCode.Space) ||
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.JoystickButton0);

        private static bool ExternalScreenOpen => PauseMenuController.InputBlocked || RetroInventoryScreen.IsAnyOpen ||
            QuestJournalScreen.IsAnyOpen || FadeTransitionPresentation.IsActive;

        private void Update()
        {
            if (canvas == null) return;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (Input.GetKeyDown(KeyCode.H))
            {
                if (historyOpen) CloseHistory();
                else if (!ExternalScreenOpen && !CornerPeekSequence.IsPlaying &&
                    !PlayerInteraction.IsLockSequencePlaying && (important ||
                    (Time.timeScale > 0f && (player == null || !player.isCameraLocked))))
                    OpenHistory();
            }
            else if (historyOpen && Input.GetKeyDown(KeyCode.Escape)) CloseHistory();
            if (historyOpen)
            {
                float direction = (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
                float extent = historyContent.rect.height - scroll.viewport.rect.height;
                if (extent > 0f && direction != 0f)
                    scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + direction * 350f * Time.unscaledDeltaTime / extent);
            }
        }

        public void OpenHistory()
        {
            if (canvas == null || historyOpen || ExternalScreenOpen) return;
            historyOpen = true;
            AcquireInput();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            dialogue.gameObject.SetActive(false);
            historyPanel.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            var text = new StringBuilder();
            foreach (string entry in history) text.Append(entry).Append("\n\n");
            historyBody.text = history.Count == 0 ? "Здесь появятся прочитанные реплики и мысли." : text.ToString();
            historyContent.sizeDelta = new Vector2(0f, Mathf.Max(400f, historyBody.preferredHeight + 20f));
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }

        public void CloseHistory()
        {
            if (!historyOpen) return;
            historyOpen = false;
            historyPanel.gameObject.SetActive(false);
            if (important)
            {
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
            }
            else StartCoroutine(ReleaseAfterFrame());
        }

        private IEnumerator ReleaseAfterFrame()
        {
            yield return null;
            if (!historyOpen && !important) ReleaseInput();
        }

        private void AcquireInput()
        {
            if (ownsInput) return;
            player = FindFirstObjectByType<PlayerController>();
            interaction = player != null ? player.GetComponent<PlayerInteraction>() : null;
            doorInput = player != null ? player.GetComponentInChildren<PlayerDoorOpen>() : null;
            previousDoorInputEnabled = doorInput != null && doorInput.enabled;
            if (doorInput != null) doorInput.enabled = false;
            previousTimeScale = Time.timeScale;
            previousCameraLock = player != null && player.isCameraLocked;
            previousInteractionEnabled = interaction != null && interaction.enabled;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            ownsInput = true;
            IsModal = true;
            Time.timeScale = 0f;
            if (player != null) { player.StopMotion(); player.isCameraLocked = true; }
            if (interaction != null) interaction.enabled = false;
        }

        private void ReleaseInput()
        {
            if (!ownsInput) return;
            ownsInput = false;
            IsModal = false;
            Time.timeScale = previousTimeScale;
            if (player != null) player.isCameraLocked = previousCameraLock;
            if (interaction != null) interaction.enabled = previousInteractionEnabled;
            if (doorInput != null) doorInput.enabled = previousDoorInputEnabled;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        private void OnDisable() => StopPresentation();
        private void OnDestroy() => ReleaseInput();

        private void ChangeSize(float amount)
        {
            textScale = Mathf.Clamp(textScale + amount, .85f, 1.3f);
            PlayerPrefs.SetFloat(ScaleKey, textScale);
            layoutVersion++;
            body.fontSize = 28f * textScale;
            historyBody.fontSize = 24f * textScale;
            sizeLabel.text = "ТЕКСТ " + Mathf.RoundToInt(textScale * 100f) + "%";
            if (historyOpen)
                historyContent.sizeDelta = new Vector2(0f, Mathf.Max(400f, historyBody.preferredHeight + 20f));
        }

        private void ToggleAuto()
        {
            autoAdvance = !autoAdvance;
            PlayerPrefs.SetInt(AutoKey, autoAdvance ? 1 : 0);
            autoLabel.text = "АВТО: " + (autoAdvance ? "ВКЛ" : "ВЫКЛ");
        }

        private void BuildInterface()
        {
            var root = new GameObject("NarrativeReadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 520;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            dialogue = Rect("ReadingPanel", root.transform, new Vector2(.5f, .08f), new Vector2(930, 208));
            dialogue.pivot = new Vector2(.5f, 0);
            var image = dialogue.gameObject.AddComponent<Image>();
            image.color = new Color(.035f, .033f, .029f, .94f);
            image.raycastTarget = false;
            speakerText = Text("Speaker", dialogue, new Vector2(32, -18), new Vector2(860, 28), 18);
            speakerText.color = new Color32(193, 166, 119, 255);
            body = Text("Dialogue", dialogue, new Vector2(32, -54), new Vector2(866, 106), 28 * textScale);
            body.alignment = TextAlignmentOptions.TopLeft;
            body.overflowMode = TextOverflowModes.Page;
            hint = Text("Continue", dialogue, new Vector2(32, -174), new Vector2(866, 24), 18);
            hint.color = new Color32(172, 167, 153, 255);
            dialogue.gameObject.SetActive(false);
            historyPanel = Rect("DialogueHistory", root.transform, Vector2.one * .5f, new Vector2(980, 620));
            historyPanel.gameObject.AddComponent<Image>().color = new Color(.035f, .033f, .029f, .98f);
            Text("Title", historyPanel, new Vector2(34, -25), new Vector2(820, 42), 30).text = "ИСТОРИЯ РЕПЛИК";
            Button("CloseHistory", historyPanel, new Vector2(836, -22), new Vector2(110, 42), "ЗАКРЫТЬ", CloseHistory);
            var viewport = Rect("HistoryViewport", historyPanel, new Vector2(0, 1), new Vector2(912, 430));
            viewport.pivot = new Vector2(0, 1);
            viewport.anchoredPosition = new Vector2(34, -92);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewport;
            historyContent = Rect("HistoryContent", viewport, new Vector2(.5f, 1), new Vector2(0, 430));
            historyContent.anchorMin = new Vector2(0, 1);
            historyContent.anchorMax = new Vector2(1, 1);
            historyContent.pivot = new Vector2(.5f, 1);
            historyBody = Text("HistoryText", historyContent, Vector2.zero, Vector2.zero, 24 * textScale);
            historyBody.rectTransform.anchorMin = Vector2.zero;
            historyBody.rectTransform.anchorMax = Vector2.one;
            historyBody.rectTransform.offsetMin = new Vector2(0, 0);
            historyBody.rectTransform.offsetMax = new Vector2(-14, 0);
            historyBody.overflowMode = TextOverflowModes.Overflow;
            scroll.content = historyContent;
            autoLabel = Button("AutoAdvance", historyPanel, new Vector2(34, -548), new Vector2(220, 42),
                "АВТО: " + (autoAdvance ? "ВКЛ" : "ВЫКЛ"), ToggleAuto);
            Button("SmallerText", historyPanel, new Vector2(290, -548), new Vector2(54, 42), "−", () => ChangeSize(-.1f));
            sizeLabel = Text("TextSize", historyPanel, new Vector2(360, -556), new Vector2(180, 28), 18);
            sizeLabel.text = "ТЕКСТ " + Mathf.RoundToInt(textScale * 100f) + "%";
            Button("LargerText", historyPanel, new Vector2(540, -548), new Vector2(54, 42), "+", () => ChangeSize(.1f));
            Text("HistoryControls", historyPanel, new Vector2(628, -558), new Vector2(320, 24), 15).text = "КОЛЕСО — ПРОКРУТКА   •   H / ESC";
            historyPanel.gameObject.SetActive(false);
        }

        private TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), size);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = fontSize;
            text.color = new Color32(234, 230, 219, 255);
            text.richText = false;
            text.raycastTarget = false;
            text.text = "";
            return text;
        }

        private TextMeshProUGUI Button(string name, Transform parent, Vector2 position, Vector2 size, string label, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), size);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.16f, .15f, .12f, 1f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = Text(name + "Label", rect, new Vector2(4, -4), size - new Vector2(8, 8), 18);
            text.alignment = TextAlignmentOptions.Center;
            text.text = label;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = size;
            return rect;
        }
    }
}
