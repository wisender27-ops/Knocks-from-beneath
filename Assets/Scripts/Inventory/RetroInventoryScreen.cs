using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    public sealed class RetroInventoryScreen : MonoBehaviour
    {
        private const int PreviewLayer = 30;
        private const float ReferenceWidth = 640f;
        private const float ReferenceHeight = 360f;
        private const float PreviewDistance = 2.6f;
        private const float PreviewFrameFill = 0.76f;
        private static readonly Color BackgroundColor = new Color32(33, 33, 34, 255);

        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
        [SerializeField] private float rotationSpeed = 18f;

        private Canvas canvas;
        private CanvasGroup group;
        private TextMeshProUGUI nameText;
        private TextMeshProUGUI descriptionText;
        private TextMeshProUGUI actionText;
        private TextMeshProUGUI emptyText;
        private RawImage previewImage;
        private RawImage noiseImage;
        private Camera previewCamera;
        private RenderTexture previewTexture;
        private Texture2D noiseTexture;
        private GameObject previewObject;
        private InventoryItemDefinition previewDefinition;
        private bool isInspecting;
        private bool draggingPreview;
        private Vector3 lastMousePosition;
        private float previewZoom = 1f;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI leftArrowText;
        private TextMeshProUGUI rightArrowText;
        private TextMeshProUGUI feedbackText;
        private float feedbackUntil;
        private readonly List<Material> previewMaterials = new List<Material>(4);
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
            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
            }
            if (noiseTexture != null) Destroy(noiseTexture);
        }

        private void Update()
        {
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
            if (Input.GetKeyDown(KeyCode.C) && inventory.Entries.Count > 0) SetInspection(!isInspecting);
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) UseSelected();
            if (isInspecting) UpdateInspection();
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SelectRelative(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SelectRelative(1);

            if (previewObject != null && !isInspecting)
                previewObject.transform.Rotate(Vector3.up, rotationSpeed * Time.unscaledDeltaTime, Space.World);

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
            if (inventory == null || isOpen || IsAnyOpen || Time.timeScale == 0f ||
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
            ClearPreview();
            feedbackText.text = string.Empty;
            feedbackUntil = 0f;
        }

        private void SelectRelative(int direction)
        {
            int count = inventory.Entries.Count;
            if (count == 0) return;
            selectedIndex = (selectedIndex + direction + count) % count;
            feedbackText.text = string.Empty;
            feedbackUntil = 0f;
            Refresh();
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
                feedbackText.text = definition.IsEquippable
                    ? (wasEquipped ? "UNEQUIPPED: " : "EQUIPPED: ") + definition.DisplayName
                    : "USED: " + definition.DisplayName;
            }
            else feedbackText.text = "CANNOT USE THIS ITEM HERE";
            feedbackUntil = Time.unscaledTime + 3f;
        }

        private void SetInspection(bool value)
        {
            isInspecting = value;
            draggingPreview = false;
            titleText.text = value ? "-  EXAMINE  -" : "-  INVENTORY  -";
            titleText.rectTransform.anchoredPosition = value ? new Vector2(0f, -12f) : new Vector2(0f, -39f);
            leftArrowText.gameObject.SetActive(!value);
            rightArrowText.gameObject.SetActive(!value);
            nameText.rectTransform.anchoredPosition = value ? new Vector2(350f, -76f) : new Vector2(24f, -86f);
            nameText.rectTransform.sizeDelta = value ? new Vector2(266f, 34f) : new Vector2(592f, 30f);
            descriptionText.rectTransform.anchoredPosition = value ? new Vector2(350f, -116f) : new Vector2(24f, -126f);
            descriptionText.rectTransform.sizeDelta = value ? new Vector2(266f, 152f) : new Vector2(236f, 142f);
            previewImage.rectTransform.anchoredPosition = value ? new Vector2(-150f, 16f) : new Vector2(132f, -8f);
            previewImage.rectTransform.sizeDelta = value ? new Vector2(280f, 224f) : new Vector2(240f, 192f);
            previewZoom = 1f;
            if (previewObject != null) previewObject.transform.localScale = Vector3.one;
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
            previewObject.transform.Rotate(Vector3.up, yaw, Space.World);
            previewObject.transform.Rotate(Vector3.right, pitch, Space.World);
            previewZoom = Mathf.Clamp(previewZoom + Input.mouseScrollDelta.y * 0.1f, 0.6f, 1.5f);
            previewObject.transform.localScale = Vector3.one * previewZoom;
            if (Input.GetKeyDown(KeyCode.R))
            {
                previewObject.transform.rotation = Quaternion.identity;
                previewZoom = 1f;
                previewObject.transform.localScale = Vector3.one;
            }
        }

        private void ClampSelection()
        {
            selectedIndex = inventory.Entries.Count == 0 ? 0 : Mathf.Clamp(selectedIndex, 0, inventory.Entries.Count - 1);
        }

        private void Refresh()
        {
            if (!isOpen || inventory == null) return;
            ClampSelection();
            bool hasItems = inventory.Entries.Count > 0;
            emptyText.gameObject.SetActive(!hasItems);
            nameText.gameObject.SetActive(hasItems);
            descriptionText.gameObject.SetActive(hasItems);
            previewImage.gameObject.SetActive(hasItems);

            if (!hasItems)
            {
                if (isInspecting) SetInspection(false);
                actionText.text = "EXIT: Z / TAB";
                ReplacePreview(null);
                return;
            }

            InventoryEntry entry = inventory.Entries[selectedIndex];
            bool equipped = inventory.EquippedItemId == entry.Definition.Id;
            nameText.text = entry.Definition.DisplayName + "  " + entry.Quantity + "/" + entry.Definition.MaxStack;
            descriptionText.text = entry.Definition.Description;
            string useAction = entry.Definition.IsEquippable ? (equipped ? "UNEQUIP" : "EQUIP") : "USE";
            actionText.text = isInspecting
                ? "DRAG / WASD: ROTATE   WHEEL: ZOOM   R: RESET\n" + useAction + ": SPACE / ENTER   BACK: C / Z / ESC   EXIT: TAB"
                : "SELECT: A / D   EXAMINE: C\n" + useAction + ": SPACE / ENTER   EXIT: Z / ESC / TAB";
            ReplacePreview(entry.Definition);
        }

        private void ReplacePreview(InventoryItemDefinition definition)
        {
            if (previewDefinition == definition) return;
            ClearPreview();
            previewDefinition = definition;
            if (definition == null || definition.PreviewPrefab == null) return;

            Vector3 previewCenter = new Vector3(0f, -1000f, PreviewDistance);
            previewObject = new GameObject("InventoryPreview_" + definition.Id);
            previewObject.SetActive(false);
            previewObject.transform.SetParent(transform, false);
            previewObject.transform.SetPositionAndRotation(previewCenter, Quaternion.identity);

            GameObject model = Instantiate(definition.PreviewPrefab, previewObject.transform);
            // Disabled scripts still receive Awake on activation; strip only the inactive clone.
            MonoBehaviour[] scripts = model.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = scripts.Length - 1; i >= 0; i--)
                if (scripts[i] != null) DestroyImmediate(scripts[i]);
            // A RequireComponent dependency can reject removal. Never activate a surviving script.
            if (model.GetComponentsInChildren<MonoBehaviour>(true).Length > 0) return;
            Behaviour[] behaviours = model.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++) behaviours[i].enabled = false;
            ParticleSystem[] particles = model.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++) particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            model.SetActive(true);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(12f, 25f, 0f));
            SetLayerRecursively(previewObject.transform, PreviewLayer);
            PreparePreviewMaterials(model, definition.PreviewTextureOverride);

            Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            Rigidbody[] bodies = model.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
                bodies[i].detectCollisions = false;
            }
            previewObject.SetActive(true);

            Bounds bounds = CalculateBounds(model);
            float visibleHeight = 2f * PreviewDistance * Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * previewTexture.width / previewTexture.height;
            float rotatingWidth = Mathf.Sqrt(bounds.size.x * bounds.size.x + bounds.size.z * bounds.size.z);
            float widthScale = rotatingWidth > 0.001f ? visibleWidth * PreviewFrameFill / rotatingWidth : float.MaxValue;
            float heightScale = bounds.size.y > 0.001f ? visibleHeight * PreviewFrameFill / bounds.size.y : float.MaxValue;
            model.transform.localScale *= Mathf.Min(widthScale, heightScale);

            bounds = CalculateBounds(model);
            model.transform.position += previewCenter - bounds.center;
            previewZoom = 1f;
        }

        private void ClearPreview()
        {
            if (previewObject != null)
            {
                previewObject.SetActive(false);
                Destroy(previewObject);
            }
            for (int i = 0; i < previewMaterials.Count; i++) Destroy(previewMaterials[i]);
            previewMaterials.Clear();
            previewObject = null;
            previewDefinition = null;
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

            titleText = CreateText("Title", frame, "-  INVENTORY  -", 23f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -39f), new Vector2(360f, 42f));
            leftArrowText = CreateText("LeftArrow", frame, "<", 28f, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(155f, -45f), new Vector2(40f, 40f));
            rightArrowText = CreateText("RightArrow", frame, ">", 28f, TextAlignmentOptions.Center, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-155f, -45f), new Vector2(40f, 40f));

            nameText = CreateText("ItemName", frame, string.Empty, 19f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -112f), new Vector2(160f, 34f));
            descriptionText = CreateText("Description", frame, string.Empty, 18f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -148f), new Vector2(160f, 132f));
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.lineSpacing = 0f;
            descriptionText.enableAutoSizing = true;
            descriptionText.fontSizeMin = 12f;
            descriptionText.fontSizeMax = 18f;

            previewImage = CreateRawImage("Preview", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 235f));
            previewTexture = new RenderTexture(320, 256, 16, RenderTextureFormat.ARGB32) { name = "InventoryPreviewRT", filterMode = FilterMode.Point };
            previewTexture.Create();
            previewImage.texture = previewTexture;

            emptyText = CreateText("Empty", frame, "INVENTORY IS EMPTY", 20f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(400f, 50f));
            feedbackText = CreateText("Feedback", frame, string.Empty, 14f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(592f, 24f));
            actionText = CreateText("Actions", frame, "EXIT: Z", 13f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(608f, 42f));

            BuildPreviewStage();
        }

        private void BuildPreviewStage()
        {
            GameObject cameraObject = new GameObject("InventoryPreviewCamera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = new Vector3(0f, -1000f, 0f);
            cameraObject.transform.rotation = Quaternion.identity;
            previewCamera = cameraObject.GetComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            previewCamera.cullingMask = 1 << PreviewLayer;
            previewCamera.fieldOfView = 37f;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 10f;
            previewCamera.targetTexture = previewTexture;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = false;

            CreatePreviewLight("Key", new Vector3(-1f, -999f, 0.2f), new Vector3(32f, -28f, 0f), 1.25f);
            CreatePreviewLight("Fill", new Vector3(1f, -999f, 1f), new Vector3(48f, 145f, 0f), 0.38f);
        }

        private void CreatePreviewLight(string lightName, Vector3 position, Vector3 rotation, float intensity)
        {
            GameObject lightObject = new GameObject("Inventory" + lightName, typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.position = position;
            lightObject.transform.rotation = Quaternion.Euler(rotation);
            lightObject.layer = PreviewLayer;
            Light lightComponent = lightObject.GetComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = intensity;
            lightComponent.color = lightName == "Key" ? new Color(0.78f, 0.82f, 0.7f) : new Color(0.34f, 0.39f, 0.48f);
            lightComponent.cullingMask = 1 << PreviewLayer;
        }

        private TextMeshProUGUI CreateText(string objectName, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
        {
            RectTransform rect = CreateRect(objectName, parent, anchorMin, anchorMax, position, dimensions);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = size;
            text.color = new Color(0.88f, 0.91f, 0.9f, 1f);
            text.alignment = alignment;
            text.raycastTarget = false;
            text.fontStyle = FontStyles.Bold;
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

        private static Bounds CalculateBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.one);
            Bounds result = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
            return result;
        }

        private void PreparePreviewMaterials(GameObject target, Texture2D textureOverride)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return;

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Material[] source = renderers[rendererIndex].sharedMaterials;
                Material[] replacements = new Material[source.Length];
                for (int materialIndex = 0; materialIndex < source.Length; materialIndex++)
                {
                    Material replacement = new Material(shader) { name = "InventoryPreviewMaterial" };
                    Material original = source[materialIndex];
                    if (original != null)
                    {
                        Texture texture = textureOverride;
                        if (texture == null && original.HasProperty("_BaseMap")) texture = original.GetTexture("_BaseMap");
                        if (texture == null && original.HasProperty("_MainTex")) texture = original.GetTexture("_MainTex");
                        if (texture != null) replacement.SetTexture("_BaseMap", texture);
                        Color tint = original.HasProperty("_BaseColor")
                            ? original.GetColor("_BaseColor")
                            : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
                        replacement.SetColor("_BaseColor", Color.Lerp(tint, Color.white, 0.18f));
                    }
                    replacements[materialIndex] = replacement;
                    previewMaterials.Add(replacement);
                }
                renderers[rendererIndex].sharedMaterials = replacements;
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }

        private void SetVisible(bool value)
        {
            if (canvas != null) canvas.enabled = value;
            if (previewCamera != null) previewCamera.enabled = value;
        }
    }
}
