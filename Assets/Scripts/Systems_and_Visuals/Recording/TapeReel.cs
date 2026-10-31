using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{
    // Показ плёнки целиком без пульта: VHS на весь экран поверх всего, до конца записи и снега.
    public static class TapeReel
    {
        private static readonly int StaticId = Shader.PropertyToID("_Static");
        private static readonly int TrackingId = Shader.PropertyToID("_Tracking");
        private const int SortingOrder = short.MaxValue; // над затемнением главы и подсказками HUD

        public static IEnumerator Play(NightRecording recording, Transform parent)
        {
            var canvasObject = new GameObject("Tape Reel", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            UiStyle.Fill(canvasObject.transform, "Black", Color.black).raycastTarget = false;

            var picture = new GameObject("Tape Picture", typeof(RectTransform));
            picture.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)picture.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var fitter = picture.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)NightRecording.Width / NightRecording.Height;
            var screen = picture.AddComponent<RawImage>();
            screen.raycastTarget = false;
            screen.texture = recording.Texture;
            Shader shader = Resources.Load<Shader>("Shaders/VhsTape");
            Material material = shader != null ? new Material(shader) : null;
            screen.material = material;

            recording.Playing = true;
            while (!recording.Finished)
            {
                if (material != null)
                {
                    material.SetFloat(StaticId, recording.SignalLoss);
                    material.SetFloat(TrackingId, recording.Tracking);
                }
                yield return null;
            }
            if (material != null) material.SetFloat(StaticId, 1f);
            yield return new WaitForSeconds(0.8f);

            if (material != null) Object.Destroy(material);
            Object.Destroy(canvasObject);
        }
    }
}
