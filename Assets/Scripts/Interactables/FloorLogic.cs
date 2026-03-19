using UnityEngine;

public class FloorLogic : MonoBehaviour
{
    private Animator anim;
    private Collider myCollider;

    [Header("Визуал")]
    public GameObject visualModel; // Перетащи сюда объект с 3D-моделью досок

    [Header("Связи")]
    [SerializeField] private GameObject knockController; // Объект со скриптом RandomKnock
    [SerializeField] private GameObject holeEventTrigger; // Триггер для Этапа 3 (лицо/фонарик)
    [SerializeField] private AudioSource breakAudio;      // Звук хруста дерева

    [Header("Состояние пола")]
    public bool isBroken = false;

    void Start()
    {
        anim = GetComponent<Animator>();
        myCollider = GetComponent<Collider>();
    }

    public void Break()
    {
        if (isBroken) return;

        // 1. Анимация и физика
        if (anim != null) anim.SetTrigger("Break");
        if (myCollider != null) myCollider.enabled = false;

        // 2. Звуковой эффект
        if (breakAudio != null) breakAudio.Play();

        // 3. ОСТАНОВКА СТУКА (Важно для атмосферы)
        if (knockController != null)
        {
            knockController.SetActive(false);
        }

        // 4. ПОДГОТОВКА ЭТАПА 3
        if (holeEventTrigger != null)
        {
            // Включаем логику, которая начнет следить, светит ли игрок в дыру
            holeEventTrigger.SetActive(true);
        }

        if (visualModel != null) visualModel.SetActive(false); // Прячем доски

        isBroken = true;

        QuestManager.Instance.AddProgress(1);
    }

    public void Fix()
    {
        // Метод Fix нам понадобится в самом конце игры (Этап 4)
        if (!isBroken) return;

        if (anim != null) anim.SetTrigger("Fix");
        if (myCollider != null) myCollider.enabled = true;

        isBroken = false;
        Debug.Log("Пол заколочен монстром!");
    }
}