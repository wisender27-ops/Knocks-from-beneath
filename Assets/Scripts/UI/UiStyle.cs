using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Общий визуальный язык интерфейса: экранное меню видеомагнитофона и субтитры с кассеты.
    // Текст лежит прямо на картинке на плотной чёрной подложке, без карточек и рамок.
    public static class UiStyle
    {
        public static readonly Color Ink = new Color(0f, 0f, 0f, .84f);
        public static readonly Color InkSoft = new Color(0f, 0f, 0f, .55f);
        public static readonly Color Dim = new Color(.012f, .012f, .014f, .9f);
        public static readonly Color Ivory = new Color32(230, 225, 214, 255);
        public static readonly Color Thought = new Color32(196, 190, 176, 255);
        public static readonly Color Amber = new Color32(217, 176, 106, 255);
        public static readonly Color Ash = new Color32(157, 157, 152, 255);
        public static readonly Color Rust = new Color32(205, 150, 126, 255);
        public static readonly Color KeyInk = new Color32(35, 31, 26, 255);
        public static readonly Color Paper = new Color32(214, 206, 186, 255);
        public static readonly Color PaperInk = new Color32(44, 39, 32, 255);

        public const string FontName = "VCR OSD Mono Cyr SDF";
        private static TMP_FontAsset cachedFont;
        private static Texture2D scanlines;

        public static TMP_FontAsset Font(TMP_FontAsset preferred = null)
        {
            if (preferred != null) return preferred;
            if (cachedFont != null) return cachedFont;
            foreach (TMP_FontAsset asset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
                if (asset != null && asset.name == FontName) return cachedFont = asset;
            foreach (TextMeshProUGUI text in Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.font != null && text.font.name.Contains("Cyr")) return cachedFont = text.font;
            return TMP_Settings.defaultFontAsset;
        }

        public static string Hex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(parent, false);
            text.font = Font(font);
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.richText = false;
            text.text = string.Empty;
            return text;
        }

        public static Image Fill(Transform parent, string name, Color color)
        {
            var image = Stretch(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // Тонкие строки развёртки поверх затемнения полноэкранных окон.
        public static RawImage Scanlines(Transform parent, float opacity = .16f)
        {
            if (scanlines == null)
            {
                scanlines = new Texture2D(1, 4, TextureFormat.RGBA32, false)
                {
                    name = "UiScanlines", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat,
                    hideFlags = HideFlags.DontSave
                };
                scanlines.SetPixels32(new[] { new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 120) });
                scanlines.Apply(false, true);
            }
            var image = Stretch("Scanlines", parent).gameObject.AddComponent<RawImage>();
            image.texture = scanlines;
            image.color = new Color(1f, 1f, 1f, opacity);
            image.uvRect = new Rect(0f, 0f, 1f, 180f);
            image.raycastTarget = false;
            return image;
        }

        public static Canvas OverlayCanvas(Transform parent, string name, int order, Vector2 reference, bool raycasts)
        {
            var root = raycasts
                ? new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
                : new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = reference;
            scaler.matchWidthOrHeight = .5f;
            return canvas;
        }
    }
}
