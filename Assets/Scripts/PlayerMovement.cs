using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public CharacterController controller;
    public float speed = 12f;
    public float gravity = -9.81f;
    
    Vector3 velocity;

    void Update()
    {
        // Получаем ввод от WASD (или стрелок)
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        // Движение относительно направления, куда смотрит игрок
        Vector3 move = transform.right * x + transform.forward * z;

        controller.Move(move * speed * Time.deltaTime);

        // Простая реализация гравитации
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}