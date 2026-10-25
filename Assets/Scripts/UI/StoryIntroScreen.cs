using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Чёрный экран с предысторией перед стартом игры. Текст печатается по буквам
    // моноширинным «видеомагнитофонным» шрифтом, страница за страницей. Нажатие во
    // время печати допечатывает страницу, следующее — листает дальше.
    // Строит свой UI сам, как PauseMenuController; уничтожается после показа.
    public sealed class StoryIntroScreen : MonoBehaviour
    {
        private static readonly Color Ivory = new Color32(239, 234, 220, 255);
        private static readonly Color Dim = new Color32(150, 144, 130, 255);

        private CanvasGroup group;
        private Image backdrop;
        private TextMeshProUGUI body, prompt;
        private AudioSource ticks;
        private AudioClip tick;

        public CanvasGroup Backdrop => group;

        public static StoryIntroScreen Create(TMP_FontAsset font)
        {
            var root = new GameObject("Story intro screen", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 1f;
            var screen = root.AddComponent<StoryIntroScreen>();
            screen.Build(font != null ? font : PauseMenuController.FindFont());
            return screen;
        }

        private void Build(TMP_FontAsset font)
        {
            group = GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            backdrop = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            backdrop.transform.SetParent(transform, false);
            Stretch(backdrop.rectTransform);
            backdrop.color = Color.black;

            body = Text("Story", font, 30f, Ivory);
            body.alignment = TextAlignmentOptions.MidlineLeft;
            body.lineSpacing = 18f;
            body.rectTransform.anchorMin = new Vector2(.5f, .5f);
            body.rectTransform.anchorMax = new Vector2(.5f, .5f);
            body.rectTransform.sizeDelta = new Vector2(900f, 300f);

            prompt = Text("Prompt", font, 18f, Dim);
            prompt.alignment = TextAlignmentOptions.BottomRight;
            prompt.rectTransform.anchorMin = prompt.rectTransform.anchorMax = new Vector2(1f, 0f);
            prompt.rectTransform.pivot = new Vector2(1f, 0f);
            prompt.rectTransform.anchoredPosition = new Vector2(-56f, 44f);
            prompt.rectTransform.sizeDelta = new Vector2(600f, 40f);
            prompt.text = "ПРОБЕЛ / ЛКМ — ДАЛЕЕ";
            prompt.alpha = 0f;

            ticks = gameObject.AddComponent<AudioSource>();
            ticks.playOnAwake = false;
            ticks.spatialBlend = 0f;
            ticks.ignoreListenerPause = true;
            tick = CreateTickClip();
        }

        private TextMeshProUGUI Text(string name, TMP_FontAsset font, float size, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(transform, false);
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public IEnumerator Show(string[] pages)
        {
            yield return new WaitForSecondsRealtime(1f);
            foreach (string page in pages)
            {
                yield return Type(page);
                yield return WaitForContinue();
                yield return FadeText(1f, 0f, .45f);
                yield return new WaitForSecondsRealtime(.35f);
            }
            body.text = "";
            prompt.alpha = 0f;
        }

        private IEnumerator Type(string page)
        {
            body.text = page;
            body.alpha = 1f;
            body.maxVisibleCharacters = 0;
            body.ForceMeshUpdate();
            int total = body.textInfo.characterCount;
            float clock = 0f;
            int shown = 0;
            // Пропускаем клик, которым закрыли предыдущую страницу.
            yield return null;
            while (shown < total)
            {
                if (ContinuePressed()) { shown = total; break; }
                clock += Time.unscaledDeltaTime;
                float delay = CharacterDelay(page, shown);
                while (clock >= delay && shown < total)
                {
                    clock -= delay;
                    char c = body.textInfo.characterInfo[shown].character;
                    shown++;
                    if (!char.IsWhiteSpace(c)) ticks.PlayOneShot(tick, Random.Range(.05f, .08f));
                    delay = CharacterDelay(page, shown);
                }
                body.maxVisibleCharacters = shown;
                yield return null;
            }
            body.maxVisibleCharacters = total;
        }

        // Ритм чтения: буквы быстро, после запятой и точки — короткая пауза.
        private float CharacterDelay(string page, int index)
        {
            if (index <= 0 || index > body.textInfo.characterCount) return .03f;
            char previous = body.textInfo.characterInfo[index - 1].character;
            if (previous == '.' || previous == '?' || previous == '!' || previous == '…') return .38f;
            if (previous == ',' || previous == '—' || previous == ':') return .16f;
            return .034f;
        }

        private IEnumerator WaitForContinue()
        {
            yield return null;
            float time = 0f;
            while (!ContinuePressed())
            {
                time += Time.unscaledDeltaTime;
                // Подсказка проявляется не сразу и мерцает, как надпись на экране видеомагнитофона.
                prompt.alpha = Mathf.Clamp01((time - .6f) / .5f) * (.55f + .3f * Mathf.Sin(time * 3.2f));
                yield return null;
            }
            prompt.alpha = 0f;
        }

        private static bool ContinuePressed() =>
            !PauseMenuController.InputBlocked && Time.timeScale > 0f &&
            (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
             Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0));

        private IEnumerator FadeText(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                body.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            body.alpha = to;
        }

        // Убирает чёрный фон, открывая сцену под ним.
        public IEnumerator FadeOut(float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / duration);
                yield return null;
            }
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        public IEnumerator FadeIn(float duration)
        {
            group.blocksRaycasts = true;
            float from = group.alpha;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            group.alpha = 1f;
        }

        // Короткий глухой щелчок печатной головки: шум с быстрым затуханием и тоном 1.6 кГц.
        private static AudioClip CreateTickClip()
        {
            const int rate = 44100;
            var data = new float[(int)(rate * .028f)];
            var random = new System.Random(3);
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Exp(-t * 190f);
                float noise = (float)random.NextDouble() * 2f - 1f;
                data[i] = (noise * .55f + Mathf.Sin(2f * Mathf.PI * 1600f * t) * .45f) * envelope;
            }
            AudioClip clip = AudioClip.Create("Story tick", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
