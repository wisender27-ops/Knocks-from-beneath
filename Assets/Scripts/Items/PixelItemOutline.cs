using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace KnocksFromBeneath
{
    public sealed class PixelItemOutline : MonoBehaviour
    {
        private readonly List<GameObject> _shells = new List<GameObject>();
        private Material _material;

        public void Show()
        {
            if (_material == null)
            {
                Shader shader = Resources.Load<Shader>("Shaders/PixelItemOutline");
                if (shader == null) { Debug.LogError("Pixel outline shader missing", this); return; }
                _material = new Material(shader);
                foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || !filter.TryGetComponent<MeshRenderer>(out var original)) continue;
                    GameObject shell = new GameObject("PixelOutline");
                    shell.layer = filter.gameObject.layer;
                    shell.transform.SetParent(filter.transform, false);
                    shell.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    var renderer = shell.AddComponent<MeshRenderer>();
                    var materials = new Material[filter.sharedMesh.subMeshCount];
                    for (int i = 0; i < materials.Length; i++) materials[i] = _material;
                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    _shells.Add(shell);
                }
            }
            foreach (GameObject shell in _shells) if (shell != null) shell.SetActive(true);
        }

        public void Hide()
        {
            foreach (GameObject shell in _shells) if (shell != null) shell.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
