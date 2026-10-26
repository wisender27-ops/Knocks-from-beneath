using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Мини-игра «Осмотр» (M5): предмет крутится в 3D-превью (мышь, WASD), колесо приближает.
    // Скрытая деталь видна только с нужной стороны и читается только вблизи; удержал взгляд — улика
    // записана в StoryFlags, после закрытия герой о ней думает.
    public sealed class ClueInspectWindow : MinigameWindow
    {
        private const float MinZoom = 0.8f;
        private const float MaxZoom = 1.9f;
        private const float MouseSpeed = 4f;      // градусов на единицу оси мыши
        private const float KeySpeed = 110f;

        private static ClueInspectWindow instance;

        private ItemPreviewStage stage;
        private InspectableClue clue;
        private ClueInspection inspection;
        private Transform detail;
        private TextMeshPro detailLabel;
        private TextMeshProUGUI nameText, statusText;
        private UiKeyHints keys;
        private RawImage view;
        private int openedFrame;
        private bool foundNow;
        private static int closedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            closedFrame = -1;
        }

        // E, закрывшая окно, не должна в том же кадре открыть его снова через PlayerInteraction.
        public static bool JustClosed => Time.frameCount == closedFrame;

        public static ClueInspectWindow Instance => instance;
        public InspectableClue Clue => clue;
        public ClueInspection Inspection => inspection;
        public ItemPreviewStage Stage => stage;
        public Transform Detail => detail;

        protected override string WindowName => "ClueInspectWindow";

        public static bool Show(InspectableClue target)
        {
            if (target == null) return false;
            if (instance == null) instance = new GameObject("Clue Inspect Window").AddComponent<ClueInspectWindow>();
            return instance.Open(target);
        }

        private bool Open(InspectableClue target)
        {
            if (!TryOpen()) return false;
            clue = target;
            inspection = new ClueInspection(target.IsFound);
            foundNow = false;
            openedFrame = Time.frameCount;
            nameText.text = target.DisplayName;
            BuildModel();
            stage.Visible = true;
            // Курсор не нужен: мышь вращает предмет, как взгляд. Закрытие окна вернёт прежний режим.
            Cursor.lockState = CursorLockMode.Locked;
            GameAudio.PlayUi(UiSound.InventoryOpen);
            Refresh(ClueView.Hidden);
            return true;
        }

        protected override void Build(Transform root)
        {
            stage = new ItemPreviewStage(transform, "CluePreview", -1200f, 400, 320);
            stage.Visible = false;

            var title = Label("Title", 312f, 30f);
            title.text = "► ОСМОТР";
            title.color = UiStyle.Amber;
            nameText = Label("Name", 268f, 24f);

            view = Rect("Preview", root, new Vector2(0f, 6f), new Vector2(560f, 448f)).gameObject.AddComponent<RawImage>();
            view.texture = stage.Texture;
            view.raycastTarget = false;

            statusText = Label("Status", -262f, 20f);
            statusText.textWrappingMode = TextWrappingModes.Normal;
            statusText.rectTransform.sizeDelta = new Vector2(900f, 70f);

            var manager = FindFirstObjectByType<QuestManager>();
            keys = KeyHints(manager != null && manager.questUiText != null ? manager.questUiText.font : null, -318f);
        }

        protected override void Tick()
        {
            if (clue == null || stage.Model == null) { Cancel(); return; }
            bool fresh = Time.frameCount == openedFrame;
            if (!fresh && (CancelPressed || Input.GetKeyDown(KeyCode.E)))
            {
                Cancel();
                return;
            }

            float yaw = Input.GetAxisRaw("Mouse X") * MouseSpeed;
            float pitch = -Input.GetAxisRaw("Mouse Y") * MouseSpeed;
            yaw += ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f)) * KeySpeed * Time.unscaledDeltaTime;
            pitch += ((Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f) -
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)) * KeySpeed * Time.unscaledDeltaTime;
            if (!fresh) stage.Rotate(yaw, pitch);
            stage.SetZoom(Mathf.Clamp(stage.Zoom + Input.mouseScrollDelta.y * 0.12f, MinZoom, MaxZoom));
            if (Input.GetKeyDown(KeyCode.R)) stage.ResetView();

            float facing = ClueInspection.Facing(detail.TransformDirection(Vector3.back), stage.Camera.transform.position - detail.position);
            if (inspection.Step(facing, stage.Zoom, Time.deltaTime))
            {
                foundNow = true;
                StoryFlags.RegisterClue(clue.ClueId);
                GameAudio.PlayUi(UiSound.QuestNew);
            }
            Refresh(inspection.Classify(facing, stage.Zoom));
        }

        private void Refresh(ClueView state)
        {
            if (inspection.Found)
            {
                statusText.color = UiStyle.Amber;
                statusText.text = clue.FoundCaption;
                if (detailLabel != null) detailLabel.color = clue.PaperLabel ? UiStyle.PaperInk : UiStyle.Amber;
                keys.Set("МЫШЬ", "ВРАЩАТЬ", "КОЛЕСО", "ПРИБЛИЗИТЬ", "E", "ОТЛОЖИТЬ");
                return;
            }
            statusText.color = UiStyle.Ash;
            statusText.text = state == ClueView.Glimpse ? "Здесь что-то есть… надо присмотреться"
                : state == ClueView.Reading ? "…" : string.Empty;
            keys.Set("МЫШЬ", "ВРАЩАТЬ", "КОЛЕСО", "ПРИБЛИЗИТЬ", "R", "СБРОС", "E", "ОТЛОЖИТЬ");
        }

        private void BuildModel()
        {
            Transform root = stage.Begin("CluePreview_" + clue.ClueId);
            var model = new GameObject("Model");
            model.transform.SetParent(root, false);
            var mesh = new GameObject("Mesh", typeof(MeshFilter), typeof(MeshRenderer));
            mesh.transform.SetParent(model.transform, false);
            mesh.transform.localScale = clue.PreviewScale;
            mesh.transform.localRotation = clue.PreviewRotation;
            mesh.GetComponent<MeshFilter>().sharedMesh = clue.PreviewMesh;
            mesh.GetComponent<MeshRenderer>().sharedMaterials = clue.PreviewMaterials;
            stage.Present(model, null);

            // Деталь добавляется после Present: её материалы не заменяются на unlit предмета,
            // а размеры задаются в метрах предмета до вписывания в кадр.
            var part = ClueDetailBuilder.BuildDetail(model.transform, clue, clue.PreviewRotation, clue.PreviewScale, false);
            detail = part.Transform;
            detailLabel = part.Text;
            ClueDetailBuilder.BuildTell(model.transform, clue, clue.PreviewRotation, clue.PreviewScale, false);
            ItemPreviewStage.SetLayerRecursively(model.transform, ItemPreviewStage.PreviewLayer);
        }

        protected override void Cancel()
        {
            InspectableClue closed = clue;
            bool thoughts = foundNow;
            if (!CloseWindow()) return;
            closedFrame = Time.frameCount;
            clue = null;
            detail = null;
            detailLabel = null;
            if (stage != null)
            {
                stage.Clear();
                stage.Visible = false;
            }
            GameAudio.PlayUi(UiSound.InventoryClose);
            if (thoughts && closed != null && closed.ThoughtLines != null && closed.ThoughtLines.Length > 0 && ThoughtManager.Instance != null)
                ThoughtManager.Instance.ShowThoughts(closed.ThoughtLines);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            stage?.Dispose();
            if (instance == this) instance = null;
        }
    }
}
