using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // B-05: у люка, если выполнены условия E5, герой выбирает — заколотить или спуститься вниз.
    public sealed class HatchDescentWindow : MinigameWindow
    {
        private UiKeyHints keys;
        private Action boardChosen, descendChosen;

        protected override string WindowName => "Hatch Choice (E5)";

        public bool Open(Action onBoard, Action onDescend)
        {
            if (!TryOpen()) return false;
            boardChosen = onBoard;
            descendChosen = onDescend;
            keys.Set("1", "ЗАКОЛОТИТЬ", "2", "СПУСТИТЬСЯ", "ESC", "ПОДУМАТЬ");
            return true;
        }

        protected override void Build(Transform root)
        {
            TMP_FontAsset font = UiStyle.Font();
            var panel = UiStyle.Rect("Panel", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(760f, 200f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = UiStyle.Ink;
            panelImage.raycastTarget = false;

            var title = UiStyle.Text("Title", panel, font, 30f, UiStyle.Amber, TextAlignmentOptions.Top);
            Stretch(title.rectTransform, 20f, -20f, 140f, -14f);
            title.text = "ЛЮК ПОДВАЛА";

            var body = UiStyle.Text("Body", panel, font, 22f, UiStyle.Ivory, TextAlignmentOptions.Center);
            Stretch(body.rectTransform, 30f, -30f, 60f, -64f);
            body.text = "Заколотить — и оно больше не вылезет.\nИли спуститься и узнать, кто стучал.";

            keys = KeyHints(font, -110f);
        }

        private static void Stretch(RectTransform rect, float left, float right, float bottom, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        protected override void Tick()
        {
            if (CancelPressed) { Cancel(); return; }
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) Choose(boardChosen);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) Choose(descendChosen);
        }

        private void Choose(Action action)
        {
            boardChosen = descendChosen = null;
            if (!CloseWindow()) return;
            action?.Invoke();
        }

        protected override void Cancel()
        {
            boardChosen = descendChosen = null;
            CloseWindow();
        }
    }
}
