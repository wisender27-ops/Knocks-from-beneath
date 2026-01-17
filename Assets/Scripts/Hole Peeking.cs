using UnityEngine;

public class HolePeeping : MonoBehaviour
{
    public Camera mainCamera;          // Основная камера игрока
    public Transform peepPoint;       // Точка у дырки (куда ставим камеру)
    public float sensitivity = 2f;    // Чувствительность мыши
    
    private bool isPeeping = false;
    private Vector3 originalCamPos;
    private Quaternion originalCamRot;
    
    private float rotX, rotY;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && IsPlayerNear()) // Добавь свою проверку дистанции
        {
            TogglePeeping();
        }

        if (isPeeping)
        {
            HandleRotation();
        }
    }

    void TogglePeeping()
    {
        isPeeping = !isPeeping;

        if (isPeeping)
        {
            // Сохраняем позицию и выключаем управление персонажем
            originalCamPos = mainCamera.transform.position;
            originalCamRot = mainCamera.transform.rotation;
            
            mainCamera.transform.position = peepPoint.position;
            // Можно добавить скрипт отключения движения игрока здесь
        }
        else
        {
            // Возвращаем всё назад
            mainCamera.transform.position = originalCamPos;
            mainCamera.transform.rotation = originalCamRot;
        }
    }

    void HandleRotation()
    {
        rotX += Input.GetAxis("Mouse X") * sensitivity;
        rotY -= Input.GetAxis("Mouse Y") * sensitivity;

        // Ограничиваем углы обзора, чтобы не вывернуться назад
        rotY = Mathf.Clamp(rotY, -40f, 40f); 
        rotX = Mathf.Clamp(rotX, -60f, 60f);

        mainCamera.transform.localRotation = Quaternion.Euler(rotY, rotX, 0);
    }
    
    bool IsPlayerNear() {
        // Здесь логика проверки: через Trigger Enter/Exit или Raycast
        return true; 
    }
}