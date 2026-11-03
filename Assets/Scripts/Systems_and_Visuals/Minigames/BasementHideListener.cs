using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // B-05: прятки в подвале — тот же приём, что под кроватью (M-07): игра слушает микрофон (VoiceNoiseGate: фон
    // комнаты подстраивается, шёпот можно, голос нельзя). Микрофона нет вообще — монстр просто не слышит.
    // Окна нет: это подача в мире, на экране только «НЕ ГОВОРИ НИ СЛОВА» и шкала громкости.
    public sealed class BasementHideListener : MonoBehaviour
    {
        private readonly HideMicrophone mic = new HideMicrophone();
        private VoiceNoiseGate voice;
        private GameObject ui;
        private RectTransform meterFill;
        private Image meterBar, vignette;
        private TextMeshProUGUI meterLabel;
        private float beat, ignoreLeft;

        public bool Active { get; private set; }
        public bool UsesMicrophone => mic.Available;
        public bool Found => Active && UsesMicrophone && voice.Found;
        public float Suspicion => voice != null ? voice.Suspicion : 0f;

        private void Awake() => mic.DeviceChanged += OnMicrophoneChanged;

        public void Begin()
        {
            End();
            voice = new VoiceNoiseGate();
            Active = true;
            mic.Start();
            BuildUi();
        }

        public void End()
        {
            Active = false;
            mic.Stop();
            if (ui != null) Destroy(ui);
            ui = null;
        }

        // Сюжетный стук из колонок попадает в микрофон: на это время его не считаем голосом игрока.
        public void IgnoreSoundFor(float seconds) => ignoreLeft = Mathf.Max(ignoreLeft, seconds);

        // Шаг игры. monsterDistance — до монстра (ближе — слышнее).
        public void Tick(float dt, float monsterDistance)
        {
            if (!Active || dt <= 0f || NarrativeReadingPanel.WorldHalted || PauseMenuController.InputBlocked) return;
            mic.Sample();
            ignoreLeft -= dt;
            if (UsesMicrophone && ignoreLeft <= 0f) voice.Advance(dt, mic.Level, monsterDistance);
            UpdateUi(dt);
        }

        private void OnDestroy() => End();

        // Новое устройство: фон комнаты меряется заново, шкала появляется или исчезает.
        private void OnMicrophoneChanged()
        {
            voice = new VoiceNoiseGate();
            if (Active) BuildUi();
        }

        // ---------------------------------------------------------------- экран

        private void BuildUi()
        {
            if (ui != null) Destroy(ui);
            TMP_FontAsset font = UiStyle.Font();
            ui = new GameObject("Basement Hide UI", typeof(RectTransform), typeof(Canvas));
            ui.transform.SetParent(transform, false);
            var canvas = ui.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            vignette = UiStyle.Fill(ui.transform, "Vignette", new Color(0f, 0f, 0f, 0.25f));
            vignette.raycastTarget = false;

            if (UsesMicrophone)
            {
                var title = UiStyle.Text("Silence", ui.transform, font, 34f, UiStyle.Ivory, TextAlignmentOptions.Center);
                title.rectTransform.anchorMin = title.rectTransform.anchorMax = title.rectTransform.pivot = new Vector2(0.5f, 1f);
                title.rectTransform.anchoredPosition = new Vector2(0f, -70f);
                title.rectTransform.sizeDelta = new Vector2(1000f, 46f);
                title.text = Loc.T("НЕ ГОВОРИ НИ СЛОВА");
                const float width = 320f;
                var meter = UiStyle.Rect("Meter", ui.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 96f), new Vector2(width, 8f));
                meter.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
                meterFill = UiStyle.Rect("Level", meter, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 8f));
                meterBar = meterFill.gameObject.AddComponent<Image>();
                meterBar.raycastTarget = false;
                var limit = UiStyle.Rect("Limit", meter, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(width / VoiceNoiseGate.Headroom, 0f), new Vector2(2f, 18f));
                limit.gameObject.AddComponent<Image>().color = UiStyle.Rust;
                meterLabel = UiStyle.Text("Mic", ui.transform, font, 16f, UiStyle.Ash, TextAlignmentOptions.Center);
                meterLabel.rectTransform.anchorMin = meterLabel.rectTransform.anchorMax = meterLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
                meterLabel.rectTransform.anchoredPosition = new Vector2(0f, 108f);
                meterLabel.rectTransform.sizeDelta = new Vector2(600f, 24f);
            }
        }

        private void UpdateUi(float dt)
        {
            if (ui == null) return;
            beat = Mathf.MoveTowards(beat, 0f, dt * 5f);
            vignette.color = new Color(0f, 0f, 0f, Mathf.Clamp01(0.3f + 0.2f * voice.Pulse));
            if (!UsesMicrophone || meterFill == null) return;
            float level = voice.Loudness;
            meterFill.sizeDelta = new Vector2(320f * level / VoiceNoiseGate.Headroom, 8f);
            Color ivory = UiStyle.Ivory;
            meterBar.color = level >= 1f ? UiStyle.Rust : level > 0.7f ? UiStyle.Amber : new Color(ivory.r, ivory.g, ivory.b, 0.8f);
            meterLabel.text = !voice.Calibrated ? Loc.T("МИКРОФОН · СЛУШАЮ ТИШИНУ…") : level >= 1f ? Loc.T("СЛИШКОМ ГРОМКО") : Loc.T("МИКРОФОН");
            meterLabel.color = level >= 1f ? UiStyle.Rust : UiStyle.Ash;
        }
    }
}
