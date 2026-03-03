using UnityEngine;

public class MonsterGrab : MonoBehaviour
{
    public Animator monsterAnimator;
    public Transform grabPoint;
    public float grabRange = 2.5f;

    private GameObject player;
    private bool isGrabbing = false;

    void Start()
    {
        // Ищем игрока в начале игры по тегу
        player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            Debug.LogError("Монстр не нашел объект с тегом Player!");
        }
    }

    void Update()
    {
        if (player == null || isGrabbing) return;

        float distance = Vector3.Distance(transform.position, player.transform.position);

        if (distance < grabRange)
        {
            StartGrab();
        }
    }

    void StartGrab()
    {
        isGrabbing = true;
        monsterAnimator.SetTrigger("StartGrab");

        // Отключаем управление и физику игрока
        var controller = player.GetComponent<CharacterController>();
        if (controller) controller.enabled = false;

        var rb = player.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        // Привязываем к руке
        player.transform.SetParent(grabPoint);
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;
    }

    // Вызывается через Animation Event в конце броска
    public void ReleasePlayer()
    {
        player.transform.SetParent(null);

        var controller = player.GetComponent<CharacterController>();
        if (controller) controller.enabled = true;

        var rb = player.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.isKinematic = false;
            // Добавим импульс броска вперед и вниз
            rb.AddForce(transform.forward * 10f + Vector3.down * 5f, ForceMode.Impulse);
        }

        isGrabbing = false;
    }
}