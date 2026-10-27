using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{

public class Door : MonoBehaviour
{
    public event System.Action LockedAttempted;
    public event System.Action Opened;
    private bool _openingNotified;
    private bool _detached;
    public enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("Настройки двери")]
    [SerializeField] private float angleRotation = 90f;
    [SerializeField] private float openSpeed = 10f;
    [SerializeField] private float automaticOpenSpeed = 4f;
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Y;
    [SerializeField] private bool interactionLocked = false;
    [Header("Петля створки")]
    [SerializeField] private bool useOuterEdgeHinge;
    [SerializeField] private bool hingeAtMaximumWorldX;
    [SerializeField] private bool addGateCollider;

    [Header("Сдвижная калитка (вместо поворота)")]
    [Tooltip("Дверь не вращается на петле, а уезжает вбок по направляющей (American barn gate).")]
    [SerializeField] private bool slideInsteadOfSwing;
    [Tooltip("Мировое направление сдвига. По умолчанию определяется автоматически: вдоль мировой оси X.")]
    [SerializeField] private Vector3 slideWorldDirection = Vector3.zero;
    [Tooltip("На сколько метров сдвинуть створку в открытом состоянии.")]
    [SerializeField] private float slideDistance = 1.15f;

    [Header("Настройки звука (Скрип)")]
    public AudioSource sfxSource;
    public AudioClip doorCreakClip;

    [Range(0.1f, 10f)]
    [SerializeField] private float volumeMultiplier = 1.5f;
    [SerializeField] private float minVelocityThreshold = 2f;

    [Header("Сюжетное закрытие (без звука)")]
    [Tooltip("Дверь закрывается сама, когда игрок ложится спать. Такое закрытие не должно звучать: скрип и спуск эмбиент-монстра — технические детали перехода, игрок их не должен слышать.")]
    [SerializeField] private bool muteCreakOnAutoClose = true;

    [Header("Заперто (T-21)")]
    [Tooltip("Звук, когда игрок пытается открыть запертую дверь. Если не задан — просто дёргается без звука.")]
    [SerializeField] private AudioClip lockedRattleClip;
    [SerializeField] private AudioSource feedbackSource;
    private float _nextRattleSound;
    [SerializeField] private float rattleAngle = 6f;
    [SerializeField] private float rattleDuration = 0.3f;

    // --- НОВАЯ ЛОГИКА IS_OPEN ---
    [SerializeField] private bool _isOpen; // Внутренняя переменная
    public bool isOpen
    {
        get => _isOpen;
        set
        {
            if (_detached) return;
            if (_isOpen == value) return; // Если значение не изменилось — ничего не делаем
            _isOpen = value;

            // Если мы НЕ держим дверь руками, реагируем на смену галочки
            if (!isBeingHeld)
            {
                if (_isOpen) OpenDoor();
                else CloseDoor();
            }
        }
    }

    private Vector3 baseLocalEuler;
    private float baseAxisAngle;
    private float targetAxisAngle;
    private bool isBeingHeld = false;

    private float currentOffset = 0f;
    private float previousRotationY;
    private float smoothDoorVelocity;
    private Vector3 closedWorldPosition;
    private Quaternion closedWorldRotation;
    private Vector3 hingeWorldPosition;
    private Quaternion previousWorldRotation;
    private Vector3 slideDirection = Vector3.left;   //Resolved direction of the sliding gate (world space)
    private Vector3 previousWorldPosition;

    void Awake()
    {
        if (addGateCollider && !TryGetComponent<Collider>(out _))
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center = filter.sharedMesh.bounds.center;
                box.size = filter.sharedMesh.bounds.size;
            }
        }

        if (useOuterEdgeHinge && sfxSource == null && doorCreakClip != null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            AudioRouting.Assign(sfxSource, AudioBus.World);
            sfxSource.spatialBlend = 1f;
            sfxSource.minDistance = 1f;
            sfxSource.maxDistance = 12f;
        }
    }

    void Start()
    {
        closedWorldPosition = transform.position;
        closedWorldRotation = transform.rotation;
        previousWorldRotation = closedWorldRotation;
        previousWorldPosition = closedWorldPosition;
        if (slideInsteadOfSwing)
        {
            // The panel is aligned along world X, so the rail runs along world X too.
            // An explicit direction wins; otherwise fall back to the sign that actually
            // has free space (the closed side is blocked by the house wall).
            if (slideWorldDirection.sqrMagnitude > 0.0001f)
            {
                slideDirection = slideWorldDirection.normalized;
            }
            else
            {
                slideDirection = HasSlideRoom(Vector3.left) ? Vector3.left : Vector3.right;
            }
        }
        if (useOuterEdgeHinge)
        {
            Renderer gateRenderer = GetComponent<Renderer>();
            if (gateRenderer != null)
            {
                Bounds bounds = gateRenderer.bounds;
                hingeWorldPosition = new Vector3(
                    hingeAtMaximumWorldX ? bounds.max.x : bounds.min.x,
                    bounds.center.y, bounds.center.z);
            }
            else
            {
                hingeWorldPosition = closedWorldPosition;
            }
        }
        baseLocalEuler = transform.localEulerAngles;
        baseAxisAngle = GetAxisAngle(baseLocalEuler);

        targetAxisAngle = baseAxisAngle;
        previousRotationY = baseAxisAngle;

        if (sfxSource && doorCreakClip)
        {
            sfxSource.clip = doorCreakClip;
            sfxSource.loop = true;
            sfxSource.playOnAwake = false;
            sfxSource.volume = 0f;
        }
    }

    void Update()
    {
        if (isBeingHeld)
        {
            HandleManualOpen();
        }

        // Плавный поворот к целевому смещению
        targetAxisAngle = baseAxisAngle + currentOffset;
        if (slideInsteadOfSwing)
        {
            // American barn gate: the panel slides along the rail, rotation is untouched.
            // currentOffset is reused as a 0..1 progress value so StartHolding/StopHolding,
            // the creak detector and ForceClose keep working unchanged.
            float progress = Mathf.Clamp01(currentOffset / Mathf.Max(Mathf.Abs(angleRotation), 0.0001f));
            Vector3 targetPosition = closedWorldPosition + slideDirection * (slideDistance * progress);
            float speed = isBeingHeld ? openSpeed : automaticOpenSpeed;
            float smoothing = 1f - Mathf.Exp(-speed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, smoothing);
        }
        else if (useOuterEdgeHinge)
        {
            Quaternion swing = Quaternion.AngleAxis(currentOffset, Vector3.up);
            Quaternion targetRotation = swing * closedWorldRotation;
            Vector3 targetPosition = hingeWorldPosition + swing * (closedWorldPosition - hingeWorldPosition);
            float speed = isBeingHeld ? openSpeed : automaticOpenSpeed;
            float smoothing = 1f - Mathf.Exp(-speed * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, targetPosition, smoothing),
                Quaternion.Slerp(transform.rotation, targetRotation, smoothing));
        }
        else
        {
            Quaternion targetQuaternion = BuildRotation(targetAxisAngle);
            float speed = isBeingHeld ? openSpeed : automaticOpenSpeed;
            float smoothing = 1f - Mathf.Exp(-speed * Time.deltaTime);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetQuaternion, smoothing);
        }

        // Расчет скорости для звука
        float currentAxisAngle = GetAxisAngle(transform.localEulerAngles);
        float deltaRot = Mathf.DeltaAngle(previousRotationY, currentAxisAngle);
        float rawVelocity = slideInsteadOfSwing
            ? Vector3.Distance(transform.position, previousWorldPosition) / Mathf.Max(Time.deltaTime, 0.0001f)
            : (useOuterEdgeHinge
                ? Quaternion.Angle(previousWorldRotation, transform.rotation) / Mathf.Max(Time.deltaTime, 0.0001f)
                : Mathf.Abs(deltaRot) / Mathf.Max(Time.deltaTime, 0.0001f));

        smoothDoorVelocity = Mathf.Lerp(smoothDoorVelocity, rawVelocity, Time.deltaTime * 10f);
        previousRotationY = currentAxisAngle;
        previousWorldPosition = transform.position;
        previousWorldRotation = transform.rotation;

        // Пока играет фидбек-тряска запертой двери — не считаем это "настоящим" скрипом:
        // RattleRoutine крутит currentOffset ради ощущения "заперто", у неё свой звук
        // (lockedRattleClip), и generic-детектор скорости не должен на неё реагировать —
        // иначе дёргание ЛЮБОЙ запертой двери (в т.ч. дверцы микроволновки) могло спавнить
        // эмбиент-монстра через тот же путь, что обычный скрип двери.
        //
        // _suppressCreak добавляет второе исключение: сюжетное закрытие на ночь.
        // Пока створка реально не доехала (currentOffset != 0), скрип не считается —
        // иначе игрок услышал бы все двери дома в момент ухода ко сну.
        if (!_isRattling && !(_suppressCreak && currentOffset != 0f))
            ManageCreakSound(smoothDoorVelocity);

        // Створка доехала — снимаем подавление, двери снова «живые».
        if (_suppressCreak && currentOffset == 0f)
        {
            _suppressCreak = false;
            smoothDoorVelocity = 0f;
        }

        // Обновляем состояние галочки без вызова сеттера, чтобы просто видеть статус в инспекторе
        // Но лучше оставить управление за пользователем через свойство выше.
        // Чтобы галочка в инспекторе сама не "прыгала", пока ты тянешь дверь:
        if (isBeingHeld) _isOpen = Mathf.Abs(currentOffset) > 5f;
        if (!_isRattling && Mathf.Abs(currentOffset) > 5f && !_openingNotified)
        {
            _openingNotified = true;
            Opened?.Invoke();
        }
        if (!_isRattling && Mathf.Abs(currentOffset) < 0.5f) _openingNotified = false;
    }

    // Это позволит видеть изменения в инспекторе Unity в реальном времени
    private void OnValidate()
    {
        // Если ты в редакторе клацнул галочку — дверь среагирует
        if (Application.isPlaying)
        {
            if (_isOpen) OpenDoor();
            else CloseDoor();
        }
    }

    private void HandleManualOpen()
    {
        float mouseMove = Input.GetAxis("Mouse Y");

        if (Mathf.Abs(mouseMove) > 0.01f)
        {
            float directionMultiplier = (angleRotation < 0) ? -1f : 1f;
            float moveStep = mouseMove * mouseSensitivity * 5f * directionMultiplier;

            float min = Mathf.Min(0, angleRotation);
            float max = Mathf.Max(0, angleRotation);

            currentOffset = Mathf.Clamp(currentOffset + moveStep, min, max);
        }
    }

    // Проверяет, есть ли свободное место для сдвига створки в указанную мировую сторону.
    // Без этого автоматический выбор стороны угадал бы и уткнулся в стену дома.
    private bool HasSlideRoom(Vector3 direction)
    {
        Collider self = GetComponent<Collider>();
        if (self == null) return true;

        Bounds bounds = self.bounds;
        Vector3 half = bounds.extents * 0.95f;
        for (float d = 0.1f; d <= slideDistance; d += 0.1f)
        {
            Vector3 probe = bounds.center + direction * d;
            Collider[] hits = Physics.OverlapBox(probe, half, transform.rotation, PhysicsMasks.AllLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (hit == self || hit.transform.IsChildOf(transform)) continue;
                return false;
            }
        }
        return true;
    }

    private void ManageCreakSound(float velocity)
    {
        if (sfxSource == null || doorCreakClip == null) return;

        // Сюжетное закрытие на ночь: скрип не должен быть слышен вообще.
        // Стоит ДАЖЕ если флаг почему-то ещё не снят — лучше лишний кадр
        // тишины, чем щелчок скрипа в момент перехода.
        if (_suppressCreak)
        {
            sfxSource.volume = 0f;
            if (sfxSource.isPlaying) sfxSource.Pause();
            return;
        }

        if (velocity > minVelocityThreshold)
        {
            if (!sfxSource.isPlaying)
            {
                sfxSource.Play();
                TriggerMonsterEvent();
            }

            float targetVolume = Mathf.Clamp((velocity / 100f) * volumeMultiplier, 0f, 1f);
            sfxSource.volume = Mathf.Lerp(sfxSource.volume, targetVolume, Time.deltaTime * 12f);
            sfxSource.pitch = Mathf.Clamp(0.85f + (velocity * 0.003f), 0.85f, 1.15f);
        }
        else
        {
            sfxSource.volume = Mathf.Lerp(sfxSource.volume, 0f, Time.deltaTime * 15f);
            if (sfxSource.volume < 0.01f && sfxSource.isPlaying)
                sfxSource.Pause();
        }
    }

    public void CloseDoor()
    {
        if (_detached) return;
        currentOffset = 0f;
        _isOpen = false;
    }

    public void OpenDoor()
    {
        if (_detached) return;
        currentOffset = angleRotation;
        _isOpen = true;
    }

    public void BurstOpen()
    {
        if (_detached) return;
        SetInteractionLocked(false);
        isBeingHeld = false;
        _suppressCreak = true;
        if (sfxSource != null) sfxSource.Stop();
        StartCoroutine(BurstOpenRoutine());
    }

    public void BreakOffHinges(Vector3 outward, AudioClip breakClip, AudioClip landingClip)
    {
        BreakOffHinges(detachedLandingCenter, Quaternion.Euler(detachedLandingEuler), breakClip, landingClip);
    }

    // B-02: точка падения задаётся вызывающим — выбитая снаружи дверь ложится в прихожей, а не на крыльце.
    public void BreakOffHinges(Vector3 landingCenter, Quaternion landingRotation, AudioClip breakClip, AudioClip landingClip)
    {
        if (_detached) return;
        _detached = true;
        StopAllCoroutines();
        isBeingHeld = false;
        interactionLocked = false;
        _isOpen = true;
        _openingNotified = true;
        _suppressCreak = true;
        if (sfxSource != null) sfxSource.Stop();
        enabled = false;

        transform.SetParent(null, true);
        foreach (Collider existing in GetComponentsInChildren<Collider>(true))
            existing.enabled = false;

        BoxCollider leafCollider = gameObject.AddComponent<BoxCollider>();
        MeshFilter mesh = GetComponent<MeshFilter>();
        if (mesh != null && mesh.sharedMesh != null)
        {
            leafCollider.center = mesh.sharedMesh.bounds.center;
            leafCollider.size = mesh.sharedMesh.bounds.size;
        }
        else
        {
            // Renderer bounds are in world space; transform all corners for a rotated leaf.
            Renderer leafRenderer = GetComponentInChildren<Renderer>();
            if (leafRenderer != null)
            {
                Bounds world = leafRenderer.bounds;
                Bounds local = new Bounds(transform.InverseTransformPoint(world.center), Vector3.zero);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = world.center + Vector3.Scale(world.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    local.Encapsulate(transform.InverseTransformPoint(corner));
                }
                leafCollider.center = local.center;
                leafCollider.size = local.size;
            }
        }
        leafCollider.isTrigger = false;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = true;
        body.mass = 25f;
        body.constraints = RigidbodyConstraints.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        leafCollider.enabled = false;
        DetachedDoorImpact impact = gameObject.AddComponent<DetachedDoorImpact>();
        impact.Initialize(breakClip, landingClip, landingCenter, landingRotation, leafCollider, body);
    }

    [Header("Постановочное выбивание двери")]
    [SerializeField] private Vector3 detachedLandingCenter = new Vector3(-39.27f, 0.36f, 121.3f);
    [SerializeField] private Vector3 detachedLandingEuler = new Vector3(0f, 18f, 0f);

    private IEnumerator BurstOpenRoutine()
    {
        float previousSpeed = automaticOpenSpeed;
        automaticOpenSpeed = 28f;
        OpenDoor();
        yield return new WaitForSeconds(0.35f);
        automaticOpenSpeed = previousSpeed;
        smoothDoorVelocity = 0f;
        _suppressCreak = false;
    }

    public void ToggleDoor()
    {
        if (_detached) return;
        if (interactionLocked) { PlayLockedFeedback(); return; }
        isOpen = !isOpen; // Используем свойство
    }

    private void OnEnable()
    {
        GameEvents.OnNightStarted += ForceClose; // Подписываемся
    }

    private void OnDisable()
    {
        GameEvents.OnNightStarted -= ForceClose; // Отписываемся (обязательно для защиты от утечек памяти)
    }

    private void ForceClose()
    {
        // isOpen-сеттер закрывает дверь только если её не держат (см. свойство выше) —
        // если игрок в этот момент как раз тянул дверь руками, "принудительное" закрытие
        // на ночь молча ничего не делало. Сначала отпускаем захват, потом закрываем.
        isBeingHeld = false;

        // Сюжетное закрытие на ночь — БЕЗ ЗВУКА.
        //
        // Раньше здесь просто ставилось isOpen = false, и обычная анимация закрытия
        // разгоняла створку до скорости выше minVelocityThreshold. Дальше
        // ManageCreakSound считал это скрипом: включался AudioSource, и вдобавок
        // TriggerMonsterEvent() выпускал эмбиент-монстра. Игрок в момент ухода
        // ко сну слышал скрип всех дверей дома — чисто технический артефакт
        // перехода, который ломает атмосферу.
        //
        // Теперь на время сюжетного закрытия скрип гасится. Обычные действия
        // игрока (сам открыл/закрыл дверь, дёрнул запертую) звучат как раньше.
        _suppressCreak = muteCreakOnAutoClose;
        isOpen = false;
        // Сбрасываем сглаженную скорость: пока дверь закрывается, детектор скрипа
        // не должен накопить её заново и выстрелить звуком уже после снятия флага.
        smoothDoorVelocity = 0f;
    }

    private bool _suppressCreak;

    private void TriggerMonsterEvent()
    {
        Camera camera = Camera.main;
        if (MonsterWatcherManager.Instance != null && camera != null)
            MonsterWatcherManager.Instance.SpawnWatcher(camera.transform.position);
    }

    private float GetAxisAngle(Vector3 euler)
    {
        switch (rotationAxis)
        {
            case RotationAxis.X: return euler.x;
            case RotationAxis.Z: return euler.z;
            default: return euler.y;
        }
    }

    private Quaternion BuildRotation(float axisAngle)
    {
        Vector3 euler = baseLocalEuler;
        switch (rotationAxis)
        {
            case RotationAxis.X:
                euler.x = axisAngle;
                break;
            case RotationAxis.Z:
                euler.z = axisAngle;
                break;
            default:
                euler.y = axisAngle;
                break;
        }
        return Quaternion.Euler(euler);
    }

    public bool IsInteractionLocked => interactionLocked;

    public bool IsFullyOpen
    {
        get
        {
            if (_detached) return true;
            if (!_isOpen) return false;
            if (slideInsteadOfSwing)
                return Vector3.Distance(transform.position, closedWorldPosition + slideDirection * slideDistance) <= 0.02f;
            Quaternion openRotation = useOuterEdgeHinge
                ? Quaternion.AngleAxis(angleRotation, Vector3.up) * closedWorldRotation
                : BuildRotation(baseAxisAngle + angleRotation);
            return Quaternion.Angle(useOuterEdgeHinge ? transform.rotation : transform.localRotation, openRotation) <= 2f;
        }
    }

    public void SetInteractionLocked(bool isLocked)
    {
        if (_detached) return;
        interactionLocked = isLocked;
        if (interactionLocked)
        {
            isBeingHeld = false;
            CloseDoor();
        }
    }

    public void StartHolding()
    {
        if (_detached) return;
        if (interactionLocked) return;
        isBeingHeld = true;
    }

    public void StopHolding() => isBeingHeld = false;

    // T-21: раньше попытка открыть запертую дверь не давала игроку никакой реакции — тишина,
    // будто дверь просто не заметила клик. Дёргаем ручку и проигрываем звук, чтобы "заперто"
    // читалось физически, а не только по отсутствию отклика.
    private Coroutine _rattleCoroutine;
    private bool _isRattling;
    // A-01: DoorAudio не должен принимать тряску запертой двери или сорванную дверь за открытие/закрытие.
    public bool IsRattling => _isRattling;
    public bool IsDetached => _detached;
    public bool IsCreakSuppressed => _suppressCreak;

    public void PlayLockedFeedback()
    {
        if (!interactionLocked) return;
        LockedAttempted?.Invoke();
        if (lockedRattleClip != null && Time.time >= _nextRattleSound)
        {
            AudioSource source = feedbackSource != null ? feedbackSource : sfxSource;
            if (source != null) source.PlayOneShot(lockedRattleClip, .55f);
            _nextRattleSound = Time.time + .4f;
        }

        // StopCoroutine(string) останавливает только корутины, запущенные через
        // StartCoroutine(string) — эта запущена через StartCoroutine(IEnumerator), так что
        // старый вызов ничего не останавливал, и при частом дёргании тряски накладывались
        // друг на друга. Храним ссылку на Coroutine и останавливаем именно её.
        if (_rattleCoroutine != null)
            StopCoroutine(_rattleCoroutine);
        _rattleCoroutine = StartCoroutine(RattleRoutine());
    }

    IEnumerator RattleRoutine()
    {
        _isRattling = true;
        float t = 0f;
        while (t < rattleDuration)
        {
            t += Time.deltaTime;
            float decay = 1f - t / rattleDuration;
            currentOffset = Mathf.Sin(t * 40f) * rattleAngle * decay;
            yield return null;
        }
        currentOffset = 0f;
        _isRattling = false;
        _rattleCoroutine = null;
    }
}
}
