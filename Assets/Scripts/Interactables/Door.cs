using UnityEngine;

public class Door : MonoBehaviour
{
    [SerializeField] private float angleRotation = 90f;
    [SerializeField] private float openSpeed = 3f;

    private float baseRotation;
    private float targetRotation;
    public bool isOpen;

    void Start()
    {
        baseRotation = transform.localEulerAngles.y;
        targetRotation = baseRotation;
        isOpen = false;
    }

    void Update()
    {
        Vector3 euler = transform.localEulerAngles;

        float currentY = euler.y;
        float newY = Mathf.LerpAngle(currentY, targetRotation, openSpeed * Time.deltaTime);

        euler.y = newY;
        transform.localEulerAngles = euler;
    }

    public void OpenDoor()
    {
        targetRotation = baseRotation + angleRotation;
        isOpen = true;
    }

    public void CloseDoor()
    {
        targetRotation = baseRotation;
        isOpen = false;
    }
}