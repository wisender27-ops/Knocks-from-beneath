using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Ряд подсказок «клавиша — действие» в стиле подсказок взаимодействия.
    public sealed class UiKeyHints : MonoBehaviour
    {
        private readonly List<RectTransform> chips = new List<RectTransform>();
        private readonly List<TextMeshProUGUI> keys = new List<TextMeshProUGUI>();
        private readonly List<TextMeshProUGUI> labels = new List<TextMeshProUGUI>();
        private TMP_FontAsset font;
        private float keySize, labelSize, height;
        private Color labelColor;
        private string signature;

        public static UiKeyHints Create(Transform parent, string name, TMP_FontAsset font, float scale = 1f, Color? labelColor = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var row = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 22f * scale;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var hints = rect.gameObject.AddComponent<UiKeyHints>();
            // Все подсказки клавиш — одним шрифтом и капсом, что бы ни передал экран.
            hints.font = UiStyle.Font();
            hints.keySize = 15f * scale;
            hints.labelSize = 17f * scale;
            hints.height = 30f * scale;
            hints.labelColor = labelColor ?? UiStyle.Ivory;
            return hints;
        }

        public RectTransform RectTransform => (RectTransform)transform;

        // Подложка для подсказок поверх игрового кадра: на светлых стенах голый текст теряется.
        public UiKeyHints Backed()
        {
            int pad = Mathf.RoundToInt(height * .4f);
            GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(pad, pad, pad / 2, pad / 2);
            var plate = gameObject.AddComponent<Image>();
            plate.color = UiStyle.InkSoft;
            plate.raycastTarget = false;
            return this;
        }

        // Пары «клавиша, действие». Пустая клавиша — только текст, пустое действие — только клавиша.
        public void Set(params string[] pairs)
        {
            string next = pairs == null ? string.Empty : string.Join("|", pairs);
            if (next == signature) return;
            signature = next;
            int count = pairs == null ? 0 : pairs.Length / 2;
            while (chips.Count < count) AddChip();
            for (int i = 0; i < chips.Count; i++)
            {
                bool active = i < count;
                chips[i].gameObject.SetActive(active);
                if (!active) continue;
                string key = pairs[i * 2];
                keys[i].text = key?.ToUpperInvariant();
                keys[i].transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(key));
                labels[i].text = pairs[i * 2 + 1]?.ToUpperInvariant();
                labels[i].gameObject.SetActive(!string.IsNullOrEmpty(labels[i].text));
            }
        }

        private void AddChip()
        {
            var chip = new GameObject("Hint" + chips.Count, typeof(RectTransform)).GetComponent<RectTransform>();
            chip.SetParent(transform, false);
            var row = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = height * .32f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            var cap = new GameObject("Key", typeof(RectTransform)).GetComponent<RectTransform>();
            cap.SetParent(chip, false);
            cap.gameObject.AddComponent<Image>().color = UiStyle.Ivory;
            var capLayout = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
            int pad = Mathf.RoundToInt(height * .3f);
            capLayout.padding = new RectOffset(pad, pad, 0, 0);
            capLayout.childControlWidth = capLayout.childControlHeight = true;
            capLayout.childForceExpandWidth = capLayout.childForceExpandHeight = false;
            capLayout.childAlignment = TextAnchor.MiddleCenter;
            var capElement = cap.gameObject.AddComponent<LayoutElement>();
            capElement.minWidth = height;
            capElement.preferredHeight = height;
            var key = UiStyle.Text("KeyLabel", cap, font, keySize, UiStyle.KeyInk, TextAlignmentOptions.Center);
            key.textWrappingMode = TextWrappingModes.NoWrap;
            var label = UiStyle.Text("Action", chip, font, labelSize, labelColor, TextAlignmentOptions.MidlineLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            chips.Add(chip);
            keys.Add(key);
            labels.Add(label);
        }
    }
}
