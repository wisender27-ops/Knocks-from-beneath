using RetroPSX;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Rendering/RetroPSX Camera Settings")]
public sealed class RetroPSXCameraSettings : MonoBehaviour
{
    [SerializeField] private RetroPSXPipelineProfile profile;

    public RetroPSXPipelineProfile Profile => profile;
}
