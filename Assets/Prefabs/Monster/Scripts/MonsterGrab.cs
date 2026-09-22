using UnityEngine;
using System.Collections.Generic;

public class CinematicMonsterGrab : MonoBehaviour
{
    [Header("Monster")]
    public Animator monsterAnimator;

    [Header("References")]
    public Camera mainCam;
    public Transform grabCamStart; // Куда камера переместится в начале
    public Transform grabCamEnd;   // Куда камера придет в конце
    public Transform lookTarget;   // Куда всегда смотреть

    [Header("Timing")]
    public float moveDuration = 1.2f;
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Perlin Shake Settings")]
    public float shakeAmplitude = 0.1f;
    public float shakeFrequency = 10.0f;

    private bool isPlaying = false;
    private float timer = 0f;
    private bool hasTriggered = false;
    private float seed;
    private GameObject activePlayer;
    private readonly List<MonoBehaviour> disabledPlayerScripts = new List<MonoBehaviour>();
    private Vector3 cameraPositionBeforeCinematic;
    private Quaternion cameraRotationBeforeCinematic;
    private bool cameraTransformCaptured;

    void OnDisable()
    {
        if (isPlaying)
            StopCinematic();
    }

    // Объект могут уничтожить посреди сцены (MonsterWatcherManager убирает фигуру по
    // своему таймеру) — управление игроку надо вернуть в любом случае.
    void OnDestroy()
    {
        if (isPlaying)
            StopCinematic();
    }

    void Awake() => seed = Random.value * 100f;

    // Используем LateUpdate, чтобы перезаписать положение камеры ПОСЛЕ того, 
    // как отработали стандартные скрипты игрока и CameraRoot.
    void LateUpdate()
    {
        if (!isPlaying) return;
        if (mainCam == null || grabCamStart == null || grabCamEnd == null)
        {
            StopCinematic();
            return;
        }

        timer += Time.deltaTime;
        float t = moveDuration > 0f ? Mathf.Clamp01(timer / moveDuration) : 1f;
        float curvedT = moveCurve != null ? moveCurve.Evaluate(t) : t;

        // 1. Считаем позицию (в мировых координатах)
        Vector3 targetPos = Vector3.Lerp(grabCamStart.position, grabCamEnd.position, curvedT);

        // 2. Добавляем шум Перлина
        float nX = (Mathf.PerlinNoise(seed + Time.time * shakeFrequency, 0f) - 0.5f) * 2f;
        float nY = (Mathf.PerlinNoise(0f, seed + Time.time * shakeFrequency) - 0.5f) * 2f;
        Vector3 shake = new Vector3(nX, nY, 0) * shakeAmplitude;

        // 3. Принудительно ставим камеру (игнорируя иерархию)
        mainCam.transform.position = targetPos + shake;

        // 4. Всегда смотрим на цель
        if (lookTarget != null)
        {
            mainCam.transform.LookAt(lookTarget);
        }

        if (t >= 1f)
        {
            StopCinematic();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || isPlaying || other == null) return;

        Transform playerRoot = other.transform.root;
        if (playerRoot == null || !playerRoot.CompareTag("Player")) return;

        hasTriggered = true;
        activePlayer = playerRoot.gameObject;
        if (mainCam == null)
            mainCam = Camera.main;
        if (mainCam != null)
        {
            cameraPositionBeforeCinematic = mainCam.transform.position;
            cameraRotationBeforeCinematic = mainCam.transform.rotation;
            cameraTransformCaptured = true;
        }

        if (monsterAnimator != null) monsterAnimator.SetTrigger("StartGrab");

        SetPlayerControl(activePlayer, false);

        isPlaying = true;
        timer = 0f;
    }

    public void StopCinematic()
    {
        isPlaying = false;

        if (cameraTransformCaptured && mainCam != null)
        {
            mainCam.transform.SetPositionAndRotation(cameraPositionBeforeCinematic, cameraRotationBeforeCinematic);
            cameraTransformCaptured = false;
        }

        GameObject player = activePlayer != null ? activePlayer : GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            SetPlayerControl(player, true);

        activePlayer = null;
    }

    private void SetPlayerControl(GameObject player, bool state)
    {
        if (player == null) return;

        var controller = player.GetComponent<CharacterController>();
        if (!state)
        {
            disabledPlayerScripts.Clear();
            if (controller != null) controller.enabled = false;

            MonoBehaviour[] scripts = player.GetComponentsInChildren<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script != this && !(script is Camera) && script.enabled)
                {
                    script.enabled = false;
                    disabledPlayerScripts.Add(script);
                }
            }
            return;
        }

        // Раньше здесь возвращалось запомненное значение. Если на момент захвата контроллер
        // уже был выключен (другая кат-сцена, телепорт в кровать, повторный вход в триггер),
        // запоминалось false — и игрок навсегда оставался с работающими скриптами движения,
        // но выключенным CharacterController: камера крутится, шаг не делается, а консоль
        // каждый кадр пишет "Move called on inactive controller". Кат-сцена не имеет права
        // оставить игрока без возможности ходить.
        if (controller != null)
            controller.enabled = true;

        foreach (var script in disabledPlayerScripts)
        {
            if (script != null)
                script.enabled = true;
        }
        disabledPlayerScripts.Clear();
    }
}

// Compatibility class for prefabs serialized with the original file name.
public sealed class MonsterGrab : CinematicMonsterGrab
{
}
