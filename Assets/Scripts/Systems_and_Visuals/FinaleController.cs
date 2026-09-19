using UnityEngine;
using System.Collections;

public class FinaleController : MonoBehaviour
{
    [Header("������ �� �������")]
    public Transform player;
    public GameObject monsterModel;
    public CanvasGroup fadeScreen;

    [Header("��������� �����")]
    public Transform holeBottomSpot;

    [Header("�����")]
    public AudioSource audioSource;
    public AudioClip evilLaughClip;
    public AudioClip nailingWoodClip;

    private bool isEndingTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isEndingTriggered || other == null)
            return;

        Transform playerRoot = other.transform.root;
        if (playerRoot == null || !playerRoot.CompareTag("Player"))
            return;

        if (!isEndingTriggered)
        {
            PlayerInventory inventory = playerRoot.GetComponent<PlayerInventory>();
            PlayerController pc = playerRoot.GetComponent<PlayerController>();

            if (inventory != null && inventory.hasHammer && pc != null)
            {
                isEndingTriggered = true;
                inventory.ActivateItem("Hammer");
                if (QuestManager.Instance != null)
                    QuestManager.Instance.AddProgress(1);
                StartCoroutine(PushedIntoHoleRoutine(pc));
                if (MonsterTimer.Instance != null)
                    MonsterTimer.Instance.StopTimer();
            }

        }
    }

    IEnumerator PushedIntoHoleRoutine(PlayerController pc)
    {
        // 1. ��������� ����������
        pc.isCameraLocked = true;
        if (pc.GetComponent<PlayerInventory>() != null) pc.GetComponent<PlayerInventory>().enabled = false;
        if (pc.GetComponent<PlayerInteraction>() != null) pc.GetComponent<PlayerInteraction>().enabled = false;

        Camera playerCam = pc.GetComponentInChildren<Camera>();
        CharacterController cc = pc.GetComponent<CharacterController>();

        // 2. ������ ������� �� ������ ����� ������
        Vector3 behindPlayer = pc.transform.position - pc.transform.forward * 2f;
        behindPlayer.y = pc.transform.position.y;
        if (monsterModel != null)
        {
            monsterModel.transform.position = behindPlayer;
            monsterModel.transform.LookAt(pc.transform.position);
            monsterModel.SetActive(true);
        }

        // 3. ����
        if (audioSource && evilLaughClip)
            audioSource.PlayOneShot(evilLaughClip);

        yield return new WaitForSeconds(0.5f);

        // 4. ������ �������� ������ �� �������
        // ������ ������ ������ ����� localRotation, �� ������� transform ������
        if (playerCam != null)
        {
            float t = 0;
            Quaternion startRot = playerCam.transform.rotation;
            Vector3 lookPosition = monsterModel != null ? monsterModel.transform.position : pc.transform.position + pc.transform.forward;
            Vector3 dirToMonster = lookPosition + Vector3.up * 1.5f - playerCam.transform.position;
            Quaternion targetRot = Quaternion.LookRotation(dirToMonster);

            // 0.08f � ����� ������, ����� �����
            while (t < 0.08f)
            {
                t += Time.deltaTime;
                playerCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t / 0.08f);
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.3f);

        // 5. ������� � ��� � ��������� CharacterController � ������ ������ �������
        if (cc != null) cc.enabled = false;

        float fallTime = 0f;
        float fallDuration = 0.6f;
        Vector3 startPos = pc.transform.position;

        while (fallTime < fallDuration)
        {
            fallTime += Time.deltaTime;
            float progress = fallTime / fallDuration;

            // ������� ���� � ���������� (�������� ����������)
            Vector3 targetPosition = holeBottomSpot != null ? holeBottomSpot.position : startPos;
            pc.transform.position = Vector3.Lerp(startPos, targetPosition, progress * progress);
            yield return null;
        }
        if (holeBottomSpot != null)
            pc.transform.position = holeBottomSpot.position;

        // 6. ����������
        float elapsed = 0;
        while (elapsed < 0.8f)
        {
            elapsed += Time.deltaTime;
            if (fadeScreen != null)
                fadeScreen.alpha = elapsed / 0.8f;
            yield return null;
        }
        if (fadeScreen != null)
            fadeScreen.alpha = 1f;

        if (monsterModel != null)
            monsterModel.SetActive(false);

        // 7. ���� �������������
        yield return new WaitForSeconds(0.5f);
        if (audioSource && nailingWoodClip)
        {
            audioSource.clip = nailingWoodClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        if (QuestManager.Instance != null && QuestManager.Instance.questUiText != null)
            QuestManager.Instance.questUiText.text = "";

        yield return new WaitForSeconds(3f);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (ThoughtManager.Instance != null)
        {
            ThoughtManager.Instance.ShowThoughts(new string[] {
                "НЕТ!",
                "ВЫПУСТИ МЕНЯ!"
            }, QuitGame);
        }
    }

    void QuitGame()
    {
        Debug.Log("GAME OVER");
        // Application.Quit();
    }
}
