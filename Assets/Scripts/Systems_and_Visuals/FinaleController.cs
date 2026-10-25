using UnityEngine;
using System.Collections;

namespace KnocksFromBeneath
{

public class FinaleController : MonoBehaviour
{
    [Header("Ссылки на объекты сцены")]
    public Transform player;
    public GameObject monsterModel;
    public CanvasGroup fadeScreen;

    [Header("Точка падения в дыру")]
    public Transform holeBottomSpot;

    [Header("Звуки")]
    public AudioSource audioSource;
    public AudioClip evilLaughClip;
    public AudioClip nailingWoodClip;
    [SerializeField] private BasementHatch basementHatch;
    [SerializeField] private GameObject boardedVisual;

    private bool isEndingTriggered = false;

    void Awake() => FadeTransitionPresentation.Ensure(fadeScreen);

    public bool BeginBoarding(PlayerController pc, bool deadlineExpired)
    {
        if (isEndingTriggered || pc == null || basementHatch == null) return false;
        PlayerInventory inventory = pc.GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.hasHammer) return false;
        IntroSequence intro = FindFirstObjectByType<IntroSequence>();
        if (intro != null && (!intro.CanBoardBasement(basementHatch) || !intro.TryClaimEnding()))
            return false;
        isEndingTriggered = true;
        HorrorMomentDirector.Instance?.Interrupt();
        inventory.ActivateItem("Hammer");
        MonsterTimer.Instance?.StopTimer();
        QuestManager.Instance?.AddProgress(1);
        StartCoroutine(BoardingRoutine(pc, deadlineExpired));
        return true;
    }

    private void LockControls(PlayerController pc)
    {
        pc.isCameraLocked = true;
        var inventory = pc.GetComponent<PlayerInventory>();
        if (inventory != null) inventory.enabled = false;
        var interaction = pc.GetComponent<PlayerInteraction>();
        if (interaction != null) interaction.enabled = false;
        var door = pc.GetComponentInChildren<PlayerDoorOpen>();
        if (door != null) door.enabled = false;
        var pickup = pc.GetComponentInChildren<PickupController>();
        if (pickup != null) pickup.enabled = false;
        var bob = pc.GetComponentInChildren<HeadBobbing>();
        if (bob != null) bob.enabled = false;
    }

    private IEnumerator BoardingRoutine(PlayerController pc, bool deadlineExpired)
    {
        LockControls(pc);
        if (deadlineExpired)
        {
            basementHatch.OpenForCinematic();
            while (!basementHatch.IsOpen) yield return null;
        }
        if (audioSource != null && nailingWoodClip != null)
            audioSource.PlayOneShot(nailingWoodClip);
        yield return new WaitForSeconds(0.35f);
        if (deadlineExpired)
        {
            yield return PushedIntoHoleRoutine(pc);
            yield break;
        }

        // M-03 «Гвозди»: доски держат, только если игрок успеет забить гвозди, пока их не выбили.
        HatchNailingGame nailing = GetComponent<HatchNailingGame>();
        if (nailing == null) nailing = gameObject.AddComponent<HatchNailingGame>();
        bool boarded = false;
        yield return nailing.Play(pc, basementHatch, boardedVisual, nailingWoodClip, result => boarded = result);
        if (!boarded)
        {
            basementHatch.OpenForCinematic();
            for (float t = 0f; t < 2f && !basementHatch.IsOpen; t += Time.deltaTime) yield return null;
            yield return PushedIntoHoleRoutine(pc);
            yield break;
        }

        basementHatch.Seal();
        if (boardedVisual != null) boardedVisual.SetActive(true);
        if (QuestManager.Instance != null && QuestManager.Instance.questUiText != null)
            QuestManager.Instance.questUiText.text = "";
        if (ThoughtManager.Instance != null)
            ThoughtManager.Instance.ShowImportantThoughts(new[] {
                "Вот. Теперь не выберется.",
                "Тихо. Наконец-то тихо."
            }, ShowEnding);
        else ShowEnding();
    }

