using UnityEngine;

namespace KnocksFromBeneath
{
    // C-01: a place where the tripod can stand. Put an empty object on the floor: position is the
    // camera mount point, the Y rotation is the direction the camera looks.
    // Without any CameraSlot in the scene CameraPlacementController uses its built-in defaults.
    public sealed class CameraSlot : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.45f, 0.9f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.05f, 0.35f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.05f, transform.forward * 0.8f);
        }
    }
}
