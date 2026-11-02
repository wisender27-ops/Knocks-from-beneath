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
        private const string PhotoResource = "UI/EndingPhoto";
        private const float CardWidth = 300f, CardHeight = 450f, CellHeight = 600f, CardSpacing = 24f;
        // Положение картинки и пустого поля внутри рамки (доли карточки), по исходному изображению.
        private const float PhotoLeft = .08f, PhotoRight = .9f, PhotoTop = .103f, PhotoBottom = .716f, MarginTop = 330f;

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
                new Vector2(0f, -30f), new Vector2(EndingRegistry.Count * (CardWidth + CardSpacing), CellHeight));
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
            RectTransform cell = UiStyle.Rect("Cassette_" + info.Id, parent, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                Vector2.zero, new Vector2(CardWidth, CellHeight));
            var element = cell.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = CardWidth;
            element.preferredHeight = CellHeight;

            // Фотография, приколотая к стене: рамка с пустым полем снизу под название.
            RectTransform photo = UiStyle.Rect("Photo", cell, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
                Vector2.zero, new Vector2(CardWidth, CardHeight));
            Texture2D art = Resources.Load<Texture2D>(PhotoResource);
            if (art != null)
            {
                var picture = photo.gameObject.AddComponent<RawImage>();
                picture.texture = art;
                picture.raycastTarget = false;
                picture.color = seen ? Color.white : new Color(.7f, .66f, .62f, 1f);
            }
            else
            {
                var paper = photo.gameObject.AddComponent<Image>();
                paper.color = seen ? UiStyle.Paper : new Color(.2f, .2f, .19f, 1f);
                paper.raycastTarget = false;
            }
            if (!seen) Cover(photo);

            Color ink = seen ? UiStyle.PaperInk : new Color(.1f, .085f, .07f, 1f);
            TextMeshProUGUI number = UiStyle.Text("Number", photo, font, 18f, ink, TextAlignmentOptions.Center);
            Place(number.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -MarginTop), new Vector2(CardWidth - 60f, 24f));
            number.rectTransform.pivot = new Vector2(.5f, 1f);
            number.text = "ЗАПИСЬ " + EndingRegistry.Number(info.Id) + "/" + EndingRegistry.Count;
            TextMeshProUGUI title = UiStyle.Text("Title", photo, font, 30f, ink, TextAlignmentOptions.Center);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.enableAutoSizing = true;
            title.fontSizeMin = 16f;
            title.fontSizeMax = 30f;
            Place(title.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -MarginTop - 22f), new Vector2(CardWidth - 60f, 44f));
            title.rectTransform.pivot = new Vector2(.5f, 1f);
            title.text = CardTitle(info, seen);

            // Подпись под фотографией: итог у открытой, намёк у закрытой.
            TextMeshProUGUI text = UiStyle.Text("Body", cell, font, 24f, seen ? UiStyle.Ivory : UiStyle.Ash, TextAlignmentOptions.Top);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.fontStyle = seen ? FontStyles.Normal : FontStyles.Italic;
            Place(text.rectTransform, new Vector2(.5f, 1f), new Vector2(0f, -CardHeight - 16f), new Vector2(CardWidth - 20f, CellHeight - CardHeight - 16f));
            text.rectTransform.pivot = new Vector2(.5f, 1f);
            text.text = CardBody(info, seen);
        }

        // Закрытая фотография: сама картинка скрыта тёмным пятном, рамка остаётся.
        private static void Cover(Transform photo)
        {
            RectTransform smear = UiStyle.Rect("Smear", photo, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(CardWidth * PhotoLeft, -CardHeight * PhotoTop),
                new Vector2(CardWidth * (PhotoRight - PhotoLeft), CardHeight * (PhotoBottom - PhotoTop)));
            var image = smear.gameObject.AddComponent<Image>();
            image.color = new Color(.025f, .022f, .02f, .88f);
            image.raycastTarget = false;
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
