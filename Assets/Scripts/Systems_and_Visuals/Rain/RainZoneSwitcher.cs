using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

namespace KnocksFromBeneath
{

// Триггеры расставлены парами прямо в дверных проёмах — две тонкие "шторки" почти
// впритык друг к другу (Trigger_Inside/Trigger_Outside): когда игрок идёт через дверь,
// он пересекает обе почти одновременно. Раньше тут был общий статический счётчик
// "игрок внутри ЛЮБОЙ зоны", рассчитанный на дедупликацию нескольких коллайдеров одной
// и той же зоны — но Inside и Outside это ДВЕ РАЗНЫЕ зоны с противоположным смыслом,
// и из-за общего счётчика при проходе через дверь срабатывал Exit чужого триггера
// (с его exitSnapshot), а не Enter того триггера, в который игрок реально зашёл —
// поэтому звук дождя переключался наоборот (пропадал снаружи, появлялся внутри).
// Правильное поведение проще: последний сработавший Enter и определяет текущее
// состояние, Exit тут вообще не нужен — противоположная шторка уже применит свой Enter.
public class RainZoneSwitcher : MonoBehaviour
{
    [Header("Audio")]
    public AudioMixerSnapshot enterSnapshot;
    public AudioMixerSnapshot exitSnapshot;
    public float transitionTime = 3f;

    [Header("Fog")]
    public float enterFogDensity;
    public float exitFogDensity;

    // Локальный трекинг (в рамках этого триггера), чтобы не дублировать Enter, если у
    // игрока несколько коллайдеров.
    private readonly HashSet<Collider> _playerCollidersInsideThisTrigger = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        if (!_playerCollidersInsideThisTrigger.Add(other)) return;

        if (enterSnapshot != null)
            enterSnapshot.TransitionTo(transitionTime);
        if (FogController.Instance != null)
            FogController.Instance.SetFog(enterFogDensity, transitionTime);

        Debug.Log("Вход в зону: " + gameObject.name);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        _playerCollidersInsideThisTrigger.Remove(other);
        // Ничего не переключаем: если игрок реально ушёл в противоположную зону, её
        // собственный Enter уже применил нужный snapshot/туман. Если игрок просто
        // отступил назад, не дойдя до соседней шторки, состояние должно остаться прежним.
    }

    private void OnDisable()
    {
        _playerCollidersInsideThisTrigger.Clear();
    }

    private static bool IsPlayerCollider(Collider col)
    {
        // Prefer root tag check so child colliders without the tag still count.
        if (col == null) return false;
        var root = col.transform != null ? col.transform.root : null;
        return root != null && root.CompareTag("Player");
    }
}
}
