using System;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Удержание клавиши пропуска. Отсчёт идёт только с нажатия: клавиша, зажатая ещё до
    // подсказки или до паузы, не считается. Отпустил — шкала быстро стекает обратно.
    public sealed class CutsceneSkipHold
    {
        public const float HoldSeconds = 1.5f;
        public const float DrainSeconds = .4f;

        private bool holding;

        public float Progress { get; private set; }
        public bool Completed => Progress >= 1f;

        public bool Advance(float deltaTime, bool pressed, bool held)
        {
            if (Completed) return true;
            holding = held && (holding || pressed);
            Progress = holding
                ? Mathf.Min(1f, Progress + deltaTime / HoldSeconds)
                : Mathf.Max(0f, Progress - deltaTime / DrainSeconds);
            return Completed;
        }
    }

    // Подсказка «зажми пробел, чтобы пропустить» со шкалой удержания. Свой холст, потому что
    // чёрный экран катсцены гаснет вместе со всем своим содержимым, а подсказка должна
    // оставаться видна и на съёмочных планах.
    public sealed class CutsceneSkipPrompt : MonoBehaviour
    {
        private const float AppearDelay = .6f, FadeSeconds = .35f, BrightSeconds = 6f, DimmedAlpha = .5f;

        private readonly CutsceneSkipHold hold = new CutsceneSkipHold();
        private Action onSkip;
        private CanvasGroup group;
        private RectTransform fill;
        private UiKeyHints hints;
        private Image track;
        private bool? shownHolding;
        private float clock, brightUntil = AppearDelay + BrightSeconds, shownProgress;

        public static CutsceneSkipPrompt Create(Action onSkip)
        {
            var root = new GameObject("Cutscene skip prompt", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5001; // над чёрным экраном StoryIntroScreen, под паузой
            UiStyle.ScaleWithScreen(root.GetComponent<CanvasScaler>(), new Vector2(1280f, 720f));
            var prompt = root.AddComponent<CutsceneSkipPrompt>();
            prompt.onSkip = onSkip;
            prompt.Build();
            return prompt;
        }

        private void Build()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;

            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 0f);
            panel.anchoredPosition = new Vector2(-56f, 96f);
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 8f;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            hints = UiKeyHints.Create(panel, "Hint", null, .85f);

            track = new GameObject("Track", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            track.transform.SetParent(panel, false);
            track.color = new Color(UiStyle.Ivory.r, UiStyle.Ivory.g, UiStyle.Ivory.b, .25f);
            track.raycastTarget = false;
            track.gameObject.AddComponent<LayoutElement>().preferredHeight = 4f;

            var bar = new GameObject("Fill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            bar.transform.SetParent(track.transform, false);
            bar.color = UiStyle.Ivory;
            bar.raycastTarget = false;
            fill = bar.rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            ShowHolding(false);
        }

        // В покое — одна клавиша; пока пробел зажат — пояснение и шкала удержания.
        private void ShowHolding(bool holding)
        {
            if (holding == shownHolding) return;
            shownHolding = holding;
            hints.Set("ПРОБЕЛ", holding ? "Удерживать, чтобы пропустить" : "");
            track.gameObject.SetActive(holding);
        }

        private void Update()
        {
            bool blocked = PauseMenuController.InputBlocked || Time.timeScale <= 0f;
            if (blocked)
            {
                group.alpha = 0f; // под экраном паузы подсказка не просвечивает
                hold.Advance(Time.unscaledDeltaTime, false, false);
                ShowProgress();
                return;
            }

            clock += Time.unscaledDeltaTime;
            bool visible = clock >= AppearDelay;
            if (Input.anyKeyDown) brightUntil = clock + BrightSeconds;
            bool skipped = hold.Advance(Time.unscaledDeltaTime, visible && Input.GetKeyDown(KeyCode.Space),
                visible && Input.GetKey(KeyCode.Space));
            ShowProgress();
            float target = !visible ? 0f : clock < brightUntil || hold.Progress > 0f ? 1f : DimmedAlpha;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime / FadeSeconds);
            if (!skipped) return;
            enabled = false;
            onSkip?.Invoke();
        }

        private void ShowProgress()
        {
            ShowHolding(hold.Progress > 0f);
            if (Mathf.Approximately(shownProgress, hold.Progress)) return;
            shownProgress = hold.Progress;
            fill.anchorMax = new Vector2(shownProgress, 1f);
        }
    }
}
