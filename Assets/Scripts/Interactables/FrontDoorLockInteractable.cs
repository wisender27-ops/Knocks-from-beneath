using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

// Засов входной двери (B-01): задвинуть — вечером дня 2 (EveningRoundController), отодвинуть — всегда,
// пока заперто. Состояние — GameState.frontDoorLocked; щеколда показывает его.
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
    private IntroSequence _intro;

    public bool IsLocked => GameState.frontDoorLocked;
    // Опрашивается фокусом взаимодействия каждый кадр — IntroSequence ищется один раз.
    public bool CanInteract => IsLocked || (Intro != null && Intro.CanLockFrontDoor);

    private IntroSequence Intro => _intro != null ? _intro : _intro = FindFirstObjectByType<IntroSequence>();

    void Awake()
    {
        CaptureOpenPosition();
        // Та же янтарная метка, что у сюжетных предметов, — пока засов можно задвинуть.
        QuestItemMarker.Ensure(gameObject).ConfigureCondition(() => !IsLocked && Intro != null && Intro.CanLockFrontDoor);
    }

    // Звуки входной двери (засов, дёргание запертой двери, открытие) звучат из щеколды, а не из
    // петель/центра створки. DoorAudio заводит свой источник уже после нашего Start — ждём кадр.
    IEnumerator Start()
    {
        yield return null;
        Vector3 latch = bolt != null ? bolt.position : transform.position;
        Door door = GetComponentInParent<Door>();
        if (door != null)
            foreach (AudioSource source in door.GetComponentsInChildren<AudioSource>(true))
                if (source != lockAudioSource && source.spatialBlend > 0f && source.transform != door.transform)
                    MoveToLatch(source, latch);
        if (lockAudioSource != null)
        {
            MoveToLatch(lockAudioSource, latch);
            lockAudioSource.spatialBlend = 1f;
            AudioRouting.Assign(lockAudioSource, AudioBus.World);
        }
    }

    static void MoveToLatch(AudioSource source, Vector3 latch)
    {
        source.transform.position = latch;
        source.minDistance = Mathf.Min(source.minDistance, 0.4f); // вблизи слышно, с какой стороны
    }

    void CaptureOpenPosition()
    {
        if (_capturedOpenPosition || bolt == null) return;
        _openLocalPosition = bolt.localPosition;
        _capturedOpenPosition = true;
    }

    public void Interact()
    {
        if (!CanInteract || Intro == null) return;
        bool changed = IsLocked ? Intro.OnFrontDoorUnlocked() : Intro.OnFrontDoorLocked();
        if (changed) ShowState();
    }

    void ShowState()
    {
        CaptureOpenPosition();

        if (lockAudioSource != null && lockSfx != null)
            lockAudioSource.PlayOneShot(lockSfx);

        if (bolt != null && _capturedOpenPosition)
        {
            StopAllCoroutines();
            StartCoroutine(SlideRoutine(TargetPosition));
        }
    }

    // Загрузка главы: щеколда сразу в положении по флагу, без звука.
    public void SnapToState()
    {
        CaptureOpenPosition();
        if (bolt != null && _capturedOpenPosition) bolt.localPosition = TargetPosition;
    }

    Vector3 TargetPosition => IsLocked ? _openLocalPosition + lockedLocalOffset : _openLocalPosition;

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
