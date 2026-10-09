using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

public sealed class BasementHatchLock : MonoBehaviour
{
    [Header("Видимая часть")]
    [Tooltip("Отделяется от люка и отлетает после удара.")]
    [SerializeField] private GameObject padlock;

    [Tooltip("Накладка/уширо́к на створке — остаётся висеть после взлома.")]
    [SerializeField] private Transform haspPlate;

    [Tooltip("Дуга замка; ссылка сохранена для совместимости сцены.")]
    [SerializeField] private Transform shackle;

    [Header("Геометрия вскрытия")]
    [Tooltip("Насколько дуга отгибается вбок в локальных координатах накладки.")]
    [SerializeField] private Vector3 shackleOpenOffset = new Vector3(0f, 0.11f, 0f);

    [Tooltip("Сколько секунд дуга отгибается.")]
    [SerializeField] private float pryDuration = 0.35f;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Дребезг замка, когда игрок пробует открыть его руками (без лома).")]
    [SerializeField] private AudioClip rattleSfx;
    [Tooltip("Звук удара по замку.")]
    [SerializeField] private AudioClip prySfx;

    [Header("Связь с сюжетом")]
    [Tooltip("Замок доступен для разрушения только при активном квесте.")]
    [SerializeField] private string requiredQuestTag = "pry-hatch-lock";

    [Header("Обратная связь")]
    [Tooltip("Если не задан — следующий шаг цепочки вызывается напрямую на IntroSequence " +
             "по имени метода OnHatchLockPried (тот же тонкий проброс, что у " +
             "FrontDoorLockInteractable). Задавать вручную нужно только если сцена " +
             "использует другой получатель.")]
    [SerializeField] private UnityEngine.Events.UnityEvent onPried;

    private bool _broken;
    private bool _playing;
    private PlayerInteraction _interaction;
    private PlayerController _player;
    private ProtagonistBody _body;
    private Camera _camera;
    private Quaternion _cameraRotation;
    private bool _wasLocked, _inventoryEnabled;
    private GameObject _heldTool;

    private float _interactionStarted;
    public float InteractionProgress => Mathf.Clamp01((Time.time - _interactionStarted) / 1.15f);
    public bool IsPlaying => _playing;
    public Vector3 StrikePoint => padlock != null ? padlock.transform.position : transform.position;
    public bool CanStartBreak(PlayerInteraction interaction)
    {
        if (_broken || _playing || interaction == null || interaction.GetHeldObject() != null ||
            QuestManager.Instance == null || !QuestManager.Instance.IsQuestActive(requiredQuestTag)) return false;
        var body = interaction.GetComponentInChildren<ProtagonistBody>();
        return body != null && body.CanKickLock(StrikePoint);
    }

    public bool TryStartBreak(PlayerInteraction interaction)
    {
        if (!CanStartBreak(interaction)) return false;
        _interaction = interaction;
        _player = interaction.GetComponent<PlayerController>();
        _camera = interaction.playerCamera;
        _body = interaction.GetComponentInChildren<ProtagonistBody>();
        if (_player == null || _camera == null || _player.isCameraLocked) return false;
        _playing = true;
        _interactionStarted = Time.time;
        _wasLocked = _player.isCameraLocked;
        _cameraRotation = _camera.transform.localRotation;
        _player.isCameraLocked = true;
        interaction.SetLockBreaking(true);
        if (interaction.inventory != null)
        {
            _inventoryEnabled = interaction.inventory.enabled;
            interaction.inventory.enabled = false;
            var inventory = interaction.inventory;
            _heldTool = inventory.crowbarInHand != null && inventory.crowbarInHand.activeSelf ? inventory.crowbarInHand :
                inventory.hammerInHand != null && inventory.hammerInHand.activeSelf ? inventory.hammerInHand : null;
            if (_heldTool != null) _heldTool.SetActive(false);
        }
        StartCoroutine(StrikeRoutine());
        return true;
    }

