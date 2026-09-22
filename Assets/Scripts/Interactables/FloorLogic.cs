using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

public class FloorLogic : MonoBehaviour
{
    public enum BreakState { Available, Playing, Completed }

    [Header("Постановочная сцена")]
    [SerializeField] private Animator sequenceAnimator;
    [SerializeField] private string playTrigger = "Play";
    [SerializeField, Min(0.1f)] private float fallbackDuration = 4f;
    [SerializeField] private GameObject intactBoard;
    [SerializeField] private GameObject animatedBoard;
    [SerializeField] private GameObject hole;
    [SerializeField] private GameObject worldCrowbar;
    [SerializeField] private Collider boardCollider;
    [SerializeField] private Collider interactionCollider;

    [Header("Связи")]
    [SerializeField] private GameObject knockController;
    [SerializeField] private GameObject holeEventTrigger;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pryClip;
    [SerializeField] private AudioClip breakClip;

    [Header("Состояние пола")]
    [SerializeField] private BreakState state;
    public bool isBroken;
    public BreakState State => state;

    [Header("Привязка к квесту")]
    [Tooltip("Пол ломается только пока активен квест с этим тегом — иначе лом бьёт по полу вхолостую.")]
    [SerializeField] private string requiredQuestTag = "break-floor";

    private Coroutine fallbackRoutine;

    void Awake()
    {
        if (sequenceAnimator == null) sequenceAnimator = GetComponent<Animator>();
        if (boardCollider == null) boardCollider = GetComponent<Collider>();

        if (isBroken || state == BreakState.Completed) ApplyCompletedState();
        else ApplyAvailableState();
    }

    public bool CanStartBreak(PlayerInventory inventory)
    {
        return state == BreakState.Available &&
               inventory != null && inventory.HasItem("Crowbar") &&
               QuestManager.Instance != null && QuestManager.Instance.IsQuestActive(requiredQuestTag);
    }

    public bool TryStartBreak(PlayerInventory inventory)
    {
        if (!CanStartBreak(inventory)) return false;

        state = BreakState.Playing;
        if (interactionCollider != null) interactionCollider.enabled = false;
        SetActive(intactBoard, false);
        SetActive(animatedBoard, true);
        SetActive(worldCrowbar, true);

        if (sequenceAnimator != null)
        {
            sequenceAnimator.ResetTrigger(playTrigger);
            sequenceAnimator.SetTrigger(playTrigger);
        }

        fallbackRoutine = StartCoroutine(CompleteAfterDelay());
        return true;
    }

    // Совместимость со старыми UnityEvent. Новое взаимодействие вызывает TryStartBreak().
    public void Break()
    {
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        TryStartBreak(inventory);
    }

    public void BeginPry()
    {
        if (state != BreakState.Playing) return;
        SetActive(intactBoard, false);
        SetActive(animatedBoard, true);
        SetActive(worldCrowbar, true);
    }

    public void PlayPrySound() => PlayOneShot(pryClip);
    public void PlayBreakSound() => PlayOneShot(breakClip);

    public void OpenHole()
    {
        if (state != BreakState.Playing) return;
        if (boardCollider != null) boardCollider.enabled = false;
        SetActive(hole, true);
        if (holeEventTrigger != null) holeEventTrigger.SetActive(true);
        if (knockController != null) knockController.SetActive(false);
    }

    public void CompleteBreak()
    {
        if (state == BreakState.Completed) return;
        if (fallbackRoutine != null) StopCoroutine(fallbackRoutine);
        fallbackRoutine = null;

        bool awardProgress = state == BreakState.Playing;
        ApplyCompletedState();

        if (awardProgress && QuestManager.Instance != null)
            QuestManager.Instance.AddProgress(1);
    }

    public void Fix()
    {
        if (state != BreakState.Completed) return;

        state = BreakState.Available;
        isBroken = false;
        ApplyAvailableState();
    }

    private IEnumerator CompleteAfterDelay()
    {
        yield return new WaitForSeconds(fallbackDuration);
        CompleteBreak();
    }

    private void ApplyAvailableState()
    {
        state = BreakState.Available;
        isBroken = false;
        SetActive(intactBoard, true);
        SetActive(animatedBoard, false);
        SetActive(hole, false);
        SetActive(worldCrowbar, false);
        if (boardCollider != null) boardCollider.enabled = true;
        if (interactionCollider != null) interactionCollider.enabled = true;
        if (holeEventTrigger != null) holeEventTrigger.SetActive(false);
    }

    private void ApplyCompletedState()
    {
        state = BreakState.Completed;
        isBroken = true;
        SetActive(intactBoard, false);
        SetActive(animatedBoard, false);
        SetActive(hole, true);
        SetActive(worldCrowbar, false);
        if (boardCollider != null) boardCollider.enabled = false;
        if (interactionCollider != null) interactionCollider.enabled = false;
        if (holeEventTrigger != null) holeEventTrigger.SetActive(true);
        if (knockController != null) knockController.SetActive(false);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }

    private void SetActive(GameObject target, bool active)
    {
        if (target == null) return;
        if (target != gameObject)
        {
            target.SetActive(active);
            return;
        }

        Renderer[] renderers = target.GetComponents<Renderer>();
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = active;
    }
}
}
