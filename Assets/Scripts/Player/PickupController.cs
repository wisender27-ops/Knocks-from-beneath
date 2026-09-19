using UnityEngine;

namespace KnocksFromBeneath
{

// Физический захват/перенос/бросок предметов игроком. Выделен из PlayerInteraction
// (T-07): PlayerInteraction остаётся тонким диспетчером рейкаста, вся физика
// удержания предмета в руках — здесь.
public class PickupController : MonoBehaviour
{
    [Header("Ссылки")]
    public Camera playerCamera;
    public Transform holdPoint;

    [Header("Следование за точкой удержания")]
    public float followSpeed = 20f;
    [SerializeField] private float maxHoldDistance = 2.2f;

    [Header("Настройки броска")]
    public float throwForce = 15f;
    [SerializeField] private float throwTorqueRandomness = 5f;
    [SerializeField] private float dropForwardImpulse = 2f;

    [Header("Физика удержания/отпускания")]
    [SerializeField] private float heldRigidbodyDamping = 15f;
    [SerializeField] private float releasedRigidbodyDamping = 0.05f;

    [Header("Эффекты сочности при броске")]
    public float shakeIntensity = 0.1f;
    public float shakeDuration = 0.15f;
    public float fovKickAmount = 3f;
    public float fovReturnSpeed = 5f;
    private const float FovSnapThreshold = 0.1f;

    private GameObject _heldObj;
    private Rigidbody _heldObjRb;
    private PickableItem _heldItemScript;
    private int _originalLayer;
    private Quaternion _heldRotationOffset = Quaternion.identity;
    private float _defaultFov;

    void Start()
    {
        if (playerCamera != null)
            _defaultFov = playerCamera.fieldOfView;
    }

    void FixedUpdate()
    {
        if (_heldObj != null && _heldObjRb != null && holdPoint != null)
            MovePhysicsObject();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (_heldObj != null)
            Release();
    }

    public GameObject GetHeldObject()
    {
        return _heldObj;
    }

    public bool TryGrab(GameObject obj)
    {
        if (obj == null || _heldObj != null || holdPoint == null) return false;
        if (obj.GetComponent<Rigidbody>() == null) return false;

        GrabPhysicsObject(obj);
        return true;
    }

    void GrabPhysicsObject(GameObject obj)
    {
        _heldObj = obj;
        _heldObjRb = obj.GetComponent<Rigidbody>();
        if (_heldObjRb == null)
        {
            _heldObj = null;
            return;
        }

        _heldItemScript = obj.GetComponent<PickableItem>();
        _heldRotationOffset = Quaternion.Inverse(holdPoint.rotation) * obj.transform.rotation;

        _originalLayer = _heldObj.layer;
        int heldLayer = LayerMask.NameToLayer("HeldItem");
        if (heldLayer >= 0)
            _heldObj.layer = heldLayer;

        _heldObjRb.interpolation = RigidbodyInterpolation.Interpolate;
        _heldObjRb.useGravity = false;
        _heldObjRb.linearDamping = heldRigidbodyDamping;
        _heldObjRb.angularDamping = heldRigidbodyDamping;
        _heldObjRb.constraints = RigidbodyConstraints.FreezeRotation;

        // Останавливаем частицы когда берём объект
        ParticleSystem ps = obj.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Сюжет: достали пирог. Публикуем событие безусловно — IntroSequence
        // сама решит, важно ли ей это сейчас (см. GameEvents.OnPieGrabbed).
        if (obj.GetComponent<PieQuestItem>() != null)
            GameEvents.OnPieGrabbed?.Invoke();
    }

    // Нажатие E, пока что-то держим в руках, и это не еда/микроволновка —
    // пробуем поставить в зону размещения, иначе просто роняем.
    public void HandleInteractPressed()
    {
        if (_heldItemScript != null && _heldItemScript.activeZone != null)
        {
            if (_heldItemScript.activeZone.TryPlaceBox(_heldObj))
            {
                ClearHeldObject();
                return;
            }
        }
        DropObject();
    }

