using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace KnocksFromBeneath
{
    /// <summary>
    /// Подземная геометрия (подвал) не запечена в лайтмап, а запечённых проб под
    /// землёй нет. Без этого компонента такие рендереры берут рассеянный свет из
    /// глобального ambient — днём это небо, и подвал подсвечивается синевой снаружи.
    /// Компонент подставляет им собственный постоянный ambient (по умолчанию почти
    /// чёрный), так что подвал освещают только его лампы и фонарик игрока —
    /// одинаково днём и ночью, без перезапекания сцены.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class UndergroundAmbientOverride : MonoBehaviour
    {
        [Tooltip("Рассеянный свет подземных объектов вместо неба. Совпадает с ночным ambient SkyboxSwitcher.")]
        [SerializeField] private Color ambient = new Color(0.008f, 0.01f, 0.018f);

        [Tooltip("Рендереры под землёй. Наземные части люка сюда не входят — им нужен обычный ambient.")]
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        private MaterialPropertyBlock _block;

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        private void OnDisable()
        {
            if (_block == null) return;
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null);
                r.lightProbeUsage = LightProbeUsage.Off;
            }
        }

        public void Apply()
        {
            if (_block == null) _block = new MaterialPropertyBlock();
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambient.linear);
            SphericalHarmonicsL2[] shArray = { sh };
            Vector4[] occlusion = { Vector4.one };

            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.lightProbeUsage = LightProbeUsage.CustomProvided;
                r.GetPropertyBlock(_block);
                _block.CopySHCoefficientArraysFrom(shArray);
                _block.CopyProbeOcclusionArrayFrom(occlusion);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
