using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // C-02: просмотр ночной записи во весь экран — как кассета в видеомагнитофоне: щелчок PLAY, экранное меню
    // «PLAY» и счётчик ленты, картинка через VHS-шейдер, в конце «снег», щелчок STOP. Esc — остановить.
    // Мини-игра «Плёнка» (C-03) переиспользует NightRecording и материал VhsTape на телевизоре.
    public sealed class NightTapeViewer : MinigameWindow
    {
        private const float TailSeconds = 0.9f, OsdSeconds = 3.5f;
        private static readonly int StaticId = Shader.PropertyToID("_Static");
        private static readonly int TrackingId = Shader.PropertyToID("_Tracking");

        private NightRecording recording;
        private RawImage screen;
        private Material material;
        private TextMeshProUGUI playLabel, counter;
        private AudioSource motor;
        private AudioClip playClip, stopClip;
        private int counterSeconds = -1;
        private float tail, shown;
        private Action closed;

        protected override string WindowName => "Night Tape (C-02)";

        public bool Open(Transform view, TapeDoor[] doors, Action onClosed)
        {
            if (view == null || !TryOpen()) return false;
            closed = onClosed;
            recording = NightRecording.Create(view, doors);
            screen.texture = recording.Texture;
            recording.Playing = true;
            tail = 0f;
            shown = 0f;
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
            playLabel.text = "PLAY";
            counter = Osd("Counter", picture, new Vector2(1f, 1f), new Vector2(-48f, -36f), TextAlignmentOptions.TopRight, font);
            var hint = Osd("Hint", root, new Vector2(0.5f, 0f), new Vector2(0f, 18f), TextAlignmentOptions.Bottom, UiStyle.Font());
            hint.fontSize = 20f;
            hint.color = UiStyle.Ash;
            hint.text = "Esc — остановить";

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
            shown += Time.deltaTime;
            if (recording == null) return;
            if (recording.Finished)
            {
                tail += Time.deltaTime;
                if (tail >= TailSeconds) { Cancel(); return; }
            }
            Refresh();
        }

        private void Refresh()
        {
            if (recording == null) return;
            if (material != null)
            {
                material.SetFloat(StaticId, recording.Finished ? 1f : recording.SignalLoss);
                material.SetFloat(TrackingId, recording.Tracking);
            }
            if (playLabel.gameObject.activeSelf && shown > OsdSeconds) playLabel.gameObject.SetActive(false);
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