    private void RestoreHeldRigidbody(Rigidbody rb, bool resetInterpolation)
    {
        if (rb == null) return;

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.linearDamping = releasedRigidbodyDamping;
        rb.angularDamping = releasedRigidbodyDamping;
        rb.constraints = RigidbodyConstraints.None;
        if (resetInterpolation)
            rb.interpolation = RigidbodyInterpolation.None;
    }

    void DropObject()
    {
        if (_heldObj == null) return;

        _heldObj.layer = _originalLayer;
        _heldObj.transform.SetParent(null);

        RestoreHeldRigidbody(_heldObjRb, true);
        if (_heldObjRb != null && playerCamera != null)
            _heldObjRb.AddForce(playerCamera.transform.forward * dropForwardImpulse, ForceMode.Impulse);

        ClearHeldObject();
    }

    void ClearHeldObject()
    {
        if (_heldObj != null)
        {
            // Включаем частицы обратно когда отпускаем объект
            ParticleSystem ps = _heldObj.GetComponentInChildren<ParticleSystem>();
            if (ps != null) ps.Play();
        }

        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
        _heldRotationOffset = Quaternion.identity;
    }

    // Вызывается, когда держимый объект уничтожили извне (например, пирог
    // съеден в PlayerInteraction) — просто забываем о нём, без физики и частиц.
    public void ForceClearHeld()
    {
        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
    }

    void MovePhysicsObject()
    {
        Vector3 targetPos = holdPoint.position;
        Vector3 currentPos = _heldObj.transform.position;

        Vector3 directionToTarget = targetPos - currentPos;
        float distance = directionToTarget.magnitude;

        _heldObjRb.linearVelocity = directionToTarget * followSpeed;
        Quaternion targetRotation = holdPoint.rotation * _heldRotationOffset;
        _heldObjRb.MoveRotation(
            Quaternion.Slerp(_heldObj.transform.rotation, targetRotation, Time.fixedDeltaTime * followSpeed)
        );

        if (distance > maxHoldDistance)
            DropObject();
    }

    public void HandleThrowPressed()
    {
        ThrowObject();
    }

    void ThrowObject()
    {
        Rigidbody rbToThrow = _heldObjRb;
        GameObject objToThrow = _heldObj;

        objToThrow.layer = _originalLayer;
        RestoreHeldRigidbody(rbToThrow, true);

        ClearHeldObject();

        rbToThrow.AddForce(playerCamera.transform.forward * throwForce, ForceMode.Impulse);
        rbToThrow.AddTorque(new Vector3(Random.value, Random.value, Random.value) * throwTorqueRandomness, ForceMode.Impulse);

        StopAllCoroutines();
        StartCoroutine(ShakeAndKick());
    }

    private System.Collections.IEnumerator ShakeAndKick()
    {
        Vector3 originalPos = playerCamera.transform.localPosition;
        float elapsed = 0.0f;

        playerCamera.fieldOfView = _defaultFov + fovKickAmount;

        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;
            playerCamera.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }

        playerCamera.transform.localPosition = originalPos;

        while (Mathf.Abs(playerCamera.fieldOfView - _defaultFov) > FovSnapThreshold)
        {
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, _defaultFov, Time.deltaTime * fovReturnSpeed);
            yield return null;
        }
        playerCamera.fieldOfView = _defaultFov;
    }

    public GameObject Release()
    {
        if (_heldObj == null) return null;

        GameObject released = _heldObj;
        Rigidbody releasedRb = _heldObjRb;

        released.layer = _originalLayer;
        released.transform.SetParent(null);

        RestoreHeldRigidbody(releasedRb, true);

        _heldObj = null;
        _heldObjRb = null;
        _heldItemScript = null;
        _heldRotationOffset = Quaternion.identity;

        ParticleSystem ps = released.GetComponentInChildren<ParticleSystem>();
        if (ps != null) ps.Play();
        return released;
    }
}
}
