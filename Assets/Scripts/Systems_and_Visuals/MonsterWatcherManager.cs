using UnityEngine;
using System.Collections.Generic;

namespace KnocksFromBeneath
{

public class MonsterWatcherManager : MonoBehaviour
{
    public static MonsterWatcherManager Instance;

    public GameObject monsterPrefab; // Модель монстра
    public Transform[] spawnPoints;  // Точки, где он может появиться
    public float observationDuration = 5f; // Сколько он будет стоять

    [Tooltip("Случайные появления монстра (скрип двери и т.п.). Днём выключены и включаются "
             + "сами с наступлением ночи — на бытовых квестах дня скример ломает тон. "
             + "Поставь галочку вручную, если нужно, чтобы он появлялся всегда.")]
    public bool ambientWatchersEnabled = false;

    private GameObject _activeMonster;

    void Awake() => Instance = this;

    void OnEnable()
    {
        GameEvents.OnNightStarted += EnableAmbientWatchers;
        GameEvents.OnDayStarted += DisableAmbientWatchers;
    }

    void OnDisable()
    {
        GameEvents.OnNightStarted -= EnableAmbientWatchers;
        GameEvents.OnDayStarted -= DisableAmbientWatchers;
    }

    void EnableAmbientWatchers()
    {
        ambientWatchersEnabled = true;
    }

    void DisableAmbientWatchers()
    {
        ambientWatchersEnabled = false;
        // Дожил до утра — убираем, иначе фигура так и стоит в дневной сцене.
        if (_activeMonster != null) Destroy(_activeMonster);
    }

    // Случайная фигура нужна только для того, чтобы на неё посмотрели: снимаем с копии
    // коллайдеры, и кат-сцена захвата (она срабатывает по OnTriggerEnter) просто не
    // запустится. Это важно, потому что живёт копия по своему таймеру observationDuration
    // и может исчезнуть посреди кат-сцены — а возвращает управление игроку именно она.
    // Захват остаётся у сюжетного монстра в сцене, его мы не трогаем.
    static void MakeHarmless(GameObject monster)
    {
        if (monster == null) return;

        foreach (var col in monster.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        foreach (var body in monster.GetComponentsInChildren<Rigidbody>(true))
            body.isKinematic = true;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // forced=true — для сюжетных скримеров концовок (BranchEndingController/HideEndingController):
    // они обязаны показать монстра игроку, а не тихо промолчать, если в последние 5 сек
    // скрипнула дверь (ambientWatchersEnabled ещё не успел стать false после ambient-скрима)
    // или уже стоит эмбиент-копия (_activeMonster != null). Обычный эмбиент-вызов
    // (Door.cs) продолжает работать с гейтингом как раньше (forced=false по умолчанию).
    public GameObject SpawnWatcher(Vector3 playerPosition, bool forced = false)
    {
        if (!forced)
        {
            if (!ambientWatchersEnabled) return null; // днём монстра в доме быть не должно
            if (_activeMonster != null) return null; // Не спавним, если он уже где-то стоит
        }
        if (monsterPrefab == null || spawnPoints == null) return null;

        Transform bestPoint = GetClosestPoint(playerPosition);
        if (bestPoint == null) return null;

        GameObject monster = Instantiate(monsterPrefab, bestPoint.position, bestPoint.rotation);
        // Заставляем его смотреть на игрока (только по оси Y)
        Vector3 lookPos = playerPosition - monster.transform.position;
        lookPos.y = 0;
        if (lookPos.sqrMagnitude > 0.001f)
            monster.transform.rotation = Quaternion.LookRotation(lookPos);

        MakeHarmless(monster);
        Destroy(monster, observationDuration); // Он исчезает, когда игрок отвлечется

        if (!forced)
            _activeMonster = monster;

        return monster;
    }

    // Спавн в конкретной точке (T-20, концовка "прятки") — в отличие от SpawnWatcher,
    // не ищет ближайшую точку из spawnPoints, а ставит монстра ровно туда, куда указывает
    // автор (например, за дверью комнаты, где прятался игрок). Игнорирует _activeMonster —
    // это скриптованный финальный скример, а не повторяемый эмбиент-скример.
    public GameObject SpawnAt(Transform point, float duration)
    {
        if (point == null || monsterPrefab == null) return null;

        var monster = Instantiate(monsterPrefab, point.position, point.rotation);
        MakeHarmless(monster);
        Destroy(monster, duration);
        return monster;
    }

    Transform GetClosestPoint(Vector3 playerPosition)
    {
        // Раньше бралась просто ближайшая точка без проверки видимости — монстр мог
        // оказаться за стеной/за углом, и скример пропадал впустую. Теперь среди точек
        // с чистой видимостью берём ближайшую, а если видимых нет — fallback на ближайшую
        // вообще (лучше показать монстра не идеально, чем не показать совсем).
        Transform closestVisible = null;
        float minDistVisible = float.MaxValue;
        Transform closestAny = null;
        float minDistAny = float.MaxValue;

        foreach (Transform pt in spawnPoints)
        {
            if (pt == null) continue;
            float dist = Vector3.Distance(playerPosition, pt.position);
            // Точка должна быть достаточно близко, чтобы ее увидеть, но не в упор
            if (dist <= 2f) continue;

            if (dist < minDistAny)
            {
                minDistAny = dist;
                closestAny = pt;
            }

            if (dist < minDistVisible && HasLineOfSight(playerPosition, pt.position))
            {
                minDistVisible = dist;
                closestVisible = pt;
            }
        }

        return closestVisible != null ? closestVisible : closestAny;
    }

    static bool HasLineOfSight(Vector3 playerPosition, Vector3 targetPosition)
    {
        Vector3 from = playerPosition + Vector3.up * 1.6f; // примерная высота глаз
        Vector3 to = targetPosition + Vector3.up * 1.0f;
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist <= 0.01f) return true;

        // Луч чуть короче полной дистанции, чтобы не цепляться за коллайдер самой точки.
        return !Physics.Raycast(from, delta / dist, dist - 0.1f, ~0, QueryTriggerInteraction.Ignore);
    }
}
}