    IEnumerator PushedIntoHoleRoutine(PlayerController pc)
    {
        // 1. Отключаем управление игроком. isCameraLocked уже блокирует и ходьбу (см.
        // PlayerController.HandleMovement), но ~0.9 с между этим моментом и отключением
        // CharacterController в шаге 5 PlayerDoorOpen/PickupController оставались активны —
        // игрок мог зацепиться за дверь или бросить/поднять предмет посреди кат-сцены.
        LockControls(pc);

        Camera playerCam = pc.GetComponentInChildren<Camera>();
        CharacterController cc = pc.GetComponent<CharacterController>();

        // 2. Ставим монстра позади игрока
        Vector3 behindPlayer = pc.transform.position - pc.transform.forward * 2f;
        behindPlayer.y = pc.transform.position.y;
        if (monsterModel != null)
        {
            monsterModel.transform.position = behindPlayer;
            monsterModel.transform.LookAt(pc.transform.position);
            monsterModel.SetActive(true);
        }

        // 3. Звук
        if (audioSource && evilLaughClip)
            audioSource.PlayOneShot(evilLaughClip);

        yield return new WaitForSeconds(0.5f);

        // 4. Резко доворачиваем камеру на монстра.
        // Крутим мировой transform камеры, а не localRotation — камера дочерняя от игрока.
        if (playerCam != null)
        {
            float t = 0;
            Quaternion startRot = playerCam.transform.rotation;
            Vector3 lookPosition = monsterModel != null ? monsterModel.transform.position : pc.transform.position + pc.transform.forward;
            Vector3 dirToMonster = lookPosition + Vector3.up * 1.5f - playerCam.transform.position;
            Quaternion targetRot = Quaternion.LookRotation(dirToMonster);

            // 0.08f — очень быстро, чтобы получился резкий рывок
            while (t < 0.08f)
            {
                t += Time.deltaTime;
                playerCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, t / 0.08f);
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.3f);

        // 5. Роняем игрока в дыру — отключаем CharacterController на время падения
        if (cc != null) cc.enabled = false;

        float fallTime = 0f;
        float fallDuration = 0.6f;
        Vector3 startPos = pc.transform.position;

        while (fallTime < fallDuration)
        {
            fallTime += Time.deltaTime;
            float progress = fallTime / fallDuration;

            // Плавно движемся вниз, к дыре (ускоряющееся падение)
            Vector3 targetPosition = holeBottomSpot != null ? holeBottomSpot.position : startPos;
            pc.transform.position = Vector3.Lerp(startPos, targetPosition, progress * progress);
            yield return null;
        }
        if (holeBottomSpot != null)
            pc.transform.position = holeBottomSpot.position;
        basementHatch.Seal();
        if (boardedVisual != null) boardedVisual.SetActive(true);

        // 6. Затемнение экрана
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

        // Темноту держит камера, а не затемнение: пока оно на экране, FadeTransitionPresentation
        // прячет UI, глушит звук и не даёт листать реплики — финал E2 застревал на чёрном экране.
        if (playerCam != null)
        {
            playerCam.cullingMask = 0;
            playerCam.clearFlags = CameraClearFlags.SolidColor;
            playerCam.backgroundColor = Color.black;
        }
        if (fadeScreen != null)
            fadeScreen.alpha = 0f;

        // 7. Звук заколачивания досок
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

        PlayerController.CursorFree = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (ThoughtManager.Instance != null)
        {
            ThoughtManager.Instance.ShowImportantThoughts(new string[] {
                "Нет! Не закрывай люк!",
                "Выпусти меня, сука!"
            }, ShowEnding);
        }
        else
        {
            ShowEnding();
        }
    }

    void ShowEnding()
    {
        // T-21: раньше тут был Application.Quit() сразу после реплики — в билде игра просто
        // закрывалась без всякой карточки итога. Теперь как и у остальных трёх концовок —
        // экран "КОНЕЦ: ..." и возврат в главное меню (EndingScreen).
        if (EndingScreen.Instance != null)
            EndingScreen.Instance.ShowEnding("КОНЕЦ");
    }
}
}
