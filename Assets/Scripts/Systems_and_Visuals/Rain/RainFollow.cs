using UnityEngine;

public class RainFollow : MonoBehaviour
{
    public Transform player; // Сюда в инспекторе перетащи игрока
    public float height = 15f; // Высота, на которой висит "туча"

    void LateUpdate()
    {
        if (player != null)
        {
            // Двигаем дождь за игроком, но сохраняем высоту
            transform.position = new Vector3(player.position.x, player.position.y + height, player.position.z);
        }
    }
}