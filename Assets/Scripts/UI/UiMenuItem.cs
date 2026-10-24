using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Пункт меню в духе экранного меню VHS: выбранный пункт — янтарный, с маркером и подложкой.
    public sealed class UiMenuItem : MonoBehaviour, IPointerEnterHandler, ISelectHandler, IDeselectHandler
    {
        private TextMeshProUGUI label, marker;
        private Image backing;
        private Button button;

        public Button Button => button;

        public static UiMenuItem Create(Transform parent, string caption, TMP_FontAsset font, float size, UnityEngine.Events.UnityAction action)
        {
            var rect = new GameObject(caption, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var item = rect.gameObject.AddComponent<UiMenuItem>();
            item.backing = rect.gameObject.AddComponent<Image>();
            item.backing.color = Color.clear;
            item.button = rect.gameObject.AddComponent<Button>();
            item.button.transition = Selectable.Transition.None;
            item.button.targetGraphic = item.backing;
            item.button.onClick.AddListener(action);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(Mathf.RoundToInt(size * .45f), Mathf.RoundToInt(size * .7f), Mathf.RoundToInt(size * .18f), Mathf.RoundToInt(size * .12f));
            layout.spacing = size * .35f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            rect.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            item.marker = UiStyle.Text("Marker", rect, font, size * .8f, UiStyle.Amber, TextAlignmentOptions.MidlineLeft);
            item.marker.text = "►";
            item.marker.gameObject.AddComponent<LayoutElement>().preferredWidth = size * .8f;
            item.label = UiStyle.Text("Label", rect, font, size, UiStyle.Ivory, TextAlignmentOptions.MidlineLeft);
            item.label.textWrappingMode = TextWrappingModes.NoWrap;
            item.label.text = caption;
            item.SetHighlighted(false);
            return item;
        }

        public void SetCaption(string value)
        {
            label.text = value;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }

        public void OnSelect(BaseEventData eventData) => SetHighlighted(true);
        public void OnDeselect(BaseEventData eventData) => SetHighlighted(false);
        private void OnDisable() => SetHighlighted(false);

        private void SetHighlighted(bool value)
        {
            if (label == null) return;
            label.color = value ? UiStyle.Amber : UiStyle.Ivory;
            marker.alpha = value ? 1f : 0f;
            backing.color = value ? UiStyle.Ink : new Color(0f, 0f, 0f, .001f);
        }

        public static void Select(UiMenuItem item)
        {
            if (item == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(item.gameObject);
        }

        public static void LinkVertical(IList<UiMenuItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnUp = items[(i - 1 + items.Count) % items.Count].button;
                navigation.selectOnDown = items[(i + 1) % items.Count].button;
                items[i].button.navigation = navigation;
            }
        }
    }
}