    public bool IsBroken => _broken;
    public bool IsLocked => !_broken;
    public string RequiredQuestTag => requiredQuestTag;

    // Вызывается из BasementHatch.Interact(): пока замок цел, створки не поедут.
    // Возвращает false, чтобы вызывающая сторона знала, что открытия не произошло.
    public bool TryUnlockByHand()
    {
        if (_broken) return true;

        if (audioSource != null && rattleSfx != null)
            audioSource.PlayOneShot(rattleSfx);

        return false;
    }

    // Старые вызовы удара ломом не запускают постановочное взаимодействие.
    public void Break(PlayerInventory inventory) { }

    private void CompleteBreak()
    {
        _broken = true;
        QuestManager.Instance?.RecordQuestCompletion(requiredQuestTag);
        onPried?.Invoke();
        if (onPried != null && onPried.GetPersistentEventCount() > 0) return;
        FindFirstObjectByType<IntroSequence>()?.OnHatchLockPried();
    }

    private IEnumerator StrikeRoutine()
    {
        Quaternion start = _camera.transform.rotation;
        Quaternion aim = Quaternion.LookRotation(StrikePoint - _camera.transform.position, Vector3.up);
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            _camera.transform.rotation = Quaternion.Slerp(start, aim, Mathf.SmoothStep(0f, 1f, t / 0.2f));
            yield return null;
        }
        _camera.transform.rotation = aim;
        _body.BeginLockKick(StrikePoint);
        yield return new WaitForSeconds(0.53f);
        if (audioSource != null && prySfx != null) audioSource.PlayOneShot(prySfx);
        LaunchPadlock();
        for (float t = 0f; t < 0.42f; t += Time.deltaTime)
        {
            _camera.transform.rotation = aim * Quaternion.Euler(Mathf.Sin(t * 55f) * 1.5f * Mathf.Exp(-t * 15f), 0f, 0f);
            yield return null;
        }
        RestorePlayer();
        CompleteBreak();
    }

    private void LaunchPadlock()
    {
        if (padlock == null) return;
        // Отделяем только замок: накладка и коллайдер створки остаются на люке.
        padlock.transform.SetParent(null, true);
        foreach (var collider in padlock.GetComponentsInChildren<Collider>())
            if (collider is MeshCollider mesh) mesh.convex = true;
        var rigidbody = padlock.GetComponent<Rigidbody>();
        if (rigidbody == null) rigidbody = padlock.AddComponent<Rigidbody>();
        rigidbody.mass = 0.4f;
        rigidbody.isKinematic = false;
        rigidbody.useGravity = true;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var capsule = _player.GetComponent<CharacterController>();
        if (capsule != null)
            foreach (var collider in padlock.GetComponentsInChildren<Collider>()) Physics.IgnoreCollision(collider, capsule);
        rigidbody.linearVelocity = _player.transform.right * 2.5f + Vector3.up * 1.6f + _player.transform.forward * 0.8f;
        rigidbody.angularVelocity = new Vector3(5f, 8f, 3f);
        Destroy(padlock, 12f);
    }

    private void RestorePlayer()
    {
        if (!_playing) return;
        _playing = false;
        if (_body != null) _body.EndLockKick();
        if (_camera != null) _camera.transform.localRotation = _cameraRotation;
        if (_player != null) _player.isCameraLocked = _wasLocked;
        if (_interaction != null)
        {
            _interaction.SetLockBreaking(false);
            if (_interaction.inventory != null) _interaction.inventory.enabled = _inventoryEnabled;
        }
        if (_heldTool != null) _heldTool.SetActive(true);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        RestorePlayer();
    }

    // Отладочный пропуск: считать замок вскрытым. Без звука и без onPried — только чтобы
    // debug-стадии могли открыть створки, не вызывая заново шаг сюжета.
    public void DebugForceBroken()
    {
        _broken = true;
        if (padlock != null) padlock.SetActive(false);
    }

}
}
