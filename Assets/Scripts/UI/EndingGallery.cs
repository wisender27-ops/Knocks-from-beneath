using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // B-06: галерея концовок в главном меню — полка с кассетами и телевизор. Выбранная кассета выдвигается,
    // телевизор показывает её запись; закрытая кассета без подписи, на экране помехи и намёк на условие.
    // Комната — один арт 1920×1080 с прозрачным окном экрана: кадр лежит под ним, за настоящей рамкой.
    public sealed class EndingGallery : MonoBehaviour
    {
        public const string LockedTitle = "▒▒▒▒";
        private const string ArtFolder = "UI/EndingGallery/";
        private const float StageWidth = 1920f, StageHeight = 1080f;
        private const float ScreenLeft = 924f, ScreenTop = 302f, ScreenRight = 1392f, ScreenBottom = 657f;
        private const float StackLeft = 212f, StackBottom = 868f, TapeWidth = 296f, TapeThickness = 1.6f, TapeGap = 2f;
        private const float PullDistance = 46f, PullSpeed = 14f, SwitchNoise = .22f, NoiseStep = .05f;
        private static readonly Color Glow = new Color(UiStyle.Amber.r, UiStyle.Amber.g, UiStyle.Amber.b, .07f);
        private static readonly Color GlowNoSignal = new Color(UiStyle.Ash.r, UiStyle.Ash.g, UiStyle.Ash.b, .04f);

        // Три рисунка кассеты на пять мест: два повторяются зеркально, чтобы стопка не выглядела копиями.
        private sealed class TapeArt
        {
            public readonly int Art;
            public readonly float Aspect;
            public readonly Rect Label; // доли картинки: x, y от верхнего края, ширина, высота
            public TapeArt(int art, float aspect, Rect label) { Art = art; Aspect = aspect; Label = label; }
        }

        private static readonly TapeArt[] Tapes =
        {
            new TapeArt(1, 6.79f, new Rect(.088f, .306f, .848f, .608f)),
            new TapeArt(2, 7.61f, new Rect(.16f, .151f, .761f, .675f)),
            new TapeArt(3, 7.39f, new Rect(.141f, .15f, .782f, .696f)),
        };
        private static readonly int[] StackArt = { 1, 2, 0, 1, 2 };
        private static readonly bool[] StackFlip = { false, false, false, true, true };

        public static string CardTitle(EndingRegistry.EndingInfo info, bool seen) => seen ? Loc.Upper(info.Title) : LockedTitle;
        public static string CardBody(EndingRegistry.EndingInfo info, bool seen) => seen ? info.Summary : info.Hint;
        public static string ScreenCaption(bool seen) => seen ? "► PLAY" : Loc.T("НЕТ СИГНАЛА");
        public static string Counter() => Loc.F("ЗАПИСАНО: {0}/{1}", EndingRegistry.SeenCount, EndingRegistry.Count);

        private EndingTapeSlot[] slots;
        private Texture2D[] frames;
        private Texture2D noise;
        private RawImage frame, snow, glow;
        private Image captionBar;
        private TextMeshProUGUI caption, title, body;
        private int selected = -1;
        private float switchNoiseLeft, nextNoise;

        public static GameObject Build(Transform parent, TMP_FontAsset font, Action onBack)
        {
            RectTransform root = UiStyle.Stretch("EndingGallery", parent);
            UiStyle.Fill(root, "Backdrop", Color.black).raycastTarget = true;
            EndingGallery gallery = root.gameObject.AddComponent<EndingGallery>();
            gallery.Compose(root, font, onBack);
            return root.gameObject;
        }

        private void Compose(RectTransform root, TMP_FontAsset font, Action onBack)
        {
            // Сцена держит пропорции 16:9 и вписана в экран целиком: заголовок, кассеты и «Назад» лежат
            // у краёв арта и не должны обрезаться, поля закрывает чёрный фон. Координаты ниже — в пикселях арта.
            RectTransform stage = UiStyle.Stretch("Stage", root);
            var fitter = stage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = StageWidth / StageHeight;

            RectTransform screen = Area("Screen", stage, ScreenLeft, ScreenTop, ScreenRight, ScreenBottom);
            frame = Picture("Frame", screen, null);
            noise = CreateNoise();
            snow = Picture("Snow", screen, noise);
            UiStyle.Scanlines(screen, .22f);

            frames = new Texture2D[EndingRegistry.Count];
            for (int i = 0; i < frames.Length; i++) frames[i] = Resources.Load<Texture2D>(ArtFolder + "end" + (i + 1));
            Picture("Room", stage, Resources.Load<Texture2D>(ArtFolder + "room"));

            glow = Picture("ScreenGlow", Area("GlowArea", stage, ScreenLeft - 330f, ScreenTop - 300f, ScreenRight + 330f, ScreenBottom + 300f),
                Resources.Load<Texture2D>(ArtFolder + "glow"));

            RectTransform captionArea = Area("CaptionArea", stage, ScreenLeft + 62f, ScreenTop + 44f, ScreenRight - 62f, ScreenTop + 92f);
            captionBar = UiStyle.Fill(captionArea, "CaptionBar", UiStyle.Ink);
            caption = Label("Caption", captionArea, font, 30f, UiStyle.Ivory, TextAlignmentOptions.MidlineLeft);

            Picture("Shade", Area("ShadeArea", stage, 1400f, 180f, 2020f, 800f), Resources.Load<Texture2D>(ArtFolder + "glow")).color = new Color(0f, 0f, 0f, .75f);
            title = Label("Title", Area("TitleArea", stage, 1530f, 322f, 1870f, 450f), font, 46f, UiStyle.Amber, TextAlignmentOptions.BottomLeft);
            body = Label("Body", Area("BodyArea", stage, 1530f, 468f, 1870f, 720f), font, 28f, UiStyle.Ivory, TextAlignmentOptions.TopLeft);

            Label("Heading", Area("HeadingArea", stage, 180f, 52f, 1100f, 142f), font, 64f, UiStyle.Amber, TextAlignmentOptions.TopLeft).text = Loc.T("► КОНЦОВКИ");
            Label("Counter", Area("CounterArea", stage, 184f, 140f, 900f, 186f), font, 28f, UiStyle.Ash, TextAlignmentOptions.TopLeft).text = Counter();

            UiMenuItem back = UiMenuItem.Create(stage, Loc.T("Назад"), font, 40f, () => onBack?.Invoke());
            var backRect = (RectTransform)back.transform;
            backRect.pivot = Vector2.zero;
            backRect.anchorMin = backRect.anchorMax = new Vector2(158f / StageWidth, 24f / StageHeight);
            backRect.anchoredPosition = Vector2.zero;

            BuildStack(stage, font);
            Link(back.Button);
            EndingTapeSlot.Pick(slots[0]);
            Show(0);
        }

        private void BuildStack(RectTransform stage, TMP_FontAsset font)
        {
            int count = EndingRegistry.Count;
            slots = new EndingTapeSlot[count];
            float bottom = StackBottom;
            // Снизу вверх, а первая концовка лежит сверху стопки.
            for (int i = count - 1; i >= 0; i--)
            {
                TapeArt art = Tapes[StackArt[i % StackArt.Length]];
                bool flip = StackFlip[i % StackFlip.Length];
                float height = TapeWidth / art.Aspect * TapeThickness;
                EndingRegistry.EndingInfo info = EndingRegistry.Endings[i];
                bool seen = EndingRegistry.IsSeen(info.Id);

                RectTransform tape = Area("Tape_" + info.Id, stage, StackLeft, bottom - height, StackLeft + TapeWidth, bottom);
                RawImage image = Picture("Art", tape, Resources.Load<Texture2D>(ArtFolder + "tape" + art.Art));
                image.raycastTarget = true;
                if (flip) image.uvRect = new Rect(1f, 0f, -1f, 1f);

                float left = flip ? 1f - art.Label.xMax : art.Label.xMin;
                float right = left + art.Label.width;
                TextMeshProUGUI text = UiStyle.Text("Label", tape, font, 24f, seen ? UiStyle.PaperInk : UiStyle.Ash, TextAlignmentOptions.Center);
                text.rectTransform.anchorMin = new Vector2(left, 1f - art.Label.yMax);
                text.rectTransform.anchorMax = new Vector2(right, 1f - art.Label.yMin);
                text.rectTransform.offsetMin = new Vector2(8f, 2f);
                text.rectTransform.offsetMax = new Vector2(-8f, -2f);
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.enableAutoSizing = true;
                text.fontSizeMin = 13f;
                text.fontSizeMax = 24f;
                text.text = CardTitle(info, seen);

                var button = tape.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.targetGraphic = image;
                slots[i] = tape.gameObject.AddComponent<EndingTapeSlot>();
                slots[i].Configure(this, i, image, button, seen);
                bottom -= height + TapeGap;
            }
        }

        // Стрелки: по стопке и на «Назад», по кругу.
        private void Link(Button back)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnUp = i > 0 ? slots[i - 1].Button : back;
                navigation.selectOnDown = i < slots.Length - 1 ? slots[i + 1].Button : back;
                slots[i].Button.navigation = navigation;
            }
            var backNavigation = new Navigation { mode = Navigation.Mode.Explicit };
            backNavigation.selectOnUp = slots[slots.Length - 1].Button;
            backNavigation.selectOnDown = slots[0].Button;
            back.navigation = backNavigation;
        }

        public void Show(int index)
        {
            if (index == selected) return;
            selected = index;
            EndingRegistry.EndingInfo info = EndingRegistry.Endings[index];
            bool seen = EndingRegistry.IsSeen(info.Id);
            frame.texture = seen ? frames[index] : null;
            switchNoiseLeft = SwitchNoise;
            caption.text = ScreenCaption(seen);
            caption.color = seen ? UiStyle.Ivory : UiStyle.Amber;
            caption.alignment = seen ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            captionBar.enabled = !seen;
            title.text = CardTitle(info, seen);
            body.text = CardBody(info, seen);
            body.color = seen ? UiStyle.Ivory : UiStyle.Ash;
            body.fontStyle = seen ? FontStyles.Normal : FontStyles.Italic;
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            switchNoiseLeft -= delta;
            bool hasPicture = frame.texture != null;
            bool showNoise = !hasPicture || switchNoiseLeft > 0f;
            frame.enabled = hasPicture;
            snow.enabled = showNoise;
            if (showNoise && Time.unscaledTime >= nextNoise)
            {
                nextNoise = Time.unscaledTime + NoiseStep;
                snow.uvRect = new Rect(UnityEngine.Random.value, UnityEngine.Random.value, 1f, 1f);
            }
            Color light = hasPicture ? Glow : GlowNoSignal;
            light.a *= .92f + .08f * Mathf.Sin(Time.unscaledTime * 31f);
            glow.color = light;
            foreach (EndingTapeSlot slot in slots) slot.Animate(selected, PullDistance, PullSpeed * delta);
        }

        private void OnDestroy()
        {
            if (noise != null) Destroy(noise);
        }

        private static Texture2D CreateNoise()
        {
            const int width = 160, height = 120;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte value = (byte)UnityEngine.Random.Range(8, 96);
                pixels[i] = new Color32(value, value, value, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // Прямоугольник в координатах арта; якоря в долях, поэтому он едет вместе со сценой.
        private static RectTransform Area(string name, Transform stage, float left, float top, float right, float bottom)
        {
            RectTransform rect = UiStyle.Stretch(name, stage);
            rect.anchorMin = new Vector2(left / StageWidth, 1f - bottom / StageHeight);
            rect.anchorMax = new Vector2(right / StageWidth, 1f - top / StageHeight);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RawImage Picture(string name, Transform parent, Texture texture)
        {
            var image = UiStyle.Stretch(name, parent).gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI Label(string name, RectTransform area, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = UiStyle.Text(name, area, font, size, color, alignment);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }
    }

    // Кассета на полке: выбор наведением, кликом и стрелками; выбранная выдвигается и светлеет.
    public sealed class EndingTapeSlot : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        private static readonly Color SeenIdle = new Color(.78f, .76f, .73f, 1f);
        private static readonly Color LockedIdle = new Color(.3f, .29f, .28f, 1f);
        private static readonly Color LockedPicked = new Color(.5f, .48f, .46f, 1f);

        private EndingGallery gallery;
        private int index;
        private RawImage image;
        private RectTransform rect;
        private bool seen;
        private float pull;

        public Button Button { get; private set; }

        public void Configure(EndingGallery owner, int slot, RawImage art, Button button, bool opened)
        {
            gallery = owner;
            index = slot;
            image = art;
            Button = button;
            seen = opened;
            rect = (RectTransform)transform;
            image.color = seen ? SeenIdle : LockedIdle;
        }

        public static void Pick(EndingTapeSlot slot)
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(slot.gameObject);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData eventData) => gallery.Show(index);

        public void Animate(int selected, float distance, float step)
        {
            bool picked = selected == index;
            pull = Mathf.Lerp(pull, picked ? 1f : 0f, Mathf.Clamp01(step));
            rect.anchoredPosition = new Vector2(pull * distance, 0f);
            Color idle = seen ? SeenIdle : LockedIdle;
            Color active = seen ? Color.white : LockedPicked;
            image.color = Color.Lerp(idle, active, pull);
        }
    }
}
