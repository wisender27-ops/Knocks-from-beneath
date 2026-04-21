using UnityEngine;
using System.Collections;

public class FogController : MonoBehaviour
{
    public static FogController Instance;

    private Coroutine currentCoroutine;

    private void Awake()
    {
        Instance = this;
    }

    public void SetFog(float targetDensity, float duration)
    {
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        currentCoroutine = StartCoroutine(LerpFog(targetDensity, duration));
    }

    private IEnumerator LerpFog(float target, float duration)
    {
        float start = RenderSettings.fogDensity;
        float time = 0f;

        while (time < duration)
        {
            RenderSettings.fogDensity = Mathf.Lerp(start, target, time / duration);
            time += Time.deltaTime;
            yield return null;
        }

        RenderSettings.fogDensity = target;
    }
}