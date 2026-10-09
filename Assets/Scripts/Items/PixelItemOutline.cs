using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class PixelItemOutline : MonoBehaviour
    {
        public void Show() => QuestItemMarker.Ensure(gameObject).enabled = true;
        public void Hide()
        {
            var marker = GetComponent<QuestItemMarker>();
            if (marker != null) marker.enabled = false;
        }
    }
}
