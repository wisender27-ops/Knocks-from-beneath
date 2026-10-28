using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    public sealed class RetroInventoryScreen : MonoBehaviour
    {
        private const float ReferenceWidth = 640f;
        private const float ReferenceHeight = 360f;
        private static readonly Color BackgroundColor = new Color32(33, 33, 34, 255);

        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [SerializeField] private float rotationSpeed = 18f;

        private Canvas canvas;
        private CanvasGroup group;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI descriptionText;
        private UiKeyHints actionHints;
        private TextMeshProUGUI emptyText;
        private RawImage previewImage;
        private RawImage noiseImage;
        private ItemPreviewStage previewStage;
        private Texture2D noiseTexture;
        private GameObject previewObject;
        private sealed class PreviewSlot
        {
            public RawImage Image;
            public ItemPreviewStage Stage;
            public InventoryItemDefinition Definition;
            public float Angle;
        }

        private readonly PreviewSlot[] previewSlots = new PreviewSlot[3];
        private int frontSlot;
        private int pendingSelection;
        private int pendingFrontSlot;
        private int carouselDirection;
        private float carouselProgress;
        private bool carouselMoving;
        private bool backSlotReplaced;
        private RectTransform carouselRoot;
        private bool isInspecting;
        private bool draggingPreview;
        private Vector3 lastMousePosition;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI leftArrowText;
        private TextMeshProUGUI rightArrowText;
        private TextMeshProUGUI feedbackText;
        private float feedbackUntil;
        private PlayerController playerController;
        private PlayerInteraction playerInteraction;
        private int selectedIndex;
        private bool isOpen;
        private bool interactionWasEnabled;
        private bool cameraWasLocked;
        private bool audioWasPaused;
        private float previousTimeScale = 1f;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        public static bool IsAnyOpen { get; private set; }

        private void Awake()
        {
            if (inventory == null) inventory = FindAnyObjectByType<PlayerInventory>();
            if (inventory != null)
            {
                playerController = inventory.GetComponent<PlayerController>();
                playerInteraction = inventory.GetComponent<PlayerInteraction>();
            }
            BuildInterface();
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (inventory != null) inventory.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (inventory != null) inventory.Changed -= Refresh;
            if (isOpen) Close();
        }

        private void OnDestroy()
        {
            ClearPreview();
            for (int i = 0; i < previewSlots.Length; i++) previewSlots[i]?.Stage.Dispose();
            if (noiseTexture != null) Destroy(noiseTexture);
        }

        private void Update()
        {
            if (PlayerInteraction.IsLockSequencePlaying || CornerPeekSequence.IsPlaying || ArrivalCutscene.IsPlaying) return;
            if (Input.GetKeyDown(toggleKey))
            {
                if (isOpen) Close(); else Open();
                return;
            }

            if (!isOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Z))
            {
                if (isInspecting) SetInspection(false); else Close();
                return;
            }
            UpdateCarousel();
            if (!carouselMoving && Input.GetKeyDown(KeyCode.C) && inventory.Entries.Count > 0) SetInspection(!isInspecting);
            if (!carouselMoving && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))) UseSelected();
            if (isInspecting) UpdateInspection();
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SelectRelative(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SelectRelative(1);


            if (feedbackUntil > 0f && Time.unscaledTime >= feedbackUntil)
            {
                feedbackText.text = string.Empty;
                feedbackUntil = 0f;
            }

            group.alpha = 0.97f + Mathf.Sin(Time.unscaledTime * 43f) * 0.025f;
            float noiseOffset = Mathf.Repeat(Time.unscaledTime * 7.3f, 1f);
            noiseImage.uvRect = new Rect(noiseOffset, noiseOffset * 0.61f, 5f, 3f);
        }

        private void Open()
        {
            if (DeathTapeEffect.IsActive || FadeTransitionPresentation.IsActive || inventory == null || isOpen || IsAnyOpen || Time.timeScale == 0f ||
                (playerInteraction != null && playerInteraction.IsEatingPie) ||
                (playerController != null && playerController.isCameraLocked)) return;
            isOpen = true;
            IsAnyOpen = true;
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            interactionWasEnabled = playerInteraction != null && playerInteraction.enabled;
            cameraWasLocked = playerController != null && playerController.isCameraLocked;
            audioWasPaused = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (playerController != null) playerController.isCameraLocked = true;
            if (playerInteraction != null) playerInteraction.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetVisible(true);
            SetInspection(false);
            ClampSelection();
            Refresh();
            GameAudio.PlayUi(UiSound.InventoryOpen);
        }

        private void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            IsAnyOpen = false;
            Time.timeScale = previousTimeScale;
            AudioListener.pause = audioWasPaused;
            if (playerController != null) playerController.isCameraLocked = cameraWasLocked;
            if (playerInteraction != null) playerInteraction.enabled = interactionWasEnabled;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            SetVisible(false);
            SetInspection(false);
            GameAudio.PlayUi(UiSound.InventoryClose);
            ClearPreview();
            feedbackText.text = string.Empty;
            feedbackUntil = 0f;
        }

        private void SelectRelative(int direction)
        {
            int count = inventory.Entries.Count;
            if (count < 2 || carouselMoving) return;
            carouselDirection = direction > 0 ? 1 : -1;
            pendingSelection = (selectedIndex + carouselDirection + count) % count;
            pendingFrontSlot = (frontSlot + (count == 2 || carouselDirection > 0 ? 1 : 2)) % 3;
            carouselProgress = 0f;
            backSlotReplaced = false;
            carouselMoving = true;
            GameAudio.PlayUi(UiSound.InventoryMove);
            feedbackText.text = string.Empty;
            feedbackUntil = 0f;
        }

        private void UpdateCarousel()
        {
            if (isInspecting) return;
            if (carouselMoving)
            {
                carouselProgress = Mathf.Min(1f, carouselProgress + Time.unscaledDeltaTime / 0.28f);
                float eased = Mathf.SmoothStep(0f, 1f, carouselProgress);
                int count = inventory.Entries.Count;
                float step = count == 2 ? 180f : 120f;
                // В дальней точке исходящий сосед заменяется следующим: на экране максимум три модели.
                if (count > 3 && !backSlotReplaced && eased >= 0.5f)
                {
                    int outgoing = (frontSlot + (carouselDirection > 0 ? 2 : 1)) % 3;
                    int incoming = (pendingSelection + carouselDirection + count) % count;
                    PopulateSlot(previewSlots[outgoing], inventory.Entries[incoming].Definition);
                    backSlotReplaced = true;
                }
                for (int i = 0; i < previewSlots.Length; i++)
                {
                    PreviewSlot slot = previewSlots[i];
                    if (!slot.Image.gameObject.activeSelf) continue;
                    float fade = 1f;
                    if (count > 3 && i == (frontSlot + (carouselDirection > 0 ? 2 : 1)) % 3)
                        fade = Mathf.Abs(eased * 2f - 1f);
                    LayoutSlot(slot, slot.Angle + carouselDirection * step * eased, fade);
                }
                if (carouselProgress >= 1f)
                {
                    selectedIndex = pendingSelection;
                    frontSlot = pendingFrontSlot;
                    carouselMoving = false;
                    Refresh();
                }
            }
            for (int i = 0; i < previewSlots.Length; i++)
            {
                PreviewSlot slot = previewSlots[i];
                if (slot.Image.gameObject.activeSelf)
                    slot.Stage.Rotate(rotationSpeed * Time.unscaledDeltaTime, 0f);
            }
            // Задние предметы рисуются раньше переднего, не перекрывая его.
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < 2; i++)
                    if (carouselRoot.GetChild(i).localScale.x > carouselRoot.GetChild(i + 1).localScale.x)
                        carouselRoot.GetChild(i).SetSiblingIndex(i + 1);
        }

        private static void LayoutSlot(PreviewSlot slot, float angle, float alpha = 1f)
        {
            float radians = angle * Mathf.Deg2Rad;
            float depth = (Mathf.Cos(radians) + 1f) * 0.5f;
            slot.Image.rectTransform.anchoredPosition = new Vector2(132f - Mathf.Sin(radians) * 90f, 22f - Mathf.Cos(radians) * 30f);
            slot.Image.rectTransform.sizeDelta = new Vector2(240f, 192f);
            slot.Image.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.48f, 1f, depth);
            slot.Image.color = new Color(1f, 1f, 1f, alpha * Mathf.Lerp(0.55f, 1f, depth));
        }

        private void UseSelected()
        {
            if (inventory == null || selectedIndex < 0 || selectedIndex >= inventory.Entries.Count) return;
            InventoryItemDefinition definition = inventory.Entries[selectedIndex].Definition;
            bool wasEquipped = inventory.EquippedItemId == definition.Id;
            if (inventory.UseItemAt(selectedIndex))
            {
                ClampSelection();
                Refresh();
                feedbackText.color = UiStyle.Amber;
                feedbackText.text = definition.IsEquippable
                    ? (wasEquipped ? "Убрано: " : "В руках: ") + definition.DisplayName
                    : "Использовано: " + definition.DisplayName;
            }
            else
            {
                feedbackText.text = string.Empty;
            }
            feedbackUntil = Time.unscaledTime + 3f;
        }

        private void SetInspection(bool value)
        {
            carouselMoving = false;
            isInspecting = value;
            draggingPreview = false;
            titleText.text = value ? "► ОСМОТР" : "► ИНВЕНТАРЬ";
            titleText.rectTransform.anchoredPosition = value ? new Vector2(0f, -12f) : new Vector2(0f, -39f);
            nameText.rectTransform.anchoredPosition = value ? new Vector2(350f, -76f) : new Vector2(24f, -86f);
            nameText.rectTransform.sizeDelta = value ? new Vector2(266f, 34f) : new Vector2(592f, 30f);
            descriptionText.rectTransform.anchoredPosition = value ? new Vector2(350f, -116f) : new Vector2(24f, -126f);
            descriptionText.rectTransform.sizeDelta = value ? new Vector2(266f, 152f) : new Vector2(236f, 142f);
            previewImage.rectTransform.localScale = Vector3.one;
            previewImage.color = Color.white;
            previewImage.rectTransform.anchoredPosition = value ? new Vector2(-150f, 16f) : new Vector2(132f, -8f);
            previewImage.rectTransform.sizeDelta = value ? new Vector2(280f, 224f) : new Vector2(240f, 192f);
            previewStage.SetZoom(1f);
            Refresh();
        }

        private void UpdateInspection()
        {
            if (previewObject == null) return;
            float yaw = (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D) ? 1f : 0f)
                - (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A) ? 1f : 0f);
            float pitch = (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) ? 1f : 0f)
                - (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) ? 1f : 0f);
            yaw *= 90f * Time.unscaledDeltaTime;
            pitch *= 90f * Time.unscaledDeltaTime;
            if (Input.GetMouseButtonDown(0))
            {
                draggingPreview = RectTransformUtility.RectangleContainsScreenPoint(previewImage.rectTransform, Input.mousePosition);
                lastMousePosition = Input.mousePosition;
            }
            if (!Input.GetMouseButton(0)) draggingPreview = false;
            if (draggingPreview)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                yaw += delta.x * 0.4f;
                pitch -= delta.y * 0.4f;
                lastMousePosition = Input.mousePosition;
            }
            previewStage.Rotate(yaw, pitch);
            previewStage.SetZoom(Mathf.Clamp(previewStage.Zoom + Input.mouseScrollDelta.y * 0.1f, 0.6f, 1.5f));
            if (Input.GetKeyDown(KeyCode.R)) previewStage.ResetView();
        }

        private void ClampSelection()
        {
            selectedIndex = inventory.Entries.Count == 0 ? 0 : Mathf.Clamp(selectedIndex, 0, inventory.Entries.Count - 1);
        }

        private void Refresh()
        {
            if (!isOpen || inventory == null) return;
            carouselMoving = false;
            ClampSelection();
            bool hasItems = inventory.Entries.Count > 0;
            // Стрелки выбора имеют смысл, только когда есть между чем выбирать.
            bool canSelect = !isInspecting && inventory.Entries.Count > 1;
            leftArrowText.gameObject.SetActive(canSelect);
            rightArrowText.gameObject.SetActive(canSelect);
            emptyText.gameObject.SetActive(!hasItems);
            nameText.gameObject.SetActive(hasItems);
            descriptionText.gameObject.SetActive(hasItems);
            previewImage.gameObject.SetActive(hasItems);

            if (!hasItems)
            {
                if (isInspecting) SetInspection(false);
                actionHints.Set("TAB", "ЗАКРЫТЬ");
                ClearPreview();
                return;
            }

            InventoryEntry entry = inventory.Entries[selectedIndex];
            bool equipped = inventory.EquippedItemId == entry.Definition.Id;
            nameText.text = entry.Definition.MaxStack > 1
                ? entry.Definition.DisplayName + "  ×" + entry.Quantity
                : entry.Definition.DisplayName;
            descriptionText.text = entry.Definition.Description;
            string useAction = entry.Definition.IsEquippable ? (equipped ? "УБРАТЬ" : "ВЗЯТЬ") : "ИСПОЛЬЗОВАТЬ";
            if (isInspecting)
                actionHints.Set("МЫШЬ", "ВРАЩАТЬ", "КОЛЕСО", "МАСШТАБ", "R", "СБРОС", "ПРОБЕЛ", useAction, "C", "НАЗАД");
            else if (inventory.Entries.Count > 1)
                actionHints.Set("A D", "ВЫБОР", "C", "ОСМОТР", "ПРОБЕЛ", useAction, "TAB", "ЗАКРЫТЬ");
            else
                actionHints.Set("C", "ОСМОТР", "ПРОБЕЛ", useAction, "TAB", "ЗАКРЫТЬ");
            ReplacePreview(entry.Definition);
            for (int offset = 0; offset < 3; offset++)
            {
                PreviewSlot slot = previewSlots[(frontSlot + offset) % 3];
                bool visible = offset < inventory.Entries.Count && (!isInspecting || offset == 0);
                slot.Image.gameObject.SetActive(visible);
                slot.Stage.Visible = visible;
                if (!visible) { if (offset != 0) PopulateSlot(slot, null); continue; }
                if (offset != 0)
                {
                    int index = (selectedIndex + (offset == 1 ? 1 : -1) + inventory.Entries.Count) % inventory.Entries.Count;
                    PopulateSlot(slot, inventory.Entries[index].Definition);
                }
                slot.Angle = offset == 0 ? 0f : inventory.Entries.Count == 2 ? -180f : offset == 1 ? -120f : 120f;
                if (!isInspecting) LayoutSlot(slot, slot.Angle);
            }
        }

        private void ReplacePreview(InventoryItemDefinition definition)
        {
            PreviewSlot slot = previewSlots[frontSlot];
            PopulateSlot(slot, definition);
            previewImage = slot.Image;
            previewStage = slot.Stage;
            previewObject = slot.Stage.Root;
        }

        private void PopulateSlot(PreviewSlot slot, InventoryItemDefinition definition)
        {
            if (slot.Definition == definition) return;
            slot.Stage.Clear();
            slot.Definition = definition;
            slot.Image.texture = slot.Stage.Texture;
            slot.Image.uvRect = new Rect(0f, 0f, 1f, 1f);
            if (definition == null) return;
            if (definition.PreviewPrefab == null)
            {
                if (definition.Icon != null)
                {
                    Sprite icon = definition.Icon;
                    Rect rect = icon.textureRect;
                    slot.Image.texture = icon.texture;
                    slot.Image.uvRect = new Rect(rect.x / icon.texture.width, rect.y / icon.texture.height,
                        rect.width / icon.texture.width, rect.height / icon.texture.height);
                }
                return;
            }
            Transform root = slot.Stage.Begin("InventoryPreview_" + definition.Id);
            GameObject model = Instantiate(definition.PreviewPrefab, root);
            // Скрипты удаляются с неактивной копии до её активации.
            MonoBehaviour[] scripts = model.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = scripts.Length - 1; i >= 0; i--)
                if (scripts[i] != null) DestroyImmediate(scripts[i]);
            if (model.GetComponentsInChildren<MonoBehaviour>(true).Length > 0) return;
            Behaviour[] behaviours = model.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = false;
            ParticleSystem[] particles = model.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++) particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            slot.Stage.Present(model, definition.PreviewTextureOverride);
        }

        private void ClearPreview()
        {
            carouselMoving = false;
            for (int i = 0; i < previewSlots.Length; i++)
            {
                PreviewSlot slot = previewSlots[i];
                if (slot == null) continue;
                slot.Stage.Clear();
                slot.Stage.Visible = false;
                slot.Definition = null;
                slot.Image.gameObject.SetActive(false);
            }
            previewObject = null;
        }

        private void BuildInterface()
        {
            GameObject canvasObject = new GameObject("RetroInventoryCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasObject.GetComponent<CanvasGroup>();

            RectTransform root = CreateRect("InventoryRoot", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RawImage fallbackBackground = root.gameObject.AddComponent<RawImage>();
            fallbackBackground.texture = Texture2D.whiteTexture;
            fallbackBackground.color = BackgroundColor;
            fallbackBackground.raycastTarget = false;

            RectTransform backgroundFrame = CreateRect("BackgroundCover", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
            AspectRatioFitter backgroundFitter = backgroundFrame.gameObject.AddComponent<AspectRatioFitter>();
            backgroundFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            backgroundFitter.aspectRatio = ReferenceWidth / ReferenceHeight;
            RawImage background = backgroundFrame.gameObject.AddComponent<RawImage>();
            background.texture = Texture2D.whiteTexture;
            background.color = BackgroundColor;
            background.raycastTarget = false;

            noiseImage = CreateRawImage("Noise", backgroundFrame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            noiseTexture = CreateNoiseTexture();
            noiseImage.texture = noiseTexture;
            noiseImage.color = new Color(1f, 1f, 1f, 0.085f);

            RectTransform frame = CreateRect("ContentFrame", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
            AspectRatioFitter contentFitter = frame.gameObject.AddComponent<AspectRatioFitter>();
            contentFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            contentFitter.aspectRatio = ReferenceWidth / ReferenceHeight;

            titleText = CreateText("Title", frame, "► ИНВЕНТАРЬ", 23f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -39f), new Vector2(360f, 42f));
            leftArrowText = CreateText("LeftArrow", frame, "<", 28f, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(155f, -45f), new Vector2(40f, 40f));
            rightArrowText = CreateText("RightArrow", frame, ">", 28f, TextAlignmentOptions.Center, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-155f, -45f), new Vector2(40f, 40f));

            nameText = CreateText("ItemName", frame, string.Empty, 19f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -112f), new Vector2(160f, 34f));
            descriptionText = CreateText("Description", frame, string.Empty, 18f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -148f), new Vector2(160f, 132f));
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.lineSpacing = 0f;
            descriptionText.enableAutoSizing = true;
            descriptionText.fontSizeMin = 12f;
            descriptionText.fontSizeMax = 18f;

            carouselRoot = CreateRect("Carousel", frame, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            for (int i = 0; i < previewSlots.Length; i++)
            {
                PreviewSlot slot = new PreviewSlot();
                slot.Image = CreateRawImage("Preview" + i, carouselRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 192f));
                slot.Stage = new ItemPreviewStage(transform, "InventoryPreview" + i, -1000f - i * 40f, 320, 256);
                slot.Image.texture = slot.Stage.Texture;
                previewSlots[i] = slot;
            }
            previewImage = previewSlots[0].Image;
            previewStage = previewSlots[0].Stage;

            emptyText = CreateText("Empty", frame, "Карманы пусты", 20f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(400f, 50f));
            feedbackText = CreateText("Feedback", frame, string.Empty, 14f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(592f, 24f));
            actionHints = UiKeyHints.Create(frame, "Actions", font, 0.5f);
            actionHints.RectTransform.anchorMin = actionHints.RectTransform.anchorMax = actionHints.RectTransform.pivot = new Vector2(0.5f, 0f);
            actionHints.RectTransform.anchoredPosition = new Vector2(0f, 18f);
            titleText.color = UiStyle.Amber;
            leftArrowText.color = rightArrowText.color = UiStyle.Amber;
            leftArrowText.text = "<";
            rightArrowText.text = ">";
            descriptionText.color = UiStyle.Ash;
        }

        private TextMeshProUGUI CreateText(string objectName, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
        {
            RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax, position, dimensions);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = UiStyle.Font(font);
            text.text = value;
            text.fontSize = size;
            text.color = UiStyle.Ivory;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.extraPadding = true;
            return text;
        }

        private static RawImage CreateRawImage(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
        {
            RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax, position, dimensions);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            return rect;
        }

        private static Texture2D CreateNoiseTexture()
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "InventoryNoise", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            Color32[] pixels = new Color32[size * size];
            uint seed = 0x81A4u;
            for (int i = 0; i < pixels.Length; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                byte value = (byte)(28 + (seed >> 28) * 5);
                pixels[i] = new Color32(value, value, value, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void SetVisible(bool value)
        {
            if (canvas != null) canvas.enabled = value;
            for (int i = 0; i < previewSlots.Length; i++)
                if (previewSlots[i] != null) previewSlots[i].Stage.Visible = value && previewSlots[i].Image.gameObject.activeSelf;
        }
    }
}
