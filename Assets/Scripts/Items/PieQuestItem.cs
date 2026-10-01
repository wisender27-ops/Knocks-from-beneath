using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{
    public class PieQuestItem : MonoBehaviour
    {
        [Header("Состояние пирога")]
        public bool isHeated = false;

        [Header("Поедание")]
        public AudioClip eatSfx;
        public float eatDuration = 5f;
        [Min(1)] public int biteCount = 4;

        [Header("Поза в руках")]
        public Vector3 heldLocalPosition = new Vector3(0.02f, -0.085f, 0.20f);
        public Vector3 heldLocalEulerAngles = new Vector3(-90f, 0f, 0f);
        [Range(0.1f, 1.5f)] public float heldScale = 1.25f;
        [Min(0f)] public float idleSway = 0.004f;

        [Header("Визуальные куски")]
        [Range(10f, 60f)] public float biteWedgeAngle = 34f;
        [Range(0.25f, 1f)] public float biteWedgeRadius = 0.92f;

        private Transform _viewAnchor;
        private bool _isEating;
        private Vector3 _eatingLocalPosition;
        private Quaternion _eatingLocalRotation;
        private GameObject[] _biteChunks;
        public Mesh[] bitePortionMeshes;
        public Material cutSurfaceMaterial;
        public int EatingBiteCount => bitePortionMeshes != null && bitePortionMeshes.Length > 0
            ? bitePortionMeshes.Length : Mathf.Max(1, biteCount);

        private void LateUpdate()
        {
            if (_viewAnchor == null || _isEating) return;
            float phase = Time.time * 1.7f;
            transform.localPosition = heldLocalPosition + Vector3.up * (Mathf.Sin(phase) * idleSway);
            transform.localRotation = Quaternion.Euler(heldLocalEulerAngles) * Quaternion.Euler(0f, 0f, Mathf.Sin(phase * 0.5f) * 1.2f);
        }

        public void AttachToView(Transform anchor)
        {
            _viewAnchor = anchor;
            _isEating = false;
            transform.SetParent(anchor, false);
            transform.localPosition = heldLocalPosition;
            transform.localRotation = Quaternion.Euler(heldLocalEulerAngles);
        }

        public void BeginEatingView()
        {
            _isEating = true;
            _eatingLocalPosition = transform.localPosition;
            _eatingLocalRotation = transform.localRotation;
            BuildBiteVisuals();
        }

        public void SetEatingView(float forward, float biteDip)
        {
            Vector3 mouthOffset = new Vector3(0.01f, -0.045f, 0.18f) - _eatingLocalPosition;
            Vector3 biteStroke = new Vector3(-0.005f, 0.008f, -0.012f);
            transform.localPosition = _eatingLocalPosition + mouthOffset * forward + biteStroke * biteDip;
            transform.localRotation = _eatingLocalRotation * Quaternion.Euler(-12f * forward, 8f * forward, -10f * forward);
        }

        public IEnumerator ConsumeBite(int biteIndex, Camera playerCamera, float duration)
        {
            if (_biteChunks == null || biteIndex < 0 || biteIndex >= _biteChunks.Length) yield break;
            GameObject chunk = _biteChunks[biteIndex];
            if (chunk == null) yield break;


            Transform chunkTransform = chunk.transform;
            chunkTransform.SetParent(null, true);
            Vector3 startPosition = chunkTransform.position;
            Vector3 startScale = chunkTransform.lossyScale;
            Vector3 targetPosition = playerCamera != null
                ? playerCamera.transform.position + playerCamera.transform.forward * 0.065f + playerCamera.transform.up * -0.045f
                : startPosition + Vector3.forward * 0.3f;
            float elapsed = 0f;
            while (elapsed < duration && chunk != null)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                chunkTransform.position = Vector3.Lerp(startPosition, targetPosition, eased);
                chunkTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
                chunkTransform.Rotate(140f * Time.deltaTime, 90f * Time.deltaTime, 0f, Space.Self);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (chunk != null) Destroy(chunk);
            _biteChunks[biteIndex] = null;
        }

        public void DetachFromView()
        {
            _isEating = false;
            _viewAnchor = null;
        }

        private void BuildBiteVisuals()
        {
            if (_biteChunks != null) return;
            MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
            if (sourceRenderer == null || bitePortionMeshes == null || bitePortionMeshes.Length == 0) return;
            for (int i = 0; i < bitePortionMeshes.Length; i++)
                if (bitePortionMeshes[i] == null) return;

            Material[] sourceMaterials = sourceRenderer.sharedMaterials;
            Material[] materials = new Material[sourceMaterials.Length + 1];
            System.Array.Copy(sourceMaterials, materials, sourceMaterials.Length);
            materials[materials.Length - 1] = cutSurfaceMaterial != null
                ? cutSurfaceMaterial : sourceRenderer.sharedMaterial;
            _biteChunks = new GameObject[bitePortionMeshes.Length];
            for (int i = 0; i < _biteChunks.Length; i++)
            {
                GameObject portion = new GameObject($"Pie portion {i + 1}");
                portion.layer = gameObject.layer;
                portion.transform.SetParent(transform, false);
                portion.AddComponent<MeshFilter>().sharedMesh = bitePortionMeshes[i];
                MeshRenderer renderer = portion.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                renderer.receiveShadows = sourceRenderer.receiveShadows;
                _biteChunks[i] = portion;
            }
            sourceRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_biteChunks == null) return;
            for (int i = 0; i < _biteChunks.Length; i++)
                if (_biteChunks[i] != null && _biteChunks[i].transform.parent != transform)
                    Destroy(_biteChunks[i]);
        }
    }
}
