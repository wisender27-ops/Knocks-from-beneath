using System;
using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class NeighborhoodNightLighting : MonoBehaviour
    {
        [Serializable]
        public struct Surface
        {
            public Renderer renderer;
            public int materialIndex;
            public Material dayMaterial;
            public Material nightMaterial;
        }

        [SerializeField] private SkyboxSwitcher sky;
        [SerializeField] private Surface[] surfaces = Array.Empty<Surface>();
        [SerializeField] private Light[] streetLights = Array.Empty<Light>();
        [SerializeField, Min(10f)] private float lightDistance = 65f;
        private Transform viewer;
        private bool isNight;
        private float nextLightCheck;

        private void OnEnable()
        {
            GameEvents.OnNightStarted += TurnOn;
            GameEvents.OnDayStarted += TurnOff;
            if (Camera.main != null) viewer = Camera.main.transform;
            SetNight(sky != null && !sky.isDayTime);
        }

        private void OnDisable()
        {
            GameEvents.OnNightStarted -= TurnOn;
            GameEvents.OnDayStarted -= TurnOff;
            SetNight(false);
        }

        private void TurnOn() => SetNight(true);
        private void TurnOff() => SetNight(false);

        private void Update()
        {
            if (!isNight || Time.unscaledTime < nextLightCheck) return;
            nextLightCheck = Time.unscaledTime + 0.5f;
            UpdateNearbyLights();
        }

        public void SetNight(bool value)
        {
            isNight = value;
            foreach (Surface surface in surfaces)
            {
                if (surface.renderer == null) continue;
                Material[] materials = surface.renderer.sharedMaterials;
                if (surface.materialIndex < 0 || surface.materialIndex >= materials.Length) continue;
                materials[surface.materialIndex] = value ? surface.nightMaterial : surface.dayMaterial;
                surface.renderer.sharedMaterials = materials;
            }
            UpdateNearbyLights();
        }

        private void UpdateNearbyLights()
        {
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            float distanceSquared = lightDistance * lightDistance;
            foreach (Light light in streetLights)
                if (light != null)
                    light.enabled = isNight && (viewer == null ||
                        (light.transform.position - viewer.position).sqrMagnitude <= distanceSquared);
        }
    }
}
