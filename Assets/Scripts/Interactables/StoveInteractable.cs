using UnityEngine;

namespace KnocksFromBeneath
{

public class StoveInteractable : MonoBehaviour
{
    [Header("Готовка")]
    public GameObject readyMealObject; // тарелка с готовым ужином — появляется по завершении готовки
    public GameObject cookingFoodObject;
    public float cookDuration = 10f;

    [Header("Звук")]
    public AudioSource stoveAudioSource;
    public AudioClip cookStartSfx;
    public AudioClip cookDoneSfx;

    private bool _isCooking;
    private bool _isReady;

    public bool IsCooking => _isCooking;
    public bool IsReady => _isReady;

    void Start()
    {
        if (readyMealObject != null && !_isReady) readyMealObject.SetActive(false);
        if (cookingFoodObject != null && !_isCooking) cookingFoodObject.SetActive(false);
    }

    public void Interact()
    {
        var intro = FindFirstObjectByType<IntroSequence>();
        if (intro == null) return;

        if (_isCooking || _isReady) return;
        if (!intro.CanCookDinner()) return;

        var window = GetComponent<DinnerCookingWindow>();
        if (window == null) window = gameObject.AddComponent<DinnerCookingWindow>();
        if (!window.Open(CompleteCooking, CancelCooking)) return;
        _isCooking = true;
        if (cookingFoodObject != null) cookingFoodObject.SetActive(true);
        if (stoveAudioSource != null && cookStartSfx != null)
            stoveAudioSource.PlayOneShot(cookStartSfx);
    }

    void CancelCooking()
    {
        _isCooking = false;
        if (cookingFoodObject != null) cookingFoodObject.SetActive(false);
    }

    void OnDisable()
    {
        GetComponent<DinnerCookingWindow>()?.ResetAttempt();
        StopAllCoroutines();
        _isCooking = false;
        if (cookingFoodObject != null) cookingFoodObject.SetActive(false);
    }

    public bool TryEatDinner()
    {
        if (!_isReady || QuestManager.Instance == null ||
            !QuestManager.Instance.IsQuestActive("dinner-eat")) return false;
        var intro = FindFirstObjectByType<IntroSequence>();
        if (intro == null) return false;

        _isReady = false;
        if (readyMealObject != null) readyMealObject.SetActive(false);
        intro.OnDinnerEaten();
        return true;
    }

    public void SetReadyForDebug()
    {
        GetComponent<DinnerCookingWindow>()?.ResetAttempt();
        StopAllCoroutines();
        if (cookingFoodObject != null) cookingFoodObject.SetActive(false);
        if (readyMealObject != null) readyMealObject.SetActive(true);
        _isCooking = false;
        _isReady = true;
    }

    public void ResetForCookDebug()
    {
        GetComponent<DinnerCookingWindow>()?.ResetAttempt();
        StopAllCoroutines();
        _isCooking = false;
        _isReady = false;
        if (cookingFoodObject != null) cookingFoodObject.SetActive(false);
        if (readyMealObject != null) readyMealObject.SetActive(false);
    }

    void CompleteCooking()
    {
        if (stoveAudioSource != null && cookDoneSfx != null)
            stoveAudioSource.PlayOneShot(cookDoneSfx);

        if (readyMealObject != null) readyMealObject.SetActive(true);
        if (cookingFoodObject != null) cookingFoodObject.SetActive(false);

        _isCooking = false;
        _isReady = true;

        var intro = FindFirstObjectByType<IntroSequence>();
        intro?.OnDinnerCooked();
    }
}
}
