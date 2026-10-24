using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    public enum NarrativeStyle
    {
        Speech,
        Thought,
        Note
    }

    // Реплики и мысли подаются как субтитры с VHS-кассеты: текст прямо на картинке,
    // у каждой строки своя чёрная подложка. Записки — отдельный лист бумаги по центру.
    public sealed class NarrativeReadingPanel : MonoBehaviour
    {
        public static bool IsModal { get; private set; }
        private const string ScaleKey = "Narrative.TextScale";
        private const string AutoKey = "Narrative.AutoAdvance";
        private const float BodySize = 26f, BodyWidth = 880f, MaxLines = 3f;
        private const float FadeSeconds = .14f;
        private readonly List<string> history = new List<string>(200);
        private readonly List<RectTransform> lineBackings = new List<RectTransform>(4);
        private RectTransform captionRoot, backingLayer, speakerBacking, cursor, noteCard, historyPanel, historyContent;
        private CanvasGroup captionGroup, noteGroup, historyGroup;
        private TextMeshProUGUI body, speakerText, noteTitle, noteBody, historyBody;
        private Image cursorImage;
        private UiKeyHints hints, historyHints;
        private UiMenuItem autoItem, closeItem;
        private TextMeshProUGUI sizeLabel;
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
        private NarrativeStyle style;
        private bool captionRequested, noteRequested, suspended, waitingForInput;
        private int shownVisible = -1, shownPage = -1;
        private float cursorBlink;
        private readonly StringBuilder noteText = new StringBuilder(256);

        public bool IsHistoryOpen => historyOpen;
        public bool AutoAdvance => autoAdvance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => IsModal = false;

        public void Initialize(TMP_FontAsset sourceFont)
        {
            if (canvas != null) return;
            font = UiStyle.Font(sourceFont);
            textScale = Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, 1f), .85f, 1.3f);
            autoAdvance = PlayerPrefs.GetInt(AutoKey, 0) != 0;
            BuildInterface();
        }

        public IEnumerator BeginSequence(bool requireConfirmation) => BeginSequence(requireConfirmation, NarrativeStyle.Speech);

        public IEnumerator BeginSequence(bool requireConfirmation, NarrativeStyle presentation)
        {
            while (ExternalScreenOpen || Time.timeScale <= 0f) yield return null;
            important = requireConfirmation;
            style = presentation;
            layoutVersion++;
            generation++;
            noteText.Length = 0;
            if (important) AcquireInput();
        }

        public IEnumerator ShowLine(string line, string speaker, float typingInterval, float minimumDuration)
        {
            if (string.IsNullOrWhiteSpace(line)) yield break;
            int version = generation;
            while (ExternalScreenOpen || historyOpen) yield return null;
            if (version != generation) yield break;
            AddHistory(speaker, line);
            if (style == NarrativeStyle.Note)
            {
                yield return ShowNoteLine(line, speaker, version, minimumDuration);
                yield break;
            }
            ApplySpeaker(speaker);
            ApplyBodyStyle();
            captionRequested = true;
            // Неактивный TMP не пересчитывает разметку: без этого первая реплика разбивается на страницы по букве.
            if (!captionGroup.gameObject.activeSelf)
            {
                captionGroup.alpha = 0f;
                captionGroup.gameObject.SetActive(true);
            }
            body.text = line;
            body.overflowMode = TextOverflowModes.Page;
            body.maxVisibleCharacters = int.MaxValue;
            body.ForceMeshUpdate(true);
            int pages = Mathf.Max(1, body.textInfo.pageCount);
            int layout = layoutVersion;
            for (int page = 0; page < pages && version == generation; page++)
            {
                body.pageToDisplay = page + 1;
                int first = body.textInfo.pageInfo[page].firstCharacterIndex;
                int end = body.textInfo.pageInfo[page].lastCharacterIndex + 1;
                int visible = important ? first : end;
                body.maxVisibleCharacters = visible;
                shownVisible = -1;
                LayoutCaption(page, first, visible);
                float typed = 0f, read = 0f, inputDelay = .18f;
                int wordCount = 1;
                for (int i = first; i < end; i++)
                    if (char.IsWhiteSpace(body.textInfo.characterInfo[i].character)) wordCount++;
                float readingSeconds = Mathf.Max(minimumDuration, .8f + wordCount / 3.2f);
                while (version == generation)
                {
                    suspended = historyOpen || ExternalScreenOpen || (!important && Time.timeScale <= 0f);
                    if (suspended) { yield return null; continue; }
                    if (layout != layoutVersion)
                    {
                        ApplyBodyStyle();
                        body.ForceMeshUpdate(true);
                        pages = Mathf.Max(1, body.textInfo.pageCount);
                        page = Mathf.Min(page, pages - 1);
                        body.pageToDisplay = page + 1;
                        first = body.textInfo.pageInfo[page].firstCharacterIndex;
                        end = body.textInfo.pageInfo[page].lastCharacterIndex + 1;
                        visible = Mathf.Clamp(visible, first, end);
                        body.maxVisibleCharacters = visible;
                        shownVisible = -1;
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
                        waitingForInput = false;
                    }
                    else
                    {
                        read += Time.unscaledDeltaTime;
                        waitingForInput = important && !autoAdvance;
                        if ((important && advance) || ((!important || autoAdvance) && read >= readingSeconds)) break;
                    }
                    UpdateHints();
                    LayoutCaption(page, first, visible);
                    yield return null;
                }
                // Release input on a later frame so the advance click cannot interact with the world.
                yield return null;
            }
            body.maxVisibleCharacters = int.MaxValue;
            waitingForInput = false;
        }

        private IEnumerator ShowNoteLine(string line, string title, int version, float minimumDuration)
        {
            noteTitle.text = SentenceCase(title);
            if (noteText.Length > 0) noteText.Append("\n\n");
            noteText.Append(line);
            noteBody.text = noteText.ToString();
            noteBody.fontSize = 23f * textScale;
            noteRequested = true;
            float read = 0f, inputDelay = .3f;
            int words = 1;
            foreach (char c in line) if (char.IsWhiteSpace(c)) words++;
            float readingSeconds = Mathf.Max(minimumDuration, .8f + words / 3.2f);
            while (version == generation)
            {
                suspended = historyOpen || ExternalScreenOpen || (!important && Time.timeScale <= 0f);
                if (suspended) { yield return null; continue; }
                inputDelay -= Time.unscaledDeltaTime;
                read += Time.unscaledDeltaTime;
                bool advance = important && inputDelay <= 0f && AdvancePressed();
                waitingForInput = important && !autoAdvance;
                UpdateHints();
                if (advance || ((!important || autoAdvance) && read >= readingSeconds)) break;
                yield return null;
            }
            yield return null;
            waitingForInput = false;
        }

        public void EndSequence()
        {
            generation++;
            important = false;
            captionRequested = false;
            noteRequested = false;
            waitingForInput = false;
            suspended = false;
            if (!historyOpen) ReleaseInput();
        }

        public void StopPresentation()
        {
            historyOpen = false;
            if (historyPanel != null) historyPanel.gameObject.SetActive(false);
            EndSequence();
            if (captionGroup != null) HideCaptionNow();
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
                historyGroup.alpha = Mathf.MoveTowards(historyGroup.alpha, 1f, Time.unscaledDeltaTime / FadeSeconds);
            }
            bool hidden = suspended || historyOpen;
            Fade(captionGroup, captionRequested && !hidden);
            Fade(noteGroup, noteRequested && !hidden);
            hints.gameObject.SetActive(important && (captionRequested || noteRequested) && !hidden);
            if (cursor.gameObject.activeSelf && waitingForInput)
            {
                cursorBlink += Time.unscaledDeltaTime;
                cursorImage.enabled = Mathf.Repeat(cursorBlink, 1f) < .55f;
            }
            else
            {
                cursorBlink = 0f;
                cursorImage.enabled = true;
            }
        }

        private static void Fade(CanvasGroup group, bool show)
        {
            if (!show && !group.gameObject.activeSelf) return;
            if (show && !group.gameObject.activeSelf) { group.alpha = 0f; group.gameObject.SetActive(true); }
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime / FadeSeconds);
            if (!show && group.alpha <= 0f) group.gameObject.SetActive(false);
        }

        private void HideCaptionNow()
        {
            captionGroup.alpha = 0f;
            captionGroup.gameObject.SetActive(false);
            noteGroup.alpha = 0f;
            noteGroup.gameObject.SetActive(false);
            hints.gameObject.SetActive(false);
        }

        private void UpdateHints()
        {
            if (!important) return;
            if (autoAdvance) hints.Set("H", "ИСТОРИЯ", "", "АВТО");
            else hints.Set("ЛКМ", "ДАЛЕЕ", "H", "ИСТОРИЯ");
        }

        private void ApplySpeaker(string speaker)
        {
            bool thought = style == NarrativeStyle.Thought;
            bool named = !thought && !string.IsNullOrEmpty(speaker);
            speakerBacking.gameObject.SetActive(named);
            speakerText.text = named ? speaker : string.Empty;
            speakerText.color = speaker == "Я" ? UiStyle.Ash : UiStyle.Amber;
        }

        private void ApplyBodyStyle()
        {
            bool thought = style == NarrativeStyle.Thought;
            body.fontSize = BodySize * textScale;
            body.color = thought ? UiStyle.Thought : UiStyle.Ivory;
            body.rectTransform.sizeDelta = new Vector2(BodyWidth, BodySize * textScale * 1.3f * MaxLines);
            ((RectTransform)backingLayer).sizeDelta = body.rectTransform.sizeDelta;
            Color ink = thought ? UiStyle.InkSoft : UiStyle.Ink;
            foreach (RectTransform backing in lineBackings) backing.GetComponent<Image>().color = ink;
            speakerText.fontSize = 17f * textScale;
            speakerBacking.GetComponent<Image>().color = UiStyle.Ink;
            shownVisible = -1;
        }

        // Подложки строятся только по уже напечатанным символам, поэтому растут вместе с текстом.
        private void LayoutCaption(int page, int first, int visible)
        {
            if (visible == shownVisible && page == shownPage) return;
            shownVisible = visible;
            shownPage = page;
            TMP_TextInfo info = body.textInfo;
            float padX = 10f * textScale, padY = 4f * textScale;
            int used = 0, pageEnd = info.pageInfo[page].lastCharacterIndex;
            float top = float.MinValue;
            Vector2 cursorPosition = Vector2.zero;
            bool hasCursor = false;
            for (int l = 0; l < info.lineCount; l++)
            {
                TMP_LineInfo line = info.lineInfo[l];
                int start = line.firstVisibleCharacterIndex;
                if (line.firstCharacterIndex < first || line.firstCharacterIndex > pageEnd || start < 0 || start >= info.characterCount) continue;
                float descender = line.descender;
                top = Mathf.Max(top, line.ascender);
                if (start >= visible)
                {
                    // Курсор ждёт в начале ещё не напечатанной строки, только если перед ней ничего нет.
                    if (!hasCursor) cursorPosition = new Vector2(info.characterInfo[start].bottomLeft.x, descender);
                    hasCursor = true;
                    continue;
                }
                int last = Mathf.Min(line.lastVisibleCharacterIndex, visible - 1);
                float x0 = info.characterInfo[start].bottomLeft.x, x1 = info.characterInfo[last].topRight.x;
                RectTransform backing = Backing(used++);
                backing.anchoredPosition = new Vector2(x0 - padX, descender - padY);
                backing.sizeDelta = new Vector2(x1 - x0 + padX * 2f, line.ascender - descender + padY * 2f);
                cursorPosition = new Vector2(x1 + 3f * textScale, descender);
                hasCursor = true;
            }
            for (int i = used; i < lineBackings.Count; i++) lineBackings[i].gameObject.SetActive(false);
            if (top == float.MinValue) top = 0f;
            float height = BodySize * textScale * .92f;
            cursor.gameObject.SetActive(hasCursor && important);
            cursor.anchoredPosition = cursorPosition + new Vector2(0f, BodySize * textScale * .02f);
            cursor.sizeDelta = new Vector2(BodySize * textScale * .5f, height * .82f);
            Vector2 speakerSize = speakerText.GetPreferredValues(speakerText.text);
            speakerBacking.sizeDelta = new Vector2(speakerSize.x + padX * 2f, speakerSize.y + padY * 2f);
            speakerBacking.anchoredPosition = new Vector2(0f, top + padY + 10f * textScale);
        }

        private RectTransform Backing(int index)
        {
            while (lineBackings.Count <= index)
            {
                var rect = UiStyle.Rect("Line" + lineBackings.Count, backingLayer, PivotAnchor, Vector2.zero, Vector2.zero, Vector2.zero);
                var image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                image.color = style == NarrativeStyle.Thought ? UiStyle.InkSoft : UiStyle.Ink;
                lineBackings.Add(rect);
            }
            RectTransform backing = lineBackings[index];
            backing.gameObject.SetActive(true);
            return backing;
        }

        // Подложки и курсор позиционируются в локальных координатах текста: начало — в его опорной точке.
        private static readonly Vector2 PivotAnchor = new Vector2(.5f, 0f);

        private void AddHistory(string speaker, string line)
        {
            string escaped = "<noparse>" + line.Replace("</noparse>", string.Empty) + "</noparse>";
            string entry;
            if (style == NarrativeStyle.Thought)
                entry = "<color=" + UiStyle.Hex(UiStyle.Thought) + ">" + escaped + "</color>";
            else
            {
                Color label = speaker == "Я" ? UiStyle.Ash : UiStyle.Amber;
                entry = "<size=70%><color=" + UiStyle.Hex(label) + "><noparse>" + speaker + "</noparse></color></size>\n" + escaped;
            }
            history.Add(entry);
            if (history.Count > 200) history.RemoveAt(0);
        }

        private static string SentenceCase(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string lower = value.ToLowerInvariant();
            return char.ToUpperInvariant(lower[0]) + lower.Substring(1);
        }

        public void OpenHistory()
        {
            if (canvas == null || historyOpen || ExternalScreenOpen) return;
            historyOpen = true;
            AcquireInput();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            historyPanel.gameObject.SetActive(true);
            historyGroup.alpha = 0f;
            Canvas.ForceUpdateCanvases();
            var text = new StringBuilder();
            foreach (string entry in history) text.Append(entry).Append("\n\n");
            historyBody.text = history.Count == 0 ? "Здесь появятся прочитанные реплики и мысли." : text.ToString();
            historyContent.sizeDelta = new Vector2(0f, Mathf.Max(400f, historyBody.preferredHeight + 20f));
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
            UiMenuItem.Select(closeItem);
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
            historyBody.fontSize = 24f * textScale;
            sizeLabel.text = SizeCaption;
            if (historyOpen)
                historyContent.sizeDelta = new Vector2(0f, Mathf.Max(400f, historyBody.preferredHeight + 20f));
        }

        private void ToggleAuto()
        {
            autoAdvance = !autoAdvance;
            PlayerPrefs.SetInt(AutoKey, autoAdvance ? 1 : 0);
            autoItem.SetCaption(AutoCaption);
        }

        private string SizeCaption => "Размер текста: " + Mathf.RoundToInt(textScale * 100f) + "%";
        private string AutoCaption =>"Автопродолжение: " + (autoAdvance ? "вкл" : "выкл");

        private void BuildInterface()
        {
            canvas = UiStyle.OverlayCanvas(transform, "NarrativeReadingCanvas", 520, new Vector2(1280, 720), true);
            Transform root = canvas.transform;

            captionRoot = UiStyle.Rect("Captions", root, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, 96f), new Vector2(BodyWidth, 240f));
            captionGroup = captionRoot.gameObject.AddComponent<CanvasGroup>();
            captionGroup.blocksRaycasts = captionGroup.interactable = false;
            backingLayer = UiStyle.Rect("LineBackings", captionRoot, new Vector2(.5f, 0f), new Vector2(.5f, 0f), Vector2.zero, new Vector2(BodyWidth, 100f));
            speakerBacking = UiStyle.Rect("SpeakerBacking", backingLayer, PivotAnchor, new Vector2(.5f, 0f), Vector2.zero, Vector2.zero);
            speakerBacking.gameObject.AddComponent<Image>().raycastTarget = false;
            speakerText = UiStyle.Text("Speaker", speakerBacking, font, 17f, UiStyle.Amber, TextAlignmentOptions.Center);
            StretchInside(speakerText.rectTransform);
            body = UiStyle.Text("Dialogue", captionRoot, font, BodySize * textScale, UiStyle.Ivory, TextAlignmentOptions.Bottom);
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(.5f, 0f);
            body.rectTransform.pivot = new Vector2(.5f, 0f);
            body.rectTransform.anchoredPosition = Vector2.zero;
            body.overflowMode = TextOverflowModes.Page;
            body.lineSpacing = 22f;
            cursor = UiStyle.Rect("Cursor", body.rectTransform, PivotAnchor, Vector2.zero, Vector2.zero, Vector2.zero);
            cursorImage = cursor.gameObject.AddComponent<Image>();
            cursorImage.color = UiStyle.Ivory;
            cursorImage.raycastTarget = false;
            captionRoot.gameObject.SetActive(false);

            RectTransform noteRoot = UiStyle.Stretch("Note", root);
            noteGroup = noteRoot.gameObject.AddComponent<CanvasGroup>();
            noteGroup.blocksRaycasts = noteGroup.interactable = false;
            UiStyle.Fill(noteRoot, "Dim", new Color(0f, 0f, 0f, .55f));
            var shadow = UiStyle.Rect("Shadow", noteRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(12f, 10f), new Vector2(560f, 360f));
            shadow.localRotation = Quaternion.Euler(0f, 0f, -1.4f);
            shadow.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .7f);
            noteCard = UiStyle.Rect("Paper", noteRoot, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0f, 24f), new Vector2(560f, 360f));
            noteCard.localRotation = Quaternion.Euler(0f, 0f, -1.4f);
            noteCard.gameObject.AddComponent<Image>().color = UiStyle.Paper;
            noteTitle = UiStyle.Text("NoteTitle", noteCard, font, 16f, new Color(UiStyle.PaperInk.r, UiStyle.PaperInk.g, UiStyle.PaperInk.b, .6f));
            noteTitle.rectTransform.anchorMin = noteTitle.rectTransform.anchorMax = new Vector2(0f, 1f);
            noteTitle.rectTransform.pivot = new Vector2(0f, 1f);
            noteTitle.rectTransform.anchoredPosition = new Vector2(44f, -34f);
            noteTitle.rectTransform.sizeDelta = new Vector2(472f, 26f);
            var rule = UiStyle.Rect("Rule", noteCard, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -66f), new Vector2(472f, 2f));
            rule.gameObject.AddComponent<Image>().color = new Color(UiStyle.PaperInk.r, UiStyle.PaperInk.g, UiStyle.PaperInk.b, .35f);
            noteBody = UiStyle.Text("NoteBody", noteCard, font, 23f, UiStyle.PaperInk);
            noteBody.rectTransform.anchorMin = noteBody.rectTransform.anchorMax = new Vector2(0f, 1f);
            noteBody.rectTransform.pivot = new Vector2(0f, 1f);
            noteBody.rectTransform.anchoredPosition = new Vector2(44f, -88f);
            noteBody.rectTransform.sizeDelta = new Vector2(472f, 250f);
            noteBody.lineSpacing = 18f;
            noteRoot.gameObject.SetActive(false);

            hints = UiKeyHints.Create(root, "AdvanceHints", font);
            hints.RectTransform.anchorMin = hints.RectTransform.anchorMax = hints.RectTransform.pivot = new Vector2(1f, 0f);
            hints.RectTransform.anchoredPosition = new Vector2(-30f, 30f);
            hints.gameObject.SetActive(false);

            historyPanel = UiStyle.Stretch("DialogueHistory", root);
            historyGroup = historyPanel.gameObject.AddComponent<CanvasGroup>();
            UiStyle.Fill(historyPanel, "Dim", UiStyle.Dim).raycastTarget = true;
            UiStyle.Scanlines(historyPanel);
            var title = UiStyle.Text("Title", historyPanel, font, 30f, UiStyle.Amber);
            Place(title.rectTransform, new Vector2(120f, -64f), new Vector2(700f, 40f));
            title.text = "► ИСТОРИЯ";
            var viewport = UiStyle.Rect("HistoryViewport", historyPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -130f), new Vector2(760f, 470f));
            viewport.anchorMin = new Vector2(0f, 0f);
            viewport.anchorMax = new Vector2(0f, 1f);
            viewport.offsetMin = new Vector2(120f, 130f);
            viewport.offsetMax = new Vector2(800f, -130f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewport;
            historyContent = UiStyle.Rect("HistoryContent", viewport, new Vector2(.5f, 1f), new Vector2(.5f, 1f), Vector2.zero, new Vector2(0f, 430f));
            historyContent.anchorMin = new Vector2(0f, 1f);
            historyContent.anchorMax = new Vector2(1f, 1f);
            historyBody = UiStyle.Text("HistoryText", historyContent, font, 24f * textScale, UiStyle.Ivory);
            historyBody.richText = true;
            historyBody.lineSpacing = 12f;
            historyBody.rectTransform.anchorMin = Vector2.zero;
            historyBody.rectTransform.anchorMax = Vector2.one;
            historyBody.rectTransform.offsetMin = Vector2.zero;
            historyBody.rectTransform.offsetMax = new Vector2(-14f, 0f);
            historyBody.overflowMode = TextOverflowModes.Overflow;
            scroll.content = historyContent;

            var options = UiStyle.Rect("Options", historyPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-50f, -124f), new Vector2(360f, 300f));
            var column = options.gameObject.AddComponent<VerticalLayoutGroup>();
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = column.childForceExpandHeight = false;
            column.spacing = 6f;
            autoItem = UiMenuItem.Create(options, AutoCaption, font, 20f, ToggleAuto);
            UiMenuItem smaller = UiMenuItem.Create(options, "Текст меньше", font, 20f, () => ChangeSize(-.1f));
            UiMenuItem larger = UiMenuItem.Create(options, "Текст крупнее", font, 20f, () => ChangeSize(.1f));
            sizeLabel = UiStyle.Text("TextSize", options, font, 17f, UiStyle.Ash);
            sizeLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            // Отступ совпадает с началом подписи пунктов меню: поле + маркер + интервал.
            sizeLabel.margin = new Vector4(32f, 6f, 0f, 0f);
            sizeLabel.text = SizeCaption;
            closeItem = UiMenuItem.Create(options, "Закрыть", font, 20f, CloseHistory);
            UiMenuItem.LinkVertical(new[] { closeItem, autoItem, smaller, larger });

            historyHints = UiKeyHints.Create(historyPanel, "HistoryHints", font);
            historyHints.RectTransform.anchorMin = historyHints.RectTransform.anchorMax = historyHints.RectTransform.pivot = new Vector2(1f, 0f);
            historyHints.RectTransform.anchoredPosition = new Vector2(-30f, 30f);
            historyHints.Set("H", "ЗАКРЫТЬ", "↑ ↓", "ЛИСТАТЬ");
            historyPanel.gameObject.SetActive(false);
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void StretchInside(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
