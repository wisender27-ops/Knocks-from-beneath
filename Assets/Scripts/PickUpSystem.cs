using UnityEngine;

public class PickUpSystem : MonoBehaviour
{
    public Transform holdPoint;
    public float pickUpRange = 3f;
    private GameObject heldObj;
    private Rigidbody heldObjRb;
    private PickableItem heldItemScript; // Ссылка на скрипт на самой коробке
    private int originalLayer; // Переменная для хранения старого слоя

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObj == null)
            {
                // ЛОГИКА ПОДБОРА (старая)
                RaycastHit hit;
                if (Physics.Raycast(transform.position, transform.forward, out hit, pickUpRange))
                {
                    if (hit.transform.CompareTag("Pickable"))
                    {
                        PickUpObject(hit.transform.gameObject);
                    }
                }
            }
            else
            {
                // ЛОГИКА УСТАНОВКИ
                // Спрашиваем у коробки: "Ты в зоне?"
                if (heldItemScript != null && heldItemScript.activeZone != null)
                {
                    if (heldItemScript.activeZone.TryPlaceBox(heldObj))
                    {
                        heldObj = null;
                        heldItemScript = null;
                        return;
                    }
                }
                
                // Если не в зоне — просто бросаем
                DropObject();
            }
        }

        if (heldObj != null) MoveObject();
    }

    void PickUpObject(GameObject pickObj)
    {
        heldObj = pickObj;
        heldObjRb = pickObj.GetComponent<Rigidbody>();
        heldItemScript = pickObj.GetComponent<PickableItem>();

        // 1. Запоминаем текущий слой объекта (например, Default)
        originalLayer = heldObj.layer;
        
        // 2. Меняем слой на HeldItem (который не сталкивается с игроком)
        // LayerMask.NameToLayer вернет номер слоя по его названию
        heldObj.layer = LayerMask.NameToLayer("HeldItem");

        heldObjRb.useGravity = false;
        heldObjRb.linearDamping = 10;
        heldObjRb.constraints = RigidbodyConstraints.FreezeRotation;
        heldObj.transform.parent = holdPoint;
    }

    void DropObject()
    {
        if (heldObj == null) return;

        // 3. Возвращаем оригинальный слой, чтобы предмет снова можно было толкать
        heldObj.layer = originalLayer;

        heldObjRb.useGravity = true;
        heldObjRb.linearDamping = 1;
        heldObjRb.constraints = RigidbodyConstraints.None;
        heldObj.transform.parent = null;
        heldObj = null;
        heldItemScript = null;
    }

    void MoveObject()
    {
        if (Vector3.Distance(heldObj.transform.position, holdPoint.position) > 0.1f)
        {
            Vector3 moveDirection = (holdPoint.position - heldObj.transform.position);
            heldObjRb.AddForce(moveDirection * 150f);
        }
    }
}