using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Авторы игры в правом нижнем углу главного меню: подпись «DEVELOPED BY», под ней аватар,
    // ник и Discord каждого. Аватары — Resources/UI/Credits/<файл>; заменить картинку можно без кода.
    public static class MenuCredits
    {
        private readonly struct Author
        {
            public readonly string Name;
            public readonly string Discord;
            public readonly string Avatar;

            public Author(string name, string discord, string avatar)
            {
                Name = name;
                Discord = discord;
                Avatar = avatar;
            }
        }

        private static readonly Author[] Authors =
        {
            new Author("Ender", "enderwis","UI/Credits/ender"),
            new Author("deskif", "deskif", "UI/Credits/deskif"),
        };

        private const float Margin = 56f;
        private const float Pad = 22f;
        private const float Width = 380f;
        private const float AvatarSize = 76f;
        private const float RowGap = 18f;
        private const float HeaderHeight = 40f;
        private const float Border = 3f;

        public static RectTransform Build(Transform parent, TMP_FontAsset font)
        {
            float height = Pad * 2f + HeaderHeight + Authors.Length * AvatarSize + (Authors.Length - 1) * RowGap;
            RectTransform root = UiStyle.Rect("Credits", parent, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-Margin, Margin), new Vector2(Width, height));
            // Панель меню раскладывает детей столбиком — блок авторов в раскладку не входит.
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            Image plate = UiStyle.Fill(root, "Plate", UiStyle.InkSoft);
            plate.raycastTarget = false;
            UiStyle.Scanlines(root, .12f);

            TextMeshProUGUI header = UiStyle.Text("Header", root, font, 24f, UiStyle.Ash, TextAlignmentOptions.TopLeft);
            Place(header.rectTransform, Pad, -Pad, Width - Pad * 2f, HeaderHeight);
            header.characterSpacing = 6f;
            header.text = "DEVELOPED BY";

            float y = -Pad - HeaderHeight;
            foreach (Author author in Authors)
            {
                BuildRow(root, font, author, y);
                y -= AvatarSize + RowGap;
            }
            return root;
        }

        private static void BuildRow(Transform parent, TMP_FontAsset font, Author author, float y)
        {
            RectTransform row = UiStyle.Rect("Author_" + author.Name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(Pad, y), new Vector2(Width - Pad * 2f, AvatarSize));

            // Рамка — как наклейка на кассете: светлый кант вокруг тёмного кадра.
            RectTransform frame = UiStyle.Rect("Avatar", row, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                new Vector2(AvatarSize, AvatarSize));
            Image edge = frame.gameObject.AddComponent<Image>();
            edge.color = UiStyle.Ash;
            edge.raycastTarget = false;

            RectTransform inner = UiStyle.Stretch("Picture", frame);
            inner.offsetMin = new Vector2(Border, Border);
            inner.offsetMax = new Vector2(-Border, -Border);
            Texture2D avatar = Resources.Load<Texture2D>(author.Avatar);
            if (avatar != null)
            {
                RawImage picture = inner.gameObject.AddComponent<RawImage>();
                picture.texture = avatar;
                picture.raycastTarget = false;
            }
            else
            {
                // Без картинки — первая буква ника на чёрном, чтобы рамка не пустовала.
                Image back = inner.gameObject.AddComponent<Image>();
                back.color = UiStyle.Ink;
                back.raycastTarget = false;
                TextMeshProUGUI letter = UiStyle.Text("Initial", inner, font, 44f, UiStyle.Ivory, TextAlignmentOptions.Center);
                Place(letter.rectTransform, 0f, 0f, AvatarSize - Border * 2f, AvatarSize - Border * 2f);
                letter.text = author.Name.Substring(0, 1).ToUpperInvariant();
            }

            float textX = AvatarSize + 20f;
            float textWidth = Width - Pad * 2f - textX;
            bool hasDiscord = !string.IsNullOrEmpty(author.Discord);

            TextMeshProUGUI name = UiStyle.Text("Name", row, font, 34f, UiStyle.Ivory,
                hasDiscord ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.Left);
            Place(name.rectTransform, textX, 0f, textWidth, hasDiscord ? AvatarSize * .56f : AvatarSize);
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.text = author.Name;

            if (!hasDiscord) return;
            TextMeshProUGUI discord = UiStyle.Text("Discord", row, font, 20f, UiStyle.Ash, TextAlignmentOptions.TopLeft);
            Place(discord.rectTransform, textX, -AvatarSize * .6f, textWidth, AvatarSize * .4f);
            discord.textWrappingMode = TextWrappingModes.NoWrap;
            discord.text = "discord: " + author.Discord;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
