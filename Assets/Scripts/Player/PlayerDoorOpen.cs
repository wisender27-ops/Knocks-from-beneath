using UnityEngine;

public class PlayerDoorOpen : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    private float distance = 3.0f;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            // Создаем луч из центра экрана
            Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, distance))
            {
                Door door = hit.collider.GetComponent<Door>();
                if (door != null)
                {
                    if (!door.isOpen) { door.OpenDoor(); }
                    
                    else { door.CloseDoor(); }

                    return;
                }

                else { Debug.Log("door is null"); }
            }
        }
    }
}
