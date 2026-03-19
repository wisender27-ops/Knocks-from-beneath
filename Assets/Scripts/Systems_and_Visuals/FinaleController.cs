using UnityEngine;
using System.Collections;

public class FinaleController : MonoBehaviour
{
    [Header("Ссылки на объекты")]
    public Transform player;
    public GameObject monsterModel;
    public CanvasGroup fadeScreen;

    [Header("Настройки сцены")]
    public Transform holeBottomSpot;

    [Header("Аудио")]
    public AudioSource audioSource;
    public AudioClip evilLaughClip;
    public AudioClip nailingWoodClip;

    private bool isEndingTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isEndingTriggered)
        {
            PlayerInventory inventory = other.GetComponent<PlayerInventory>();
            PlayerController pc = other.GetComponent<PlayerController>();

            if (inventory != null && inventory.hasHammer && pc != null)
            {
                isEndingTriggered = true;
                inventory.ActivateItem("Hammer");
                QuestManager.Instance.AddProgress(1);
                StartCoroutine(PushedIntoHoleRoutine(pc));
            }
        }
    }

    IEnumerator PushedIntoHoleRoutine(PlayerController pc)
    {
        // 1. БЛОКИРУЕМ УПРАВЛЕНИЕ
        pc.isCameraLocked = true;
        if (pc.GetComponent<PlayerInventory>() != null) pc.GetComponent<PlayerInventory>().enabled = false;
        if (pc.GetComponent<PlayerInteraction>() != null) pc.GetComponent<PlayerInteraction>().enabled = false;

        Camera playerCam = pc.GetComponentInChildren<Camera>();
        CharacterController cc = pc.GetComponent<CharacterController>();

        // 2. СТАВИМ МОНСТРА ЗА СПИНОЙ ОЧЕНЬ БЛИЗКО
        Vector3 behindPlayer = pc.transform.position - pc.transform.forward * 2f;
        behindPlayer.y = pc.transform.position.y;
        monsterModel.transform.position = behindPlayer;
        monsterModel.transform.LookAt(pc.transform.position);
        monsterModel.SetActive(true);

        // 3. СМЕХ
        if (audioSource && evilLaughClip)
            audioSource.PlayOneShot(evilLaughClip);

        yield return new WaitForSeconds(0.5f);

        // 4. РЕЗКИЙ РАЗВОРОТ КАМЕРЫ НА МОНСТРА
        // Крутим только камеру через localRotation, не трогаем transform игрока
        if (playerCam != null)
        {
            float t = 0;
            Quaternion startRot = playerCam.transform.rotation;
            Vector3 dirToMonster = monsterModel.transform.position + Vector3.up * 1.5f - playerCam.transform.position;
            Quaternion targetRot = Quaternion.LookRotation(dirToMonster);

            // 0.08f — очень быстро, почти резко
            while (t < 0.08f)
            {
                t += Time.deltaTime;
                playerCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t / 0.08f);
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.3f);

        // 5. ПАДЕНИЕ В ЯМУ — отключаем CharacterController и роняем игрока физикой
        if (cc != null) cc.enabled = false;

        float fallTime = 0f;
        float fallDuration = 0.6f;
        Vector3 startPos = pc.transform.position;

        while (fallTime < fallDuration)
        {
            fallTime += Time.deltaTime;
            float progress = fallTime / fallDuration;

            // Двигаем вниз с ускорением (имитация гравитации)
            pc.transform.position = Vector3.Lerp(startPos, holeBottomSpot.position, progress * progress);
            yield return null;
        }
        pc.transform.position = holeBottomSpot.position;

        // 6. ЗАТЕМНЕНИЕ
        float elapsed = 0;
        while (elapsed < 0.8f)
        {
            elapsed += Time.deltaTime;
            fadeScreen.alpha = elapsed / 0.8f;
            yield return null;
        }
        fadeScreen.alpha = 1f;

        monsterModel.SetActive(false);

        // 7. ЗВУК ЗАКОЛАЧИВАНИЯ
        yield return new WaitForSeconds(0.5f);
        if (audioSource && nailingWoodClip)
        {
            audioSource.clip = nailingWoodClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (QuestManager.Instance != null)
            QuestManager.Instance.questUiText.text = "";

        yield return new WaitForSeconds(3f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ThoughtManager.Instance.ShowThoughts(new string[] {
        "Нет... Пожалуйста...",
        "ОН ЗАКОЛОТИЛ МЕНЯ ЗДЕСЬ."
    }, QuitGame);
    }

    void QuitGame()
    {
        Debug.Log("GAME OVER");
        // Application.Quit();
    }
}