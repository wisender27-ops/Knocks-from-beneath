using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

// Засов на входной двери (T-17) — отдельный от самого открывания Interact()-объект:
// висит не на полотне двери, а на самой щеколде, чтобы "открыть дверь" и "запереть дверь"
// были разными точками наведения. Реальная блокировка (Door.SetInteractionLocked) и флаг
// GameState.frontDoorLocked выставляются в EveningRoundController, сюда попадает только
// тонкий проброс через IntroSequence — тот же паттерн, что StoveInteractable.
// Сам компонент отвечает лишь за видимую реакцию: щеколда уезжает в закрытое положение.
public class FrontDoorLockInteractable : MonoBehaviour
{
    [Tooltip("Подвижная часть щеколды. Если не задана — видимой реакции не будет, логика замка не изменится.")]
    [SerializeField] private Transform bolt;

    [Tooltip("Насколько (в локальных координатах засова) щеколда уезжает, когда дверь заперта.")]
    [SerializeField] private Vector3 lockedLocalOffset = new Vector3(0f, 0f, 0.035f);

    [Tooltip("Сколько секунд едет засов — T-21: раньше телепортировался мгновенно.")]
    [SerializeField] private float slideDuration = 0.25f;

    [Header("Звук")]
    [SerializeField] private AudioSource lockAudioSource;
    [SerializeField] private AudioClip lockSfx;

    private Vector3 _openLocalPosition;
    private bool _capturedOpenPosition;
    private bool _locked;

    void Awake()
    {
        CaptureOpenPosition();
    }

    void CaptureOpenPosition()
    {
        if (_capturedOpenPosition || bolt == null) return;
        _openLocalPosition = bolt.localPosition;
        _capturedOpenPosition = true;
    }

    public void Interact()
    {
        if (_locked) return;

        var intro = FindFirstObjectByType<IntroSequence>();
        if (intro == null) return;

        // Запереть можно только во время вечернего обхода — решает EveningRoundController,
        // здесь только реагируем на его ответ.
        if (!intro.OnFrontDoorLocked()) return;

        _locked = true;
        ShowLocked();
    }

    void ShowLocked()
    {
        CaptureOpenPosition();

        if (lockAudioSource != null && lockSfx != null)
            lockAudioSource.PlayOneShot(lockSfx);

        if (bolt != null && _capturedOpenPosition)
            StartCoroutine(SlideRoutine(_openLocalPosition + lockedLocalOffset));
    }

    IEnumerator SlideRoutine(Vector3 target)
    {
        Vector3 start = bolt.localPosition;
        float t = 0f;
        float duration = Mathf.Max(0.01f, slideDuration);
        while (t < duration)
        {
            t += Time.deltaTime;
            bolt.localPosition = Vector3.Lerp(start, target, t / duration);
            yield return null;
        }
        bolt.localPosition = target;
    }
}
}
