using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{

// Плита для ужина дня 2 (T-15). Проще микроволновки: продукты уже абстрактный
// квест-прогресс (IngredientItem.Collect(), как мусор), не физический предмет,
// который несут и кладут внутрь — поэтому тут нет стадии "положить/закрыть дверцу".
// Готовка доступна только пока IntroSequence.CanCookDinner() (== DinnerCookingController
// собрал все ингредиенты и ужин ещё не съеден) — тот же паттерн тонкого проброса,
// что MicrowaveInteractable использует для CanPlacePieInMicrowave/OnPiePlacedInMicrowave.
public class StoveInteractable : MonoBehaviour
{
    [Header("Готовка")]
    public GameObject readyMealObject; // тарелка с готовым ужином — появляется по завершении готовки
    public float cookDuration = 10f;

    [Header("Звук")]
    public AudioSource stoveAudioSource;
    public AudioClip cookStartSfx;
    public AudioClip cookDoneSfx;

    private bool _isCooking;
    private bool _isReady;

    void Start()
    {
        if (readyMealObject != null) readyMealObject.SetActive(false);
    }

    public void Interact()
    {
        var intro = FindFirstObjectByType<IntroSequence>();
        if (intro == null) return;

        if (_isReady)
        {
            if (readyMealObject != null) readyMealObject.SetActive(false);
            _isReady = false;
            intro.OnDinnerEaten();
            return;
        }

        if (_isCooking) return;
        if (!intro.CanCookDinner()) return;

        StartCoroutine(CookRoutine());
    }

    IEnumerator CookRoutine()
    {
        _isCooking = true;

        if (stoveAudioSource != null && cookStartSfx != null)
            stoveAudioSource.PlayOneShot(cookStartSfx);

        yield return new WaitForSeconds(cookDuration);

        if (stoveAudioSource != null && cookDoneSfx != null)
            stoveAudioSource.PlayOneShot(cookDoneSfx);

        if (readyMealObject != null) readyMealObject.SetActive(true);

        _isCooking = false;
        _isReady = true;
    }
}
}
