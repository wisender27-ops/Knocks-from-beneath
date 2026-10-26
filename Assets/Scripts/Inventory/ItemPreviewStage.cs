using System.Collections.Generic;
using UnityEngine;

namespace KnocksFromBeneath
{
    // Сцена 3D-превью предмета в RenderTexture: своя камера на слое превью далеко под картой,
    // модель вписывается в кадр и рисуется unlit-материалами. Общая для инвентаря и «Осмотра» (M-05);
    // у каждого экрана свой экземпляр на своей высоте, чтобы камеры не видели чужие модели.
    public sealed class ItemPreviewStage
    {
        public const int PreviewLayer = 30;
        private const float PreviewDistance = 2.6f;
        private const float PreviewFrameFill = 0.76f;

        private readonly Transform owner;
        private readonly Vector3 center;
        private readonly List<Material> materials = new List<Material>(4);

        public Camera Camera { get; private set; }
        public RenderTexture Texture { get; private set; }
        public GameObject Root { get; private set; }
        public GameObject Model { get; private set; }
        public float Zoom { get; private set; } = 1f;

        public ItemPreviewStage(Transform owner, string name, float height, int width, int textureHeight)
        {
            this.owner = owner;
            center = new Vector3(0f, height, PreviewDistance);
            Texture = new RenderTexture(width, textureHeight, 16, RenderTextureFormat.ARGB32) { name = name + "RT", filterMode = FilterMode.Point };
            Texture.Create();

            GameObject cameraObject = new GameObject(name + "Camera", typeof(Camera));
            cameraObject.transform.SetParent(owner, false);
            cameraObject.transform.position = new Vector3(0f, height, 0f);
            cameraObject.transform.rotation = Quaternion.identity;
            Camera = cameraObject.GetComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            Camera.cullingMask = 1 << PreviewLayer;
            Camera.fieldOfView = 37f;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 10f;
            Camera.targetTexture = Texture;
            Camera.allowHDR = false;
            Camera.allowMSAA = false;

            CreateLight(name + "Key", new Vector3(-1f, height + 1f, 0.2f), new Vector3(32f, -28f, 0f), 1.25f, new Color(0.78f, 0.82f, 0.7f));
            CreateLight(name + "Fill", new Vector3(1f, height + 1f, 1f), new Vector3(48f, 145f, 0f), 0.38f, new Color(0.34f, 0.39f, 0.48f));
        }

        public Vector3 Center => center;
        public bool Visible { set { if (Camera != null) Camera.enabled = value; } }

        // Пустой неактивный корень: вызывающий кладёт в него модель, затем Present.
        public Transform Begin(string name)
        {
            Clear();
            Root = new GameObject(name);
            Root.SetActive(false);
            Root.transform.SetParent(owner, false);
            Root.transform.SetPositionAndRotation(center, Quaternion.identity);
            return Root.transform;
        }

        // Модель уже лежит в Root: слой, unlit-материалы, без физики, вписать в кадр.
        public void Present(GameObject model, Texture2D textureOverride)
        {
            Model = model;
            model.SetActive(true);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(12f, 25f, 0f));
            SetLayerRecursively(Root.transform, PreviewLayer);
            PrepareMaterials(model, textureOverride);

            Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            Rigidbody[] bodies = model.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
                bodies[i].detectCollisions = false;
            }
            Root.SetActive(true);

            Bounds bounds = CalculateBounds(model);
            float visibleHeight = 2f * PreviewDistance * Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float visibleWidth = visibleHeight * Texture.width / Texture.height;
            float rotatingWidth = Mathf.Sqrt(bounds.size.x * bounds.size.x + bounds.size.z * bounds.size.z);
            float widthScale = rotatingWidth > 0.001f ? visibleWidth * PreviewFrameFill / rotatingWidth : float.MaxValue;
            float heightScale = bounds.size.y > 0.001f ? visibleHeight * PreviewFrameFill / bounds.size.y : float.MaxValue;
            model.transform.localScale *= Mathf.Min(widthScale, heightScale);

            bounds = CalculateBounds(model);
            model.transform.position += center - bounds.center;
            Zoom = 1f;
        }

        public void Rotate(float yaw, float pitch)
        {
            if (Root == null) return;
            Root.transform.Rotate(Vector3.up, yaw, Space.World);
            Root.transform.Rotate(Vector3.right, pitch, Space.World);
        }

        public void SetZoom(float value)
        {
            Zoom = value;
            if (Root != null) Root.transform.localScale = Vector3.one * value;
        }

        public void ResetView()
        {
            if (Root != null) Root.transform.rotation = Quaternion.identity;
            SetZoom(1f);
        }

        public void Clear()
        {
            if (Root != null)
            {
                Root.SetActive(false);
                Object.Destroy(Root);
            }
            for (int i = 0; i < materials.Count; i++) Object.Destroy(materials[i]);
            materials.Clear();
            Root = null;
            Model = null;
        }

        public void Dispose()
        {
            Clear();
            if (Texture != null)
            {
                Texture.Release();
                Object.Destroy(Texture);
                Texture = null;
            }
        }

        public static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++) SetLayerRecursively(root.GetChild(i), layer);
        }

        private void CreateLight(string lightName, Vector3 position, Vector3 rotation, float intensity, Color color)
        {
            GameObject lightObject = new GameObject(lightName, typeof(Light));
            lightObject.transform.SetParent(owner, false);
            lightObject.transform.position = position;
            lightObject.transform.rotation = Quaternion.Euler(rotation);
            lightObject.layer = PreviewLayer;
            Light lightComponent = lightObject.GetComponent<Light>();
            lightComponent.type = LightType.Directional;
            lightComponent.intensity = intensity;
            lightComponent.color = color;
            lightComponent.cullingMask = 1 << PreviewLayer;
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.one);
            Bounds result = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
            return result;
        }

        private void PrepareMaterials(GameObject target, Texture2D textureOverride)
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
                    materials.Add(replacement);
                }
                renderers[rendererIndex].sharedMaterials = replacements;
            }
        }
    }
}
