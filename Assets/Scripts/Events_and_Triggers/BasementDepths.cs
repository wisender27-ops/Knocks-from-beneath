using UnityEngine;

namespace KnocksFromBeneath
{
    // Объект сцены "Basement Depths": вентиляционная решётка в стене подвала, забетонированный проём и точка записи. Труба — рядом, BasementDuctScene.
    public sealed class BasementDepths : MonoBehaviour
    {
        public const float GrilleSwing = -115f;

        public Transform grillePivot;
        public BasementCellLock vent;
        public BasementCellLock sealedWall;
        public BasementCellLock tapeCamera;
        public Transform tapeView;
        public BasementDuctScene duct;

        private static BasementDepths instance;

        public static BasementDepths Get()
        {
            if (instance != null) return instance;
            instance = FindFirstObjectByType<BasementDepths>(FindObjectsInactive.Include);
            if (instance == null) { Debug.LogError("[Подвал] В сцене нет объекта BasementDepths."); return null; }
            instance.tapeCamera.gameObject.SetActive(false);
            return instance;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // Звуки трубы играют, пока герой в ней.
        public void SetDuctActive(bool active)
        {
            if (duct != null) duct.SetAudible(active);
        }

        // Он вылез, решётка осталась распахнутой.
        public void OpenGrille()
        {
            if (grillePivot != null) grillePivot.localRotation = Quaternion.AngleAxis(GrilleSwing, Vector3.up) * grillePivot.localRotation;
        }
    }
}
