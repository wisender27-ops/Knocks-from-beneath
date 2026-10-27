using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    public sealed class DeathTapeEffect : MonoBehaviour
    {
        private const float Duration = 1.1f;
        private static DeathTapeEffect active;
        private static readonly int Jitter = Shader.PropertyToID("_ScanLineJitter");
        private static readonly int Jump = Shader.PropertyToID("_VerticalJump");
        private static readonly int Shake = Shader.PropertyToID("_HorizontalShake");
        private static readonly int Drift = Shader.PropertyToID("_ColorDrift");
        private static readonly int GlitchTime = Shader.PropertyToID("_GlitchTime");

        public static bool IsActive => active != null;
        public static bool FrameCaptured => active != null && active.captured;
        public static int RenderedFrames => active != null ? active.renderedFrames : 0;

        private RenderTexture frame, output;
        private Material material;
        private AudioSource staticSource;
        private GameObject overlay;
        private CanvasGroup endingFade;
        private Graphic[] graphics;
        private bool[] graphicStates;
        private float previousTimeScale;
        private bool previousAudioPause, ownsState, captured;
        private int renderedFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => active = null;

        public static void Cancel()
        {
            if (active == null) return;
            var effect = active;
            effect.StopAllCoroutines();
            effect.Cleanup();
            Destroy(effect.gameObject);
        }

        public static IEnumerator Play(CanvasGroup endingFade = null)
        {
            if (IsActive) yield break;
            var root = new GameObject("Death Tape Effect");
            var effect = root.AddComponent<DeathTapeEffect>();
            effect.endingFade = endingFade;
            active = effect;
            try
            {
                effect.StartCoroutine(effect.RunOwned());
                while (effect != null && active == effect) yield return null;
            }
            finally
            {
                if (effect != null) effect.Cleanup();
                if (root != null) Destroy(root);
            }
        }

        private IEnumerator RunOwned()
        {
            // Свой таймер завершает эффект, даже если вызывающая сюжетная корутина была остановлена.
            try { yield return Run(); }
            finally
            {
                Cleanup();
                Destroy(gameObject);
            }
        }

        private IEnumerator Run()
        {
            Shader shader = Resources.Load<Shader>("DeathTape/AnalogGlitch");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError("Death Tape: KinoGlitch shader is missing or unsupported.");
                yield break;
            }

            previousTimeScale = Time.timeScale;
            previousAudioPause = AudioListener.pause;
            ownsState = true;
            graphics = FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            graphicStates = new bool[graphics.Length];
            for (int i = 0; i < graphics.Length; i++)
            {
                graphicStates[i] = graphics[i].enabled;
                graphics[i].enabled = false;
            }

            // Захватываем сцену без HUD до создания собственного экранного слоя.
            yield return new WaitForEndOfFrame();
            frame = CreateBuffer("Death Tape Frozen Frame");
            output = CreateBuffer("Death Tape Output");
            ScreenCapture.CaptureScreenshotIntoRenderTexture(frame);
            captured = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var staticClip = Resources.Load<AudioClip>("DeathTape/Static");
            if (staticClip != null)
            {
                staticSource = gameObject.AddComponent<AudioSource>();
                AudioRouting.Assign(staticSource, AudioBus.Horror);
                staticSource.playOnAwake = false;
                staticSource.ignoreListenerPause = true;
                staticSource.spatialBlend = 0f;
                staticSource.clip = staticClip;
                staticSource.volume = 0f;
                staticSource.Play();
            }

            overlay = new GameObject("Death Tape Screen", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32750;
            var imageRoot = new GameObject("Frozen Image", typeof(RectTransform), typeof(RawImage));
            imageRoot.transform.SetParent(overlay.transform, false);
            var image = imageRoot.GetComponent<RawImage>();
            image.texture = output;
            image.raycastTarget = true;
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            for (float elapsed = 0f; elapsed < Duration; elapsed += Time.unscaledDeltaTime)
            {
                float strength = Mathf.Lerp(0.45f, 1f, elapsed / Duration);
                float clock = Time.unscaledTime;
                if (staticSource != null)
                    staticSource.volume = 0.35f * Mathf.Clamp01(elapsed / 0.03f) * Mathf.Clamp01((Duration - elapsed) / 0.15f);
                material.SetVector(Jitter, new Vector4(0.018f + 0.075f * strength, 0.72f - 0.35f * strength, 0f, 0f));
                material.SetVector(Jump, new Vector4(0.12f * strength, clock * 0.32f, 0f, 0f));
                material.SetFloat(Shake, 0.035f * strength);
                material.SetVector(Drift, new Vector4(0.025f * strength, clock * 9f, 0f, 0f));
                material.SetFloat(GlitchTime, clock);
                Graphics.Blit(frame, output, material);
                renderedFrames++;
                yield return null;
            }
            if (endingFade != null) endingFade.alpha = 1f;
        }

        private static RenderTexture CreateBuffer(string bufferName)
        {
            var buffer = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                name = bufferName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            buffer.Create();
            return buffer;
        }

        private void Cleanup()
        {
            if (overlay != null) overlay.SetActive(false);
            if (staticSource != null) staticSource.Stop();
            if (ownsState)
            {
                ownsState = false;
                Time.timeScale = previousTimeScale;
                AudioListener.pause = previousAudioPause;
                if (graphics != null)
                    for (int i = 0; i < graphics.Length; i++)
                        if (graphics[i] != null) graphics[i].enabled = graphicStates[i];
            }
            graphics = null;
            graphicStates = null;
            Release(ref frame);
            Release(ref output);
            if (material != null) Destroy(material);
            material = null;
            if (active == this) active = null;
        }

        private static void Release(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private void OnDisable() => Cleanup();
        private void OnDestroy() => Cleanup();
    }
}
