using UnityEngine;

namespace KnocksFromBeneath
{
    // Префаб Resources/Basement/BasementDepths (собирает BasementDepthsBuilder): вентиляционная решётка в стене подвала, замурованный проём,
    // квадратная труба и тоннели. В сцене не лежит, появляется при первом обращении; координаты префаба мировые.
    public sealed class BasementDepths : MonoBehaviour
    {
        private const string PrefabPath = "Basement/BasementDepths";
        public const float GrilleSwing = -115f;

        public Transform grillePivot;
        public BasementCellLock vent;
        public BasementCellLock sealedWall;
        public BasementCellLock tapeCamera;
        public BasementCellLock ductExit;
        public BasementCellLock ladder;
        public Transform tapeView;
        public GameObject duct;
        public GameObject tunnels;

        private static BasementDepths instance;

        public static BasementDepths Get()
        {
            if (instance != null) return instance;
            var prefab = Resources.Load<BasementDepths>(PrefabPath);
            if (prefab == null) { Debug.LogError("[Подвал] Нет префаба Resources/" + PrefabPath + "."); return null; }
            instance = Instantiate(prefab);
            instance.name = prefab.name;
            instance.SetTunnelsActive(false);
            instance.SetDuctActive(false);
            instance.tapeCamera.gameObject.SetActive(false);
            return instance;
        }

        public void SetTunnelsActive(bool active)
        {
            if (tunnels != null) tunnels.SetActive(active);
        }

        public void SetDuctActive(bool active)
        {
            if (duct != null) duct.SetActive(active);
        }

        // Он вылез, решётка осталась распахнутой.
        public void OpenGrille()
        {
            if (grillePivot != null) grillePivot.localRotation = Quaternion.AngleAxis(GrilleSwing, Vector3.up) * grillePivot.localRotation;
        }
    }
}
