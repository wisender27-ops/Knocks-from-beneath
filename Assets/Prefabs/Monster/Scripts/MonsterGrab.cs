using UnityEngine;

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

    void Awake() => seed = Random.value * 100f;

    // Используем LateUpdate, чтобы перезаписать положение камеры ПОСЛЕ того, 
    // как отработали стандартные скрипты игрока и CameraRoot.
    void LateUpdate()
    {
        if (!isPlaying) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / moveDuration);
        float curvedT = moveCurve.Evaluate(t);

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
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;
            if (monsterAnimator != null) monsterAnimator.SetTrigger("StartGrab");

            // Блокируем скрипты игрока (WASD)
            SetPlayerControl(other.gameObject, false);

            isPlaying = true;
            timer = 0f;
        }
    }

    public void StopCinematic()
    {
        isPlaying = false;
        // Просто возвращаем управление. Камера сама "приклеится" обратно к CameraRoot, 
        // так как мы перестали перезаписывать её позицию в LateUpdate.
        if (GameObject.FindGameObjectWithTag("Player") != null)
        {
            SetPlayerControl(GameObject.FindGameObjectWithTag("Player"), true);
        }
    }

    private void SetPlayerControl(GameObject player, bool state)
    {
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = state;

        // Отключаем все скрипты кроме этого, чтобы игрок не дергал камеру мышкой
        MonoBehaviour[] scripts = player.GetComponentsInChildren<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script != this && !(script is Camera))
            {
                script.enabled = state;
            }
        }
    }
}