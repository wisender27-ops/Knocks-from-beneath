using UnityEngine;

public class PickUpSystem : MonoBehaviour
{
    public Transform holdPoint;
    public float pickUpRange = 3f;
    private GameObject heldObj;
    private Rigidbody heldObjRb;
    private PickableItem heldItemScript;
    private int originalLayer;
    [SerializeField] private Camera playerCamera;
    private float distance = 3.0f;

    // Параметры для плавной фиксации
    [Header("Настройки плавности")]
    public float followSpeed = 20f; // Скорость следования за holdPoint

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObj == null)
            {
                Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, distance))
                {
                    if (hit.transform.CompareTag("Pickable"))
                    {
                        PickUpObject(hit.transform.gameObject);
                    }
                }
            }
            else
            {
                if (heldItemScript != null && heldItemScript.activeZone != null)
                {
                    if (heldItemScript.activeZone.TryPlaceBox(heldObj))
                    {
                        heldObj = null;
                        heldItemScript = null;
                        return;
                    }
                }
                DropObject();
            }
        }
    }

    void FixedUpdate()
    {
        if (heldObj != null)
        {
            MoveObject();
        }
    }

    void PickUpObject(GameObject pickObj)
    {
        heldObj = pickObj;
        heldObjRb = pickObj.GetComponent<Rigidbody>();
        heldItemScript = pickObj.GetComponent<PickableItem>();

        originalLayer = heldObj.layer;
        heldObj.layer = LayerMask.NameToLayer("HeldItem");

        // ПРАВКА ДЛЯ СТАБИЛЬНОСТИ
        heldObjRb.useGravity = false;
        heldObjRb.interpolation = RigidbodyInterpolation.Interpolate; // Сглаживает движение
        heldObjRb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Против провалов
        
        heldObjRb.linearDamping = 5f; 
        heldObjRb.angularDamping = 5f;
        heldObjRb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void DropObject()
    {
        if (heldObj == null) return;

        heldObj.layer = originalLayer;
        
        // ВОЗВРАЩАЕМ СТАНДАРТНЫЕ НАСТРОЙКИ
        heldObjRb.useGravity = true;
        heldObjRb.interpolation = RigidbodyInterpolation.None;
        heldObjRb.linearDamping = 1f;
        heldObjRb.angularDamping = 0.05f;
        heldObjRb.constraints = RigidbodyConstraints.None;

        heldObj = null;
        heldItemScript = null;
    }

    void MoveObject()
    {
        Vector3 targetPos = holdPoint.position;
        Vector3 currentPos = heldObj.transform.position;
        
        // Вместо AddForce используем расчет скорости для достижения точки.
        // Это убирает "раскачку" и дрожание.
        Vector3 velocity = (targetPos - currentPos) * followSpeed;
        
        // В новых версиях Unity (2023+) используй .linearVelocity
        // В старых используй .velocity
        heldObjRb.linearVelocity = velocity;

        // Проверка дистанции: если объект застрял и отстал — бросаем
        if (Vector3.Distance(currentPos, targetPos) > 2.0f) 
        {
            DropObject();
        }
    }
}