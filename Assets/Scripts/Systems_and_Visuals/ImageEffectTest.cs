using UnityEngine;

[ExecuteInEditMode]
public class ImageEffectTest : MonoBehaviour
{
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        Graphics.Blit(src, dest);
        Debug.Log("OnRenderImage called");
    }
}
