using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    public sealed class NightTapeViewer : MinigameWindow
    {
        private static readonly int StaticId = Shader.PropertyToID("_Static");
        private static readonly int TrackingId = Shader.PropertyToID("_Tracking");

        private NightRecording recording;
        private RawImage screen;
        private Material material;
        private TextMeshProUGUI playLabel, counter, feedback;
        private RectTransform feedbackBox, trackFill, trackHead, trackMark;
        private UiKeyHints keys;
        private float savedFrameTime;
        private AudioSource motor;
        private AudioClip playClip, stopClip;
        private int counterSeconds = -1;
        private float feedbackTime;
        private Action closed;
        private TapeTransport transport;
        private bool suppressShuttle;
        public bool EvidenceCaptured { get; private set; }

        protected override string WindowName => "Night Tape (M2)";

        public bool Open(Transform view, TapeDoor[] doors, Action onClosed)
        {
            if (view == null || !TryOpen()) return false;
            closed = onClosed;
            transport = new TapeTransport();
            suppressShuttle = false;
            EvidenceCaptured = false;
            recording = NightRecording.Create(view, doors);
            screen.texture = recording.Texture;
            recording.Playing = true;
            trackMark.gameObject.SetActive(false);
            SetFeedback("Кто был в доме ночью? Остановите плёнку на нужном кадре и сохраните его.", 10f);
            counterSeconds = -1;
            playLabel.gameObject.SetActive(true);
            GameAudio.PlayUi(playClip, 0.9f);
            if (motor != null && motor.clip != null) motor.Play();
            Refresh();
            return true;
        }

        protected override void Build(Transform root)
        {
            var black = Rect("Black", root, Vector2.zero, Vector2.zero);
            black.anchorMin = Vector2.zero;
            black.anchorMax = Vector2.one;
            black.gameObject.AddComponent<Image>().color = Color.black;

            var picture = Rect("Tape Picture", root, Vector2.zero, Vector2.zero);
            picture.anchorMin = Vector2.zero;
            picture.anchorMax = Vector2.one;
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)NightRecording.Width / NightRecording.Height;
            screen = picture.gameObject.AddComponent<RawImage>();
            screen.raycastTarget = false;
            Shader shader = Resources.Load<Shader>("Shaders/VhsTape");
            if (shader != null) screen.material = material = new Material(shader);

            TMP_FontAsset font = TapeFont.Get();
            playLabel = Osd("PLAY", picture, new Vector2(0f, 1f), new Vector2(48f, -36f), TextAlignmentOptions.TopLeft, font);
            playLabel.text = "PLAY >";
            counter = Osd("Counter", picture, new Vector2(1f, 1f), new Vector2(-48f, -36f), TextAlignmentOptions.TopRight, font);

            // Нижняя панель пульта: затемнение, сообщение, шкала плёнки и клавиши в стиле остальных мини-игр.
            var bar = UiStyle.Rect("Control Bar", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 140f));
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = new Vector2(1f, 0f);
            bar.sizeDelta = new Vector2(0f, 140f);
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = UiStyle.Ink;
            barImage.raycastTarget = false;

            feedbackBox = UiStyle.Rect("Evidence", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 98f), new Vector2(880f, 38f));
            var feedbackImage = feedbackBox.gameObject.AddComponent<Image>();
            feedbackImage.color = UiStyle.Dim;
            feedbackImage.raycastTarget = false;
            feedback = UiStyle.Text("Text", feedbackBox, UiStyle.Font(), 21f, UiStyle.Amber, TextAlignmentOptions.Center);
            feedback.rectTransform.anchorMin = Vector2.zero;
            feedback.rectTransform.anchorMax = Vector2.one;
            feedback.rectTransform.offsetMin = new Vector2(12f, 0f);
            feedback.rectTransform.offsetMax = new Vector2(-12f, 0f);
            feedback.textWrappingMode = TextWrappingModes.NoWrap;

            var track = UiStyle.Rect("Track", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 76f), new Vector2(760f, 4f));
            var trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(UiStyle.Ash.r, UiStyle.Ash.g, UiStyle.Ash.b, .35f);
            trackImage.raycastTarget = false;
            trackFill = UiStyle.Fill(track, "Fill", UiStyle.Ash).rectTransform;
            trackHead = UiStyle.Rect("Head", track, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 18f));
            var headImage = trackHead.gameObject.AddComponent<Image>();
            headImage.color = UiStyle.Ivory;
            headImage.raycastTarget = false;
            trackMark = UiStyle.Rect("Saved Frame", track, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 14f));
            var markImage = trackMark.gameObject.AddComponent<Image>();
            markImage.color = UiStyle.Rust;
            markImage.raycastTarget = false;
            trackMark.gameObject.SetActive(false);

            keys = KeyHints(UiStyle.Font(), -322f);

            playClip = Resources.Load<AudioClip>("Audio/Recording/VcrPlay");
            stopClip = Resources.Load<AudioClip>("Audio/Recording/VcrStop");
            motor = AudioRouting.Create(root, "VCR Motor", AudioBus.World, 0f);
            motor.clip = Resources.Load<AudioClip>("Audio/Recording/VcrMotor");
            motor.loop = true;
            motor.playOnAwake = false;
            motor.volume = 0.35f;
        }

        private static TextMeshProUGUI Osd(string name, Transform parent, Vector2 anchor, Vector2 position,
            TextAlignmentOptions alignment, TMP_FontAsset font)
        {
            var rect = Rect(name, parent, position, new Vector2(420f, 60f));
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 46f;
            text.alignment = alignment;
            text.color = new Color(0.92f, 0.95f, 1f);
            text.raycastTarget = false;
            return text;
        }

        protected override void Tick()
        {
            if (CancelPressed) { Cancel(); return; }
            if (recording == null) return;
            transport.Synchronize(recording.Time);
            if (Input.GetKeyDown(KeyCode.Space))
            {
                transport.TogglePlay();
                suppressShuttle = true;
                recording.Seek(transport.Time);
                GameAudio.PlayUi(transport.Playing ? playClip : stopClip, 0.65f);
            }
            int shuttle = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            if (shuttle == 0) suppressShuttle = false;
            if (suppressShuttle) shuttle = 0;
            bool wasPlaying = transport.Playing;
            if (shuttle != 0 || !transport.Playing || recording.Finished)
            {
                transport.Advance(Time.deltaTime, shuttle);
                recording.Seek(transport.Time);
            }
            recording.Playing = transport.Playing;
            if (wasPlaying && !transport.Playing) GameAudio.PlayUi(stopClip, 0.5f);
            if (Input.GetKeyDown(KeyCode.E)) CaptureFrame();
            if (feedbackTime > 0f)
            {
                feedbackTime -= Time.deltaTime;
                if (feedbackTime <= 0f) SetFeedback(EvidenceCaptured ? "Кадр сохранён. Esc — вернуться в комнату." : "", 0f);
            }
            if (motor != null)
            {
                bool moving = transport.Playing || !transport.IsStopped;
                motor.pitch = Mathf.Lerp(0.8f, 1.6f, Mathf.Abs(transport.Speed) / 6f);
                motor.volume = moving ? 0.35f : 0f;
            }
            Refresh();
        }

        private void CaptureFrame()
        {
            if (transport.CaptureEvidence())
            {
                EvidenceCaptured = true;
                savedFrameTime = transport.Time;
                SetFeedback("Кадр сохранён — " + NightTape.Stamp(transport.Time), 4f);
                GameAudio.PlayUi(stopClip, 0.7f);
            }
            else SetFeedback(transport.IsStopped ? "На этом кадре ничего необычного." : "Сначала остановите плёнку.", 4f);
        }

        private void SetFeedback(string message, float seconds)
        {
            feedback.text = message;
            feedbackBox.gameObject.SetActive(!string.IsNullOrEmpty(message));
            feedbackTime = seconds;
        }

        private static void PlaceOnTrack(RectTransform marker, float time)
        {
            float k = Mathf.Clamp01(time / NightTape.Duration);
            marker.anchorMin = marker.anchorMax = new Vector2(k, 0.5f);
            marker.anchoredPosition = Vector2.zero;
        }

        private void Refresh()
        {
            if (recording == null) return;
            if (material != null)
            {
                material.SetFloat(StaticId, recording.Finished ? 1f : recording.SignalLoss);
                material.SetFloat(TrackingId, Mathf.Max(recording.Tracking, Mathf.Abs(transport.Speed) * 0.13f));
            }
            string state = transport.Playing ? "PLAY >" : transport.Speed > 0.001f ? "FF >>" : transport.Speed < -0.001f ? "<< REW" : "|| PAUSE";
            if (playLabel.text != state) playLabel.text = state;
            trackFill.anchorMax = new Vector2(Mathf.Clamp01(recording.Time / NightTape.Duration), 1f);
            PlaceOnTrack(trackHead, recording.Time);
            if (EvidenceCaptured)
            {
                trackMark.gameObject.SetActive(true);
                PlaceOnTrack(trackMark, savedFrameTime);
            }
            if (transport.IsStopped)
                keys.Set("ПРОБЕЛ", "ПУСК", "A D", "ПЕРЕМОТКА", "E", "СОХРАНИТЬ КАДР", "ESC", "ВЫЙТИ");
            else
                keys.Set("ПРОБЕЛ", "СТОП", "A D", "ПЕРЕМОТКА", "ESC", "ВЫЙТИ");
            int seconds = Mathf.FloorToInt(recording.Time);
            if (seconds == counterSeconds) return;
            counterSeconds = seconds;
            counter.text = string.Format("0:{0:00}:{1:00}", seconds / 60, seconds % 60);
        }

        protected override void Cancel()
        {
            if (!CloseWindow()) return;
            GameAudio.PlayUi(stopClip, 0.9f);
            if (motor != null) motor.Stop();
            if (recording != null) Destroy(recording.gameObject);
            recording = null;
            if (screen != null) screen.texture = null;
            Action callback = closed;
            closed = null;
            callback?.Invoke();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (material != null) Destroy(material);
        }
    }
}
