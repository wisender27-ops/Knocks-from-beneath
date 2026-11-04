using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Выбор языка в правом верхнем углу главного меню: флаг и название текущего языка,
    // по нажатию — список флагов, каждый язык подписан на самом себе. Флаги — Resources/UI/Flags/<код>.
    public sealed class MenuLanguagePicker : MonoBehaviour
    {
        private const float Margin = 56f;
        private const float FlagWidth = 48f, FlagHeight = 32f, Border = 2f;
        private const float ToggleWidth = 380f, ToggleHeight = 52f;
        private const float RowHeight = 46f, ListPad = 10f;
        private const float ToggleFont = 28f, RowFont = 26f;

        private GameObject list, blocker;
        private TextMeshProUGUI arrow;
        private Action<string> onSelect;

        public bool IsOpen => list != null && list.activeSelf;

        public static MenuLanguagePicker Build(Transform parent, TMP_FontAsset font, Action<string> onSelect)
        {
            // Невидимая подложка на весь экран: клик мимо списка закрывает его.
            RectTransform shade = UiStyle.Stretch("LanguageBlocker", parent);
            shade.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Image shadeImage = shade.gameObject.AddComponent<Image>();
            shadeImage.color = Color.clear;
            Button shadeButton = shade.gameObject.AddComponent<Button>();
            shadeButton.transition = Selectable.Transition.None;

            RectTransform root = UiStyle.Rect("LanguagePicker", parent, Vector2.one, Vector2.one,
                new Vector2(-Margin, -Margin), new Vector2(ToggleWidth, ToggleHeight));
            root.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            MenuLanguagePicker picker = root.gameObject.AddComponent<MenuLanguagePicker>();
            picker.onSelect = onSelect;
            picker.blocker = shade.gameObject;
            shadeButton.onClick.AddListener(picker.Close);

            Loc.Language current = Array.Find(Loc.Languages, language => language.Code == Loc.Current);
            if (current.Code == null) current = Loc.Languages[0];
            picker.BuildToggle(root, font, current);
            picker.BuildList(parent, font);
            GameAudio.HookSelectables(root);
            GameAudio.HookSelectables(picker.list.transform);
            picker.Close();
            return picker;
        }

        public void Close()
        {
            if (list != null) list.SetActive(false);
            if (blocker != null) blocker.SetActive(false);
            if (arrow != null) arrow.text = "▼";
        }

        private void Toggle()
        {
            bool open = !IsOpen;
            list.SetActive(open);
            blocker.SetActive(open);
            arrow.text = open ? "▲" : "▼";
        }

        private void BuildToggle(RectTransform root, TMP_FontAsset font, Loc.Language current)
        {
            Image plate = UiStyle.Fill(root, "Plate", UiStyle.InkSoft);
            plate.raycastTarget = true;
            Button button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = plate;
            button.onClick.AddListener(Toggle);

            // Флаг и название прижаты к правому краю: длина названия у языков разная.
            arrow = UiStyle.Text("Arrow", root, font, 18f, UiStyle.Ash, TextAlignmentOptions.Center);
            Place(arrow.rectTransform, new Vector2(1f, .5f), new Vector2(-14f, 0f), new Vector2(22f, ToggleHeight));
            TextMeshProUGUI name = UiStyle.Text("Name", root, font, ToggleFont, UiStyle.Ivory, TextAlignmentOptions.MidlineRight);
            Place(name.rectTransform, new Vector2(1f, .5f), new Vector2(-46f, 0f), new Vector2(ToggleWidth - FlagWidth - 80f, ToggleHeight));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.text = current.NativeName;
            name.ForceMeshUpdate();
            float flagX = -46f - Mathf.Min(name.preferredWidth, ToggleWidth - FlagWidth - 80f) - 14f;
            Flag(root, current.Code, new Vector2(1f, .5f), new Vector2(flagX, 0f));
            root.gameObject.AddComponent<HoverTint>().Init(name, UiStyle.Ivory, UiStyle.Amber);
        }

        // Список — сосед кнопки, а не её ребёнок: иначе наведение на строки подсвечивало бы и кнопку.
        private void BuildList(Transform parent, TMP_FontAsset font)
        {
            float height = ListPad * 2f + Loc.Languages.Length * RowHeight;
            RectTransform panel = UiStyle.Rect("Languages", parent, Vector2.one, Vector2.one,
                new Vector2(-Margin, -Margin - ToggleHeight - 8f), new Vector2(ToggleWidth, height));
            panel.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            list = panel.gameObject;
            UiStyle.Fill(panel, "Plate", UiStyle.Ink).raycastTarget = true;
            UiStyle.Scanlines(panel, .12f);

            float y = -ListPad;
            foreach (Loc.Language language in Loc.Languages)
            {
                BuildRow(panel, font, language, y);
                y -= RowHeight;
            }
        }

        private void BuildRow(RectTransform panel, TMP_FontAsset font, Loc.Language language, float y)
        {
            RectTransform row = UiStyle.Rect("Lang_" + language.Code, panel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, y), new Vector2(ToggleWidth, RowHeight));
            Image back = row.gameObject.AddComponent<Image>();
            back.color = Color.clear;
            Button button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = back;
            string code = language.Code;
            button.onClick.AddListener(() => Choose(code));

            Flag(row, code, new Vector2(0f, .5f), new Vector2(22f + FlagWidth, 0f));
            bool selected = code == Loc.Current;
            Color normal = selected ? UiStyle.Amber : UiStyle.Ivory;
            TextMeshProUGUI name = UiStyle.Text("Name", row, font, RowFont, normal, TextAlignmentOptions.MidlineLeft);
            Place(name.rectTransform, new Vector2(0f, .5f), new Vector2(FlagWidth + 44f, 0f),
                new Vector2(ToggleWidth - FlagWidth - 60f, RowHeight));
            name.textWrappingMode = TextWrappingModes.NoWrap;
            name.enableAutoSizing = true;
            name.fontSizeMax = RowFont;
            name.fontSizeMin = RowFont * .7f;
            name.text = language.NativeName;
            row.gameObject.AddComponent<HoverTint>().Init(name, normal, UiStyle.Amber, back);
        }

        private void Choose(string code)
        {
            Close();
            if (code != Loc.Current) onSelect?.Invoke(code);
        }

        // Флаг в тонкой светлой рамке, как наклейка; правый край — в точке position.
        private static void Flag(Transform parent, string code, Vector2 anchor, Vector2 position)
        {
            RectTransform frame = UiStyle.Rect("Flag", parent, anchor, new Vector2(1f, .5f), position,
                new Vector2(FlagWidth + Border * 2f, FlagHeight + Border * 2f));
            Image edge = frame.gameObject.AddComponent<Image>();
            edge.color = UiStyle.Ash;
            edge.raycastTarget = false;
            RectTransform inner = UiStyle.Stretch("Picture", frame);
            inner.offsetMin = new Vector2(Border, Border);
            inner.offsetMax = new Vector2(-Border, -Border);
            Texture2D texture = Resources.Load<Texture2D>("UI/Flags/" + code);
            if (texture != null)
            {
                RawImage picture = inner.gameObject.AddComponent<RawImage>();
                picture.texture = texture;
                picture.raycastTarget = false;
            }
            else
            {
                Image empty = inner.gameObject.AddComponent<Image>();
                empty.color = UiStyle.Ink;
                empty.raycastTarget = false;
            }
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Наведение: подпись янтарная, у строк списка ещё и подсветка фона.
        private sealed class HoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            private static readonly Color RowHighlight = new Color(1f, 1f, 1f, .07f);
            private TextMeshProUGUI label;
            private Color normal, hover;
            private Image back;

            public void Init(TextMeshProUGUI target, Color normalColor, Color hoverColor, Image background = null)
            {
                label = target;
                normal = normalColor;
                hover = hoverColor;
                back = background;
            }

            public void OnPointerEnter(PointerEventData eventData) => Set(true);
            public void OnPointerExit(PointerEventData eventData) => Set(false);
            private void OnDisable() => Set(false);

            private void Set(bool on)
            {
                if (label != null) label.color = on ? hover : normal;
                if (back != null) back.color = on ? RowHighlight : Color.clear;
            }
        }
    }
}
