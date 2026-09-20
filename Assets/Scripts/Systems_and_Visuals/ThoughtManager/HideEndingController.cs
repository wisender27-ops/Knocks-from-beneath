using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

// Концовка 3 (прятки), ночь 2 — T-20. Игрок прячется в одной из зон; неважно, выйдет ли он
// раньше или после "сигнала безопасности" — засада привязана к конкретной зоне и срабатывает
// в любом случае при выходе. Это осознанно: смысл концовки в том, что спрятаться некуда.
public sealed class HideEndingController
{
    private readonly GameObject[] _hideZones;
    private readonly Transform[] _ambushPoints; // тот же индекс, что и в _hideZones
    private readonly Transform _playerTransform;
    private readonly Func<IEnumerator, Coroutine> _startCoroutine;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action<bool> _lockCamera;
    private readonly Func<bool> _tryClaimEnding;
    private readonly Action _endGame;

    private bool _resolved;
    private int _activeZoneIndex = -1;

    // MonsterWatcherManager.Instance намеренно НЕ захватывается тут, а читается в месте
    // использования (SpawnWatcherNearPlayer/AmbushRoutine) — порядок Awake() между разными
    // MonoBehaviour в Unity не гарантирован, на момент создания этого контроллера в
    // IntroSequence.Awake() Instance может быть ещё null.
    public HideEndingController(
        GameObject[] hideZones,
        Transform[] ambushPoints,
        Transform playerTransform,
        Func<IEnumerator, Coroutine> startCoroutine,
        Action<string[], Action> showThoughts,
        Action<bool> lockCamera,
        Func<bool> tryClaimEnding,
        Action endGame)
    {
        _hideZones = hideZones;
        _ambushPoints = ambushPoints;
        _playerTransform = playerTransform;
        _startCoroutine = startCoroutine;
        _showThoughts = showThoughts;
        _lockCamera = lockCamera;
        _tryClaimEnding = tryClaimEnding;
        _endGame = endGame;
    }

    public void ActivateZones()
    {
        SetZonesActive(true);
    }

    // Вызывается BranchEndingController.TryResolve(), когда развилку забрала другая
    // концовка: зоны гаснут, и начатое укрытие уже ничем не закончится.
    public void SetZonesActive(bool active)
    {
        // Включение = чистый старт развилки, выключение = развилку забрал кто-то другой.
        _resolved = !active;
        _activeZoneIndex = -1;

        if (_hideZones == null) return;
        for (int i = 0; i < _hideZones.Length; i++)
        {
            if (_hideZones[i] != null) _hideZones[i].SetActive(active);
        }
    }

    public void HandleZoneEntered(GameObject zone)
    {
        if (_resolved || _activeZoneIndex >= 0) return;

        int idx = IndexOfZone(zone);
        if (idx < 0) return;

        _activeZoneIndex = idx;
        _startCoroutine(SearchRoutine());
    }

    public void HandleZoneExited(GameObject zone)
    {
        if (_resolved) return;

        int idx = IndexOfZone(zone);
        if (idx < 0 || idx != _activeZoneIndex) return;

        // Забираем развилку себе; если её уже забрал побег через дверь — выходим молча.
        if (_tryClaimEnding != null && !_tryClaimEnding()) return;

        _resolved = true;
        _startCoroutine(AmbushRoutine(idx));
    }

    int IndexOfZone(GameObject zone)
    {
        if (_hideZones == null) return -1;
        for (int i = 0; i < _hideZones.Length; i++)
        {
            if (_hideZones[i] == zone) return i;
        }
        return -1;
    }

    IEnumerator SearchRoutine()
    {
        // Монстр "ищет" — пара скримов эмбиентом (MonsterWatcherManager.SpawnWatcher уже
        // сам выбирает точку рядом с игроком, не рядом с местом, где тот спрятался), потом
        // тишина и сигнал, что вроде бы можно выходить. Если игрок уже вышел раньше
        // (_resolved == true) — досрочно прерываемся, ничего лишнего не показываем.
        yield return new WaitForSeconds(4f);
        if (_resolved) yield break;
        SpawnWatcherNearPlayer();

        yield return new WaitForSeconds(5f);
        if (_resolved) yield break;
        SpawnWatcherNearPlayer();

        yield return new WaitForSeconds(8f);
        if (_resolved) yield break;

        _showThoughts(new string[] {
            "Тихо...",
            "Кажется, можно выходить."
        }, null);
    }

    void SpawnWatcherNearPlayer()
    {
        if (MonsterWatcherManager.Instance == null || _playerTransform == null) return;
        MonsterWatcherManager.Instance.SpawnWatcher(_playerTransform.position);
    }

    IEnumerator AmbushRoutine(int zoneIndex)
    {
        _lockCamera?.Invoke(true);

        Transform ambush = (_ambushPoints != null && zoneIndex < _ambushPoints.Length) ? _ambushPoints[zoneIndex] : null;
        if (MonsterWatcherManager.Instance != null && ambush != null)
            MonsterWatcherManager.Instance.SpawnAt(ambush, 3f);

        yield return new WaitForSeconds(1.5f);

        _showThoughts(new string[] {
            "..."
        }, _endGame);
    }
}
}
