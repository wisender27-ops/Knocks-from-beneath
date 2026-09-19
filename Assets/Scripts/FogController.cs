using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{

public class FogController : MonoBehaviour
{
    public static FogController Instance;

    private Coroutine currentCoroutine;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void SetFog(float targetDensity, float duration)
    {
        if (currentCoroutine != null)
            StopCoroutine(currentCoroutine);

        if (duration <= 0f)
        {
            RenderSettings.fogDensity = targetDensity;
            currentCoroutine = null;
            return;
        }

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
        currentCoroutine = null;
    }
}
}
