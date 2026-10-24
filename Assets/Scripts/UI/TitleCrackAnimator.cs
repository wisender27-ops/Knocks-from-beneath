using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace KnocksFromBeneath
{

// Оживляет заголовок главного меню: трещина между словами прорастает от центра к краям,
// пункты меню проявляются по очереди, затем снизу время от времени стучат серией неровных ударов.
// Порядок роста трещины - порядок дочерних блоков в иерархии.
[RequireComponent(typeof(AudioSource))]
public sealed class TitleCrackAnimator : MonoBehaviour
{
    [Header("Появление")]
    [SerializeField] private float startDelay = .35f;
    [SerializeField] private float revealDuration = .75f;
    [SerializeField] private CanvasGroup[] menuItems;
    [SerializeField] private float menuItemStagger = .18f;
    [SerializeField] private float menuItemFade = .45f;

    [Header("Стук")]
    [SerializeField] private RectTransform[] shakeTargets;
    [SerializeField] private Graphic[] flickerTargets;
    [SerializeField] private AudioClip[] knockClips;
    [SerializeField, Range(0f, 1f)] private float knockVolume = .8f;
    [SerializeField] private float idleMin = 9f;
    [SerializeField] private float idleMax = 20f;
    [SerializeField] private float tremorDuration = .3f;
    [SerializeField] private float tremorStrength = 4f;

    [Header("Клик игрока")]
    [SerializeField] private bool answerPlayerKnocks = true;
    [SerializeField] private float answerAfterQuiet = 1.2f;
    [SerializeField] private int maxAnswerKnocks = 3;

    private bool ready, knocking;
    private int playerKnocks;
    private float lastPlayerKnock;
    private Coroutine answerRoutine;
    private RectTransform crack;
    private Graphic[] blocks;
    private AudioSource source;
    private Vector2 crackRest;
    private Vector2[] shakeRest;

    void Awake()
    {
        crack = (RectTransform)transform;
        crackRest = crack.anchoredPosition;
        shakeRest = new Vector2[shakeTargets.Length];
        for (int i = 0; i < shakeTargets.Length; i++)
            if (shakeTargets[i] != null) shakeRest[i] = shakeTargets[i].anchoredPosition;
        blocks = GetComponentsInChildren<Graphic>(true);
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    void OnEnable()
    {
        ready = knocking = false;
        playerKnocks = 0;
        answerRoutine = null;
        SetCrackVisible(0f);
        for (int i = 0; i < menuItems.Length; i++)
            if (menuItems[i] != null) menuItems[i].alpha = 0f;
        StartCoroutine(Run());
    }

    void OnDisable()
    {
        if (blocks == null) return;
        Restore();
        SetCrackVisible(1f);
        for (int i = 0; i < menuItems.Length; i++)
            if (menuItems[i] != null) menuItems[i].alpha = 1f;
    }

    private IEnumerator Run()
    {
        yield return Wait(startDelay);
        StartCoroutine(RevealMenuItems());
        for (float t = 0f; t < revealDuration; t += Time.unscaledDeltaTime)
        {
            SetCrackVisible(t / revealDuration);
            yield return null;
        }
        SetCrackVisible(1f);
        ready = true;
        yield return Knock(1f);

        while (true)
        {
            yield return Wait(Random.Range(idleMin, idleMax));
            int knocks = Random.value < .5f ? 2 : 3;
            for (int i = 0; i < knocks; i++)
            {
                yield return Knock(Random.Range(.75f, 1.15f));
                yield return Wait(Random.Range(.25f, .6f));
            }
        }
    }

    private IEnumerator RevealMenuItems()
    {
        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null) continue;
            StartCoroutine(FadeIn(menuItems[i]));
            yield return Wait(menuItemStagger);
        }
    }

    private IEnumerator FadeIn(CanvasGroup group)
    {
        for (float t = 0f; t < menuItemFade; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / menuItemFade;
            yield return null;
        }
        group.alpha = 1f;
    }

    // Вызывается кликом по названию. Во время удара клики игнорируются, это и есть ограничение частоты.
    public void KnockByPlayer()
    {
        if (!ready || knocking) return;
        playerKnocks = Mathf.Min(playerKnocks + 1, maxAnswerKnocks);
        lastPlayerKnock = Time.unscaledTime;
        StartCoroutine(Knock(1f));
        if (answerPlayerKnocks && answerRoutine == null) answerRoutine = StartCoroutine(AnswerWhenQuiet());
    }

    // Игрок постучал и затих: снизу отвечают тем же числом ударов, глуше и сильнее.
    private IEnumerator AnswerWhenQuiet()
    {
        while (Time.unscaledTime - lastPlayerKnock < answerAfterQuiet || knocking) yield return null;
        int count = playerKnocks;
        playerKnocks = 0;
        for (int i = 0; i < count; i++)
        {
            yield return Knock(1.3f, true);
            yield return Wait(Random.Range(.4f, .7f));
        }
        answerRoutine = playerKnocks > 0 ? StartCoroutine(AnswerWhenQuiet()) : null;   // игрок успел стукнуть во время ответа
    }

    // Один удар: звук, дрожание заголовка и трещины, короткое мерцание названия.
    private IEnumerator Knock(float strength, bool deep = false)
    {
        while (knocking) yield return null;
        knocking = true;
        if (knockClips != null && knockClips.Length > 0)
            source.PlayOneShot(knockClips[deep ? 0 : Random.Range(0, knockClips.Length)], knockVolume * Mathf.Clamp01(strength));
        SetFlicker(.35f);
        for (float t = 0f; t < tremorDuration; t += Time.unscaledDeltaTime)
        {
            float decay = 1f - t / tremorDuration;
            float wave = Mathf.Sin(t * 75f) * tremorStrength * strength * decay;
            crack.anchoredPosition = crackRest + new Vector2(0f, wave);
            for (int i = 0; i < shakeTargets.Length; i++)
                if (shakeTargets[i] != null) shakeTargets[i].anchoredPosition = shakeRest[i] + new Vector2(0f, wave * .4f);
            if (t > .06f) SetFlicker(1f);
            yield return null;
        }
        Restore();
        knocking = false;
    }

    private void Restore()
    {
        crack.anchoredPosition = crackRest;
        for (int i = 0; i < shakeTargets.Length; i++)
            if (shakeTargets[i] != null) shakeTargets[i].anchoredPosition = shakeRest[i];
        SetFlicker(1f);
    }

    private void SetFlicker(float alpha)
    {
        if (flickerTargets == null) return;
        for (int i = 0; i < flickerTargets.Length; i++)
            if (flickerTargets[i] != null) flickerTargets[i].canvasRenderer.SetAlpha(alpha);
    }

    // progress 0..1: блоки до текущего видны полностью, растущий - частично.
    private void SetCrackVisible(float progress)
    {
        float position = progress * blocks.Length;
        for (int i = 0; i < blocks.Length; i++)
            blocks[i].canvasRenderer.SetAlpha(Mathf.Clamp01(position - i));
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }
}
}
