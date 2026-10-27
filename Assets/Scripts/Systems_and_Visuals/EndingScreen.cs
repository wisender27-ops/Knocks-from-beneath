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
        FadeTransitionPresentation.Ensure(fadeScreen)?.Allow(titleText);
        Instance = this;
        if (titleText != null) titleText.text = "";
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Концовка открывается в реестре сразу — даже если игрок выйдет до конца карточки.
    // afterCard — вместо выхода в меню (B-07: смерть в «Стакане воды» сразу повторяет ночь 2).
    public void ShowEnding(EndingId ending, System.Action afterCard = null)
    {
        EndingRegistry.MarkSeen(ending);
        StartCoroutine(ShowEndingRoutine(EndingRegistry.ScreenTitle(ending), afterCard));
    }

    IEnumerator ShowEndingRoutine(string title, System.Action afterCard)
    {
        PlayerController.CursorFree = true;
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
            // Концовка оформлена как конец записи на кассете — в общем VHS-стиле интерфейса.
            titleText.text = titleText.richText
                ? "<size=40%><color=" + UiStyle.Hex(UiStyle.Amber) + ">■ СТОП</color></size>\n" + title
                : title;
        }

        yield return new WaitForSeconds(holdDuration);

        if (afterCard != null)
        {
            afterCard();
            yield break;
        }
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
}
