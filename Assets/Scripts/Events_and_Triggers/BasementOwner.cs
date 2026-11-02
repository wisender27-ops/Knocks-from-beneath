using UnityEngine;

namespace KnocksFromBeneath
{
    public sealed class BasementOwner : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer head;
        [SerializeField] private Material revealedMaterial;

        public void Configure(SkinnedMeshRenderer headRenderer, Material faceMaterial)
        {
            head = headRenderer;
            revealedMaterial = faceMaterial;
        }

        // Лицо: центр головы, а если модель не настроена — примерно на высоте сидящего человека.
        public Vector3 FacePoint => head != null ? head.bounds.center : transform.position + Vector3.up * 0.8f;

        public void RevealFace()
        {
            if (head != null && revealedMaterial != null) head.sharedMaterial = revealedMaterial;
        }
    }
}
