using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KnocksFromBeneath
{

// Карточка концовки (T-21) — общая для всех четырёх исходов ночи 2 (побег/укрытие/молоток/
// таймер). Раньше каждая концовка заканчивалась своим Application.Quit() сразу после последней
// реплики — в билде игра просто закрывалась, без какого-либо "конца". Переиспользует
// Canvas (UI)/FadeScreen — тот же CanvasGroup, что и у дневной/ночной переходной заливки
// (IntroSequence.fadeScreen, FinaleController.fadeScreen), поэтому если экран уже чёрный
// (концовка молотка сама его затемняет перед этим) — доигрывает без рывка.
public class EndingScreen : MonoBehaviour
{
    public static EndingScreen Instance;

    [SerializeField] private CanvasGroup fadeScreen;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private float fadeDuration = 1.2f;
    [SerializeField] private float holdDuration = 3.5f;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    void Awake()
    {
        Instance = this;
        if (titleText != null) titleText.text = "";
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShowEnding(string title)
    {
        StartCoroutine(ShowEndingRoutine(title));
    }

    IEnumerator ShowEndingRoutine(string title)
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (fadeScreen != null)
        {
            float start = fadeScreen.alpha;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                fadeScreen.alpha = Mathf.Lerp(start, 1f, t / fadeDuration);
                yield return null;
            }
            fadeScreen.alpha = 1f;
        }

        if (titleText != null)
        {
            titleText.gameObject.SetActive(true);
            titleText.text = title;
        }

        yield return new WaitForSeconds(holdDuration);

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
}
