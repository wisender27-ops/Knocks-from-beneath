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
            if (previewTexture != null)
            {
                previewTexture.Release();
                Destroy(previewTexture);
            }
            if (noiseTexture != null) Destroy(noiseTexture);
            for (int i = 0; i < previewMaterials.Count; i++) Destroy(previewMaterials[i]);
            previewMaterials.Clear();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                if (isOpen) Close(); else Open();
                return;
            }

            if (!isOpen) return;

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Z)) Close();
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SelectRelative(-1);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SelectRelative(1);
            else if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) UseSelected();

            if (previewObject != null)
                previewObject.transform.Rotate(Vector3.up, rotationSpeed * Time.unscaledDeltaTime, Space.World);

            group.alpha = 0.97f + Mathf.Sin(Time.unscaledTime * 43f) * 0.025f;
            float noiseOffset = Mathf.Repeat(Time.unscaledTime * 7.3f, 1f);
            noiseImage.uvRect = new Rect(noiseOffset, noiseOffset * 0.61f, 5f, 3f);
        }

        private void Open()
        {
            if (inventory == null || isOpen) return;
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
        }

        private void SelectRelative(int direction)
        {
            int count = inventory.Entries.Count;
            if (count == 0) return;
            selectedIndex = (selectedIndex + direction + count) % count;
            Refresh();
        }

        private void UseSelected()
        {
            if (inventory.UseItemAt(selectedIndex))
            {
                ClampSelection();
                Refresh();
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
                actionText.text = "EXIT: Z";
                ReplacePreview(null);
                return;
            }

            InventoryEntry entry = inventory.Entries[selectedIndex];
            bool equipped = inventory.EquippedItemId == entry.Definition.Id;
            nameText.text = entry.Definition.DisplayName + "  " + entry.Quantity + "/" + entry.Definition.MaxStack;
            descriptionText.text = entry.Definition.Description;
            if (entry.Definition.IsEquippable)
                actionText.text = (equipped ? "UNEQUIP: SPACE" : "EQUIP: SPACE") + "   ||   EXIT: Z";
            else
                actionText.text = "USE: SPACE   ||   EXIT: Z";
            ReplacePreview(entry.Definition);
        }

        private void ReplacePreview(InventoryItemDefinition definition)
        {
            if (previewObject != null) Destroy(previewObject);
            for (int i = 0; i < previewMaterials.Count; i++) Destroy(previewMaterials[i]);
            previewMaterials.Clear();
            previewObject = null;
            if (definition == null || definition.PreviewPrefab == null) return;

            Vector3 previewCenter = new Vector3(0f, -1000f, PreviewDistance);
            previewObject = new GameObject("InventoryPreview_" + definition.Id);
            previewObject.transform.position = previewCenter;
            previewObject.name = "InventoryPreview_" + definition.Id;

            GameObject model = Instantiate(definition.PreviewPrefab, previewObject.transform);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(12f, 25f, 0f));
            SetLayerRecursively(previewObject.transform, PreviewLayer);
            PreparePreviewMaterials(model, definition.PreviewTextureOverride);

            Collider[] colliders = model.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            Rigidbody[] bodies = model.GetComponentsInChildren<Rigidbody>();
            for (int i = 0; i < bodies.Length; i++) bodies[i].isKinematic = true;

            Bounds bounds = CalculateBounds(model);
            float visibleHeight = 2f * PreviewDistance * Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * previewTexture.width / previewTexture.height;
            float rotatingWidth = Mathf.Sqrt(bounds.size.x * bounds.size.x + bounds.size.z * bounds.size.z);
            float widthScale = rotatingWidth > 0.001f ? visibleWidth * PreviewFrameFill / rotatingWidth : float.MaxValue;
            float heightScale = bounds.size.y > 0.001f ? visibleHeight * PreviewFrameFill / bounds.size.y : float.MaxValue;
            model.transform.localScale *= Mathf.Min(widthScale, heightScale);

            bounds = CalculateBounds(model);
            model.transform.position += previewCenter - bounds.center;
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

            CreateText("Title", frame, "-  INVENTORY  -", 23f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -39f), new Vector2(360f, 42f));
            CreateText("LeftArrow", frame, "<", 28f, TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(155f, -77f), new Vector2(40f, 40f));
            CreateText("RightArrow", frame, ">", 28f, TextAlignmentOptions.Center, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-155f, -77f), new Vector2(40f, 40f));

            nameText = CreateText("ItemName", frame, string.Empty, 19f, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -112f), new Vector2(160f, 34f));
            descriptionText = CreateText("Description", frame, string.Empty, 18f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -148f), new Vector2(160f, 132f));
            descriptionText.textWrappingMode = TextWrappingModes.Normal;
            descriptionText.lineSpacing = -8f;

            previewImage = CreateRawImage("Preview", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 235f));
            previewTexture = new RenderTexture(320, 256, 16, RenderTextureFormat.ARGB32) { name = "InventoryPreviewRT", filterMode = FilterMode.Point };
            previewTexture.Create();
            previewImage.texture = previewTexture;

            emptyText = CreateText("Empty", frame, "INVENTORY IS EMPTY", 20f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(400f, 50f));
            actionText = CreateText("Actions", frame, "EXIT: Z", 18f, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(500f, 38f));

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
