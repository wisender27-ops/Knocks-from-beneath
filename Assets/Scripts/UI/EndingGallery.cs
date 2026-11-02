using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // B-06: галерея концовок в главном меню. Кассеты VHS: открытые подписаны названием и итогом,
    // закрытые — «▒▒▒▒» и намёк на условие.
    public static class EndingGallery
    {
        public const string LockedTitle = "▒▒▒▒";
        private const float CardWidth = 320f, CardHeight = 460f, CardSpacing = 24f;

        public static string CardTitle(EndingRegistry.EndingInfo info, bool seen) => seen ? info.Title.ToUpperInvariant() : LockedTitle;
        public static string CardBody(EndingRegistry.EndingInfo info, bool seen) => seen ? info.Summary : info.Hint;
        public static string Counter() => "ЗАПИСАНО: " + EndingRegistry.SeenCount + "/" + EndingRegistry.Count;

        public static GameObject Build(Transform parent, TMP_FontAsset font, Action onBack)
        {
            RectTransform root = UiStyle.Stretch("EndingGallery", parent);
            UiStyle.Fill(root, "Dim", UiStyle.Dim).raycastTarget = true;
            UiStyle.Scanlines(root);

            TextMeshProUGUI heading = UiStyle.Text("Heading", root, font, 64f, UiStyle.Amber, TextAlignmentOptions.TopLeft);
            Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(180f, -80f), new Vector2(1400f, 90f));
            heading.text = "▌▌ КОНЦОВКИ";
            TextMeshProUGUI counter = UiStyle.Text("Counter", root, font, 28f, UiStyle.Ash, TextAlignmentOptions.TopLeft);
            Place(counter.rectTransform, new Vector2(0f, 1f), new Vector2(184f, -166f), new Vector2(800f, 40f));
            counter.text = Counter();

            RectTransform row = UiStyle.Rect("Cassettes", root, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(0f, -20f), new Vector2(EndingRegistry.Count * (CardWidth + CardSpacing), CardHeight));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = CardSpacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            foreach (EndingRegistry.EndingInfo info in EndingRegistry.Endings) Card(row, font, info);

            UiMenuItem back = UiMenuItem.Create(root, "Назад", font, 40f, () => onBack?.Invoke());
            Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(158f, 90f), new Vector2(400f, 60f));
            UiMenuItem.Select(back);
            return root.gameObject;
        }

        private static void Card(Transform parent, TMP_FontAsset font, EndingRegistry.EndingInfo info)
        {
            bool seen = EndingRegistry.IsSeen(info.Id);
            RectTransform card = UiStyle.Rect("Cassette_" + info.Id, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(CardWidth, CardHeight));
            var element = card.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = CardWidth;
            element.preferredHeight = CardHeight;
            var body = card.gameObject.AddComponent<Image>();
            body.color = seen ? new Color(.11f, .105f, .095f, 1f) : new Color(.06f, .06f, .062f, 1f);
            body.raycastTarget = false;

            // Наклейка кассеты.
            RectTransform label = UiStyle.Rect("Label", card, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -24f), new Vector2(CardWidth - 40f, 150f));
            var paper = label.gameObject.AddComponent<Image>();
            paper.color = seen ? UiStyle.Paper : new Color(.2f, .2f, .19f, 1f);
            paper.raycastTarget = false;
            Color ink = seen ? UiStyle.PaperInk : UiStyle.Ash;
            TextMeshProUGUI number = UiStyle.Text("Number", label, font, 22f, ink, TextAlignmentOptions.TopLeft);
            Place(number.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(260f, 30f));
            number.text = "ЗАПИСЬ " + EndingRegistry.Number(info.Id) + "/" + EndingRegistry.Count;
            TextMeshProUGUI title = UiStyle.Text("Title", label, font, 34f, ink, TextAlignmentOptions.Center);
            title.textWrappingMode = TextWrappingModes.Normal;
            title.enableAutoSizing = true;
            title.fontSizeMin = 20f;
            title.fontSizeMax = 34f;
            Place(title.rectTransform, new Vector2(.5f, 0f), new Vector2(0f, 12f), new Vector2(CardWidth - 70f, 100f));
            title.rectTransform.pivot = new Vector2(.5f, 0f);
            title.text = CardTitle(info, seen);

            // Окошко ленты.
            RectTransform window = UiStyle.Rect("TapeWindow", card, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                new Vector2(0f, -190f), new Vector2(CardWidth - 90f, 34f));
            var glass = window.gameObject.AddComponent<Image>();
            glass.color = new Color(0f, 0f, 0f, .85f);
            glass.raycastTarget = false;

            TextMeshProUGUI text = UiStyle.Text("Body", card, font, 24f, seen ? UiStyle.Ivory : UiStyle.Ash, TextAlignmentOptions.TopLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.fontStyle = seen ? FontStyles.Normal : FontStyles.Italic;
            Place(text.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -250f), new Vector2(CardWidth - 44f, 190f));
            text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.text = CardBody(info, seen);
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
