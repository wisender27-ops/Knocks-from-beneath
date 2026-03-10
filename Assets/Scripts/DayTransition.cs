using UnityEngine;
using System.Collections;

public class DayTransition : MonoBehaviour
{
    public SkyboxSwitcher skySwitcher;
    public CanvasGroup fadeScreen;
    public GameObject messageText;

    // Этот метод мы привяжем к квесту в инспекторе
    public void StartNightSequence()
    {
        StartCoroutine(NightRoutine());
    }

    IEnumerator NightRoutine()
    {
        // 1. Fade Out
        float elapsed = 0;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        // 2. Логика смены мира
        messageText.SetActive(true);
        if (skySwitcher != null) skySwitcher.isDayTime = false; 

        yield return new WaitForSeconds(3f);

        // 3. Fade In
        messageText.SetActive(false);
        while (elapsed > 0)
        {
            elapsed -= Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }
    }
}