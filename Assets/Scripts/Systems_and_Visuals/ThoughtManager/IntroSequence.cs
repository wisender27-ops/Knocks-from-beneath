using UnityEngine;
using System.Collections;

public class IntroSequence : MonoBehaviour
{
    [Header("Настройки перехода ночи")]
    public SkyboxSwitcher skySwitcher;
    public CanvasGroup fadeScreen;
    public GameObject nightTrigger;
    public Transform playerTransform;
    public Vector3 nightSpawnPosition;
    public Vector3 nightSpawnRotation;

    void Start()
    {
        if (QuestManager.Instance.questUiText != null)
            QuestManager.Instance.questUiText.text = "";

        Invoke(nameof(StartIntro), 1.0f);
    }

    // --- ЦЕПОЧКА 1: МУСОР ---
    void StartIntro()
    {
        string[] storyLines = { "Наконец-то я дома...", "Нужно обживаться... И воняет знатно, надо бы вынести этот мусор." };
        ThoughtManager.Instance.ShowThoughts(storyLines, SetupTrashQuest);
    }

    void SetupTrashQuest() => QuestManager.Instance.CreateQuest("Вынести мусор", 1, OnTrashFinished);

    // --- ЦЕПОЧКА 2: КОРОБКА ---
    public void OnTrashFinished()
    {
        string[] reactionLines = { "Фух, одной проблемой меньше.", "Так, теперь та коробка у входа... Нужно перетащить её в гараж." };
        ThoughtManager.Instance.ShowThoughts(reactionLines, SetupBoxQuest);
    }

    void SetupBoxQuest() => QuestManager.Instance.CreateQuest("Отнести коробку в гараж", 1, OnBoxFinished);

    // --- ЦЕПОЧКА 3: СОН ---
    public void OnBoxFinished()
    {
        string[] sleepLines = { "Всё, на сегодня хватит.", "Смертельно устал. Пора ложиться спать." };
        // После этих мыслей запускаем переход в ночь
        ThoughtManager.Instance.ShowThoughts(sleepLines, () => StartCoroutine(NightRoutine()));
    }

    IEnumerator NightRoutine()
    {
        // 1. Fade Out
        float elapsed = 0;
        while (elapsed < 1.5f)
        {
            elapsed += Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        // --- МОМЕНТ ТЕМНОТЫ ---
        if (skySwitcher != null) skySwitcher.isDayTime = false;

        if (playerTransform != null)
        {
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            playerTransform.position = nightSpawnPosition;
            playerTransform.rotation = Quaternion.Euler(nightSpawnRotation);
            if (cc != null) cc.enabled = true;
        }

        yield return new WaitForSeconds(3f); // Время в темноте (можно вставить текст "Прошло 4 часа...")

        if (nightTrigger != null) nightTrigger.SetActive(true);

        // 2. Fade In
        while (elapsed > 0)
        {
            elapsed -= Time.deltaTime;
            fadeScreen.alpha = elapsed / 1.5f;
            yield return null;
        }

        // Мысли после пробуждения от стука
        string[] wakeLines = { "...Что это было?", "Я слышал какой-то звук... Снизу?" };
        ThoughtManager.Instance.ShowThoughts(wakeLines);
    }
}