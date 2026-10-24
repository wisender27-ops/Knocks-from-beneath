using UnityEngine;

namespace KnocksFromBeneath
{
    public class ItemGlow : MonoBehaviour
    {
        public float visibleDistance = 4f;
        public float fadeDistance = 2f;

        private void Awake()
        {
            QuestItemMarker.SuppressLegacyParticles(transform);
            QuestItemMarker.Ensure(gameObject).VisibleDistance = visibleDistance;
            enabled = false;
        }
    }
}
