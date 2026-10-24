using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [DefaultExecutionOrder(32000)]
    public sealed class FadeTransitionPresentation : MonoBehaviour
    {
        public static bool IsActive { get; private set; }
        private readonly Dictionary<Graphic, bool> graphics = new Dictionary<Graphic, bool>();
        private readonly HashSet<Graphic> allowed = new HashSet<Graphic>();
        private readonly List<Graphic> cachedGraphics = new List<Graphic>();
        private CanvasGroup fade;
        private float previousVolume;
        private int lastScanFrame = -1;
        private bool suppressing, previousBlocksRaycasts;

        public static FadeTransitionPresentation Ensure(CanvasGroup screen)
        {
            if (screen == null) return null;
            var guard = screen.GetComponent<FadeTransitionPresentation>();
            return guard != null ? guard : screen.gameObject.AddComponent<FadeTransitionPresentation>();
        }

        public void Allow(Graphic graphic)
        {
            if (graphic == null) return;
            allowed.Add(graphic);
            if (graphics.TryGetValue(graphic, out bool wasEnabled))
            {
                graphic.enabled = wasEnabled;
                graphics.Remove(graphic);
                cachedGraphics.Remove(graphic);
            }
        }

        private void Awake()
        {
            fade = GetComponent<CanvasGroup>();
            Allow(GetComponent<Graphic>());
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;
        }

        private void OnEnable() => Canvas.willRenderCanvases += PrepareToRender;
        private void LateUpdate() => RefreshPresentation();

        private void PrepareToRender()
        {
            lastScanFrame = -1;
            RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            if (fade == null || fade.alpha <= 0f)
            {
                Restore();
                return;
            }

            if (!suppressing)
            {
                previousVolume = AudioListener.volume;
                previousBlocksRaycasts = fade.blocksRaycasts;
                suppressing = true;
                IsActive = true;
                lastScanFrame = -1;
            }

            // Сохраняем изменение настройки громкости, сделанное во время перехода.
            if (AudioListener.volume > 0f) previousVolume = AudioListener.volume;
            AudioListener.volume = 0f;
            fade.blocksRaycasts = true;

            if (lastScanFrame != Time.frameCount)
            {
                foreach (var graphic in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (allowed.Contains(graphic) || graphics.ContainsKey(graphic)) continue;
                    graphics.Add(graphic, graphic.enabled);
                    cachedGraphics.Add(graphic);
                }
                lastScanFrame = Time.frameCount;
            }

            // Не отключаем UI GameObject: на нём может идти сюжетная корутина.
            foreach (var graphic in cachedGraphics)
                if (graphic != null) graphic.enabled = false;
        }

        private void Restore()
        {
            if (!suppressing) return;
            suppressing = false;
            IsActive = false;
            AudioListener.volume = previousVolume;
            if (fade != null) fade.blocksRaycasts = previousBlocksRaycasts;
            foreach (var pair in graphics)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            graphics.Clear();
            cachedGraphics.Clear();
        }

        private void OnDisable()
        {
            Canvas.willRenderCanvases -= PrepareToRender;
            Restore();
        }
    }
}
