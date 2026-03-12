using UnityEngine;
using System.Collections.Generic;

public class MonsterWatcherManager : MonoBehaviour
{
    public static MonsterWatcherManager Instance;

    public GameObject monsterPrefab; // Модель монстра
    public Transform[] spawnPoints;  // Точки, где он может появиться
    public float observationDuration = 5f; // Сколько он будет стоять

    private GameObject _activeMonster;

    void Awake() => Instance = this;

    public void SpawnWatcher(Vector3 playerPosition)
    {
        if (_activeMonster != null) return; // Не спавним, если он уже где-то стоит

        Transform bestPoint = GetClosestPoint(playerPosition);
        if (bestPoint != null)
        {
            _activeMonster = Instantiate(monsterPrefab, bestPoint.position, bestPoint.rotation);
            // Заставляем его смотреть на игрока (только по оси Y)
            Vector3 lookPos = playerPosition - _activeMonster.transform.position;
            lookPos.y = 0;
            _activeMonster.transform.rotation = Quaternion.LookRotation(lookPos);

            Destroy(_activeMonster, observationDuration); // Он исчезает, когда игрок отвлечется
        }
    }

    Transform GetClosestPoint(Vector3 playerPosition)
    {
        Transform closest = null;
        float minDist = float.MaxValue;

        foreach (Transform pt in spawnPoints)
        {
            float dist = Vector3.Distance(playerPosition, pt.position);
            // Точка должна быть достаточно близко, чтобы ее увидеть, но не в упор
            if (dist < minDist && dist > 2f)
            {
                minDist = dist;
                closest = pt;
            }
        }
        return closest;
    }
}