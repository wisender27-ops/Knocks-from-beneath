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

        public void RevealFace()
        {
            if (head != null && revealedMaterial != null) head.sharedMaterial = revealedMaterial;
        }
    }
}
