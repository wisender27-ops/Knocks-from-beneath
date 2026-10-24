using System;
using TMPro;
using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class DinnerCookingWindow : MonoBehaviour
    {
        private GameObject root;
        private RectTransform needle;
        private TextMeshProUGUI title, hint;
        private UiKeyHints keys;
        private PlayerController player;
        private PlayerInteraction interaction;
        private Action complete, cancel;
        private bool open, finished, cameraLocked, interactionEnabled, cursorVisible;
        private bool started, attemptUsed;
        private CursorLockMode cursorLock;
        private float elapsed;
        private const float SweepSeconds = 1.5f;
        public bool IsOpen => open;

        public void ResetAttempt()
        {
            started = false;
            Close(false);
            attemptUsed = false;
        }

        public bool Open(Action onComplete, Action onCancel)
        {
            if (FadeTransitionPresentation.IsActive || open || attemptUsed || Time.timeScale == 0f || QuestJournalScreen.IsAnyOpen || RetroInventoryScreen.IsAnyOpen)
                return false;
            player = FindFirstObjectByType<PlayerController>();
            if (player != null && player.isCameraLocked) return false;
            interaction = FindFirstObjectByType<PlayerInteraction>();
            if (root == null) Build();
            complete = onComplete;
            cancel = onCancel;
            cameraLocked = player != null && player.isCameraLocked;
            interactionEnabled = interaction != null && interaction.enabled;
            cursorLock = Cursor.lockState;
            cursorVisible = Cursor.visible;
            if (player != null) player.isCameraLocked = true;
            if (interaction != null) interaction.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            open = true;
            root.SetActive(true);
            ShowInstructions();
            return true;
        }

        private void ShowInstructions()
        {
            elapsed = 0f;
            finished = false;
            started = false;
            title.text = "► ГОТОВКА";
            hint.text = "Останови указатель пробелом в янтарной зоне. Попытка одна.";
            keys.Set("ENTER", "НАЧАТЬ", "ESC", "ВЫЙТИ");
            MoveNeedle(0f);
        }

        private void Update()
        {
            if (!open || Time.timeScale == 0f || PauseMenuController.InputBlocked) return;
            if (!started)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { Close(false); return; }
                if (Input.GetKeyDown(KeyCode.Return))
                {
                    started = attemptUsed = true;
                    hint.text = "Останови указатель в янтарной зоне";
                    keys.Set("ПРОБЕЛ", "СТОП");
                }
                return;
            }
            if (finished)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape)) Close(true);
                return;
            }
            elapsed += Time.deltaTime;
            float position = Mathf.Clamp01(elapsed / SweepSeconds);
            MoveNeedle(position);
            if (!Input.GetKeyDown(KeyCode.Space) && position < 1f) return;
            finished = true;
            title.text = Quality(position);
            hint.text = string.Empty;
            keys.Set("ENTER", "ПОДАТЬ УЖИН");
        }

        public static string Quality(float position)
        {
            if (position >= 0.47f && position <= 0.55f) return "Идеально!";
            if (position >= 0.35f && position <= 0.67f) return "Нормально";
            return "Подгорело";
        }

        private void MoveNeedle(float position)
        {
            float angle = Mathf.Lerp(145f, 35f, position);
            float radians = angle * Mathf.Deg2Rad;
            needle.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 300f;
            needle.localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        private void Close(bool success)
        {
            if (!open) return;
            open = false;
            root.SetActive(false);
            if (player != null) player.isCameraLocked = cameraLocked;
            if (interaction != null) interaction.enabled = interactionEnabled;
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
            // Закрытие после старта тоже расходует попытку и завершает квест готовки.
            var callback = success || started ? complete : cancel;
            complete = cancel = null;
            callback?.Invoke();
        }

        private void OnDisable() => Close(false);
        private void OnDestroy()
        {
            Close(false);
            if (root != null) Destroy(root);
        }

        private void Build()
        {
            root = new GameObject("DinnerCooking", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            var shade = Rect("Shade", root.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.62f);
            var arc = Rect("CookingArc", root.transform, new Vector2(0f, -200f), new Vector2(700f, 700f));
            arc.gameObject.AddComponent<DinnerCookingArc>().raycastTarget = false;
            needle = Rect("Needle", arc, Vector2.zero, new Vector2(6f, 66f));
            needle.gameObject.AddComponent<UnityEngine.UI.Image>().color = UiStyle.Ivory;
            title = Label("Title", 215f, 30f);
            title.color = UiStyle.Amber;
            hint = Label("Controls", -250f, 20f);
            keys = UiKeyHints.Create(root.transform, "Keys", title.font);
            keys.RectTransform.anchorMin = keys.RectTransform.anchorMax = keys.RectTransform.pivot = new Vector2(0.5f, 0.5f);
            keys.RectTransform.anchoredPosition = new Vector2(0f, -300f);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private TextMeshProUGUI Label(string name, float y, float size)
        {
            var text = Rect(name, root.transform, new Vector2(0f, y), new Vector2(1000f, 70f))
                .gameObject.AddComponent<TextMeshProUGUI>();
            var manager = FindFirstObjectByType<QuestManager>();
            text.font = UiStyle.Font(manager != null && manager.questUiText != null ? manager.questUiText.font : null);
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = UiStyle.Ivory;
            text.raycastTarget = false;
            return text;
        }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DinnerCookingArc : UnityEngine.UI.Graphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
        {
            mesh.Clear();
            Color rim = new Color(UiStyle.Ivory.r, UiStyle.Ivory.g, UiStyle.Ivory.b, 0.55f);
            Draw(mesh, 0f, 1f, 286f, 314f, new Color(0f, 0f, 0f, 0.84f));
            Draw(mesh, 0.35f, 0.67f, 286f, 314f, new Color(UiStyle.Ash.r, UiStyle.Ash.g, UiStyle.Ash.b, 0.7f));
            Draw(mesh, 0.47f, 0.55f, 286f, 314f, UiStyle.Amber);
            Draw(mesh, 0f, 1f, 317f, 320f, rim);
            Draw(mesh, 0f, 1f, 280f, 283f, rim);
        }

        private static void Draw(UnityEngine.UI.VertexHelper mesh, float start, float end,
            float inner, float outer, Color color)
        {
            int segments = Mathf.Max(2, Mathf.CeilToInt((end - start) * 80f));
            for (int i = 0; i < segments; i++)
            {
                float a = Mathf.Lerp(145f, 35f, Mathf.Lerp(start, end, (float)i / segments)) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(145f, 35f, Mathf.Lerp(start, end, (float)(i + 1) / segments)) * Mathf.Deg2Rad;
                var left = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var right = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int index = mesh.currentVertCount;
                mesh.AddVert(left * inner, color, Vector2.zero);
                mesh.AddVert(left * outer, color, Vector2.zero);
                mesh.AddVert(right * outer, color, Vector2.zero);
                mesh.AddVert(right * inner, color, Vector2.zero);
                mesh.AddTriangle(index, index + 1, index + 2);
                mesh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
