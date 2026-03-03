using UnityEngine;

public class PushObjects : MonoBehaviour
{
    public float pushPower = 5.0f; // Сила толчка (для массы 3 ставь 5-10)

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;

        // Если у объекта нет Rigidbody или он Kinematic — игнорируем
        if (body == null || body.isKinematic) return;

        // Не толкаем то, что под ногами (чтобы не взлетать как на лифте)
        if (hit.moveDirection.y < -0.3f) return;

        // Направление толчка только в стороны (X, Z)
        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);

        // Прикладываем силу
        body.AddForce(pushDir * pushPower, ForceMode.Impulse);
    }
}