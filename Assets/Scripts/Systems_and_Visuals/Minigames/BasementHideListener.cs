using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // B-05: прятки в подвале — тот же приём, что под кроватью (M-07): игра слушает микрофон (VoiceNoiseGate: фон
    // комнаты подстраивается, шёпот можно, голос нельзя), без микрофона — задержка дыхания на Пробел (BreathHold).
    // Окна нет: это подача в мире, на экране только «НЕ ГОВОРИ НИ СЛОВА» и шкала громкости.
    public sealed class BasementHideListener : MonoBehaviour
    {
        private const float MicDeadSeconds = 2f;
        private const float MicStallSeconds = 0.5f;

        private readonly float[] micWindow = new float[1024];
        private VoiceNoiseGate voice;
        private BreathHold breath;
        private AudioClip micClip;
        private string micDevice;
        private int micPosition;
        private float micStalled, micLastSound, micLevel = VoiceNoiseGate.SilenceDb;
        private GameObject ui;
        private RectTransform meterFill, airFill;
        private Image meterBar, airBar, vignette;
        private TextMeshProUGUI meterLabel;
        private float beat;

        public bool Active { get; private set; }
        public bool UsesMicrophone => micClip != null;
        public bool Found => Active && (UsesMicrophone ? voice.Found : breath.Found);
        public float Suspicion => UsesMicrophone ? voice.Suspicion : breath.Suspicion;

        public void Begin()
        {
            End();
            voice = new VoiceNoiseGate();
            breath = new BreathHold();
            Active = true;
            StartMicrophone();
            BuildUi();
        }

        public void End()
        {
            Active = false;
            StopMicrophone();
            if (ui != null) Destroy(ui);
            ui = null;
        }

        // Шаг игры. monsterDistance — до монстра (ближе — слышнее), moving — игрок сейчас идёт.
        public void Tick(float dt, float monsterDistance, bool moving)
        {
            if (!Active || dt <= 0f || Time.timeScale <= 0f || PauseMenuController.InputBlocked) return;
            SampleMicrophone();
            if (UsesMicrophone) voice.Advance(dt, micLevel, monsterDistance);
            else breath.Advance(dt, Input.GetKey(KeyCode.Space) || Input.GetMouseButton(1), monsterDistance, moving);
            UpdateUi(dt);
        }

        private void OnDestroy() => End();

        // ---------------------------------------------------------------- микрофон

        private void StartMicrophone()
        {
            StopMicrophone();
            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0) return;
            micDevice = devices[0];
            micClip = Microphone.Start(micDevice, true, 1, 16000);
            micLevel = VoiceNoiseGate.SilenceDb;
            micPosition = 0;
            micStalled = 0f;
            micLastSound = Time.time;
        }

        private void StopMicrophone()
        {
            if (micClip != null && Microphone.IsRecording(micDevice)) Microphone.End(micDevice);
            micClip = null;
        }

        private void SampleMicrophone()
        {
            if (micClip == null) return;
            if (!Microphone.IsRecording(micDevice)) { MicrophoneLost(); return; }
            int position = Microphone.GetPosition(micDevice);
            if (position == micPosition)
            {
                micStalled += Time.unscaledDeltaTime;
                if (micStalled > MicStallSeconds) MicrophoneLost();
                return;
            }
            micPosition = position;
            micStalled = 0f;
            int offset = position - micWindow.Length;
            if (offset < 0) offset += micClip.samples;
            if (!micClip.GetData(micWindow, offset)) return;
            micLevel = VoiceNoiseGate.ToDb(micWindow, micWindow.Length);
            if (micLevel > VoiceNoiseGate.SilenceDb + 0.5f) micLastSound = Time.time;
            else if (Time.time - micLastSound > MicDeadSeconds) MicrophoneLost();
        }

        // Микрофон отключили или доступ запрещён: переходим на задержку дыхания.
        private void MicrophoneLost()
        {
            StopMicrophone();
            micLevel = VoiceNoiseGate.SilenceDb;
            BuildUi();
            ThoughtManager.Instance?.TryShowInteractionThoughts("basement-mic-lost",
                new[] { "Микрофон не слышно. Задерживай дыхание — Пробел." }, 0f);
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
                title.text = "НЕ ГОВОРИ НИ СЛОВА";
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
            else
            {
                var back = UiStyle.Rect("Air", ui.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 118f), new Vector2(180f, 3f));
                var backImage = back.gameObject.AddComponent<Image>();
                backImage.color = new Color(0f, 0f, 0f, 0.35f);
                backImage.raycastTarget = false;
                airFill = UiStyle.Rect("Fill", back, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(180f, 3f));
                airBar = airFill.gameObject.AddComponent<Image>();
                airBar.raycastTarget = false;
                var keys = UiKeyHints.Create(ui.transform, "Keys", font);
                keys.RectTransform.anchorMin = keys.RectTransform.anchorMax = keys.RectTransform.pivot = new Vector2(0.5f, 0f);
                keys.RectTransform.anchoredPosition = new Vector2(0f, 70f);
                keys.Set("ПРОБЕЛ", "ЗАДЕРЖАТЬ ДЫХАНИЕ");
            }
        }

        private void UpdateUi(float dt)
        {
            if (ui == null) return;
            beat = Mathf.MoveTowards(beat, 0f, dt * 5f);
            if (UsesMicrophone)
            {
                vignette.color = new Color(0f, 0f, 0f, Mathf.Clamp01(0.3f + 0.2f * voice.Pulse));
                if (meterFill == null) return;
                float level = voice.Loudness;
                meterFill.sizeDelta = new Vector2(320f * level / VoiceNoiseGate.Headroom, 8f);
                Color ivory = UiStyle.Ivory;
                meterBar.color = level >= 1f ? UiStyle.Rust : level > 0.7f ? UiStyle.Amber : new Color(ivory.r, ivory.g, ivory.b, 0.8f);
                meterLabel.text = !voice.Calibrated ? "МИКРОФОН · СЛУШАЮ ТИШИНУ…" : level >= 1f ? "СЛИШКОМ ГРОМКО" : "МИКРОФОН";
                meterLabel.color = level >= 1f ? UiStyle.Rust : UiStyle.Ash;
                return;
            }
            float strain = breath.Holding ? 1f - breath.Air : 0f;
            vignette.color = new Color(Mathf.Clamp01((0.35f - breath.Air) / 0.35f) * (breath.Holding ? 0.35f : 0f), 0f, 0f,
                Mathf.Clamp01(0.3f + 0.45f * strain));
            if (airFill == null) return;
            airFill.sizeDelta = new Vector2(180f * breath.Air, 3f);
            airBar.color = breath.Air < 0.25f ? UiStyle.Rust : UiStyle.Ivory;
        }
    }
}
