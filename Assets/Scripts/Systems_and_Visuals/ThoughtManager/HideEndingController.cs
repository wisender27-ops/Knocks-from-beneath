using System;
using System.Collections;
using UnityEngine;

namespace KnocksFromBeneath
{

// Поиск монстра, пока игрок сидит в укрытии (M-07 «Дыхание» — HidingBreathGame).
// finished(true) — монстр нашёл (или игрок вылез раньше времени), finished(false) — ушёл.
// Stop() — развилку забрал другой путь, поиск прерывается. SetPrompt — игрок у укрытия (-1 — отошёл).
public interface IHidingSearch
{
    IEnumerator Run(int zoneIndex, Action<bool> finished);
    void Stop();
    void SetPrompt(int zoneIndex);
}

// Концовка 3 (прятки), ночь 2 — T-20. Без поиска (IHidingSearch) зона сама и есть укрытие: вошёл —
// спрятался, засада привязана к зоне и срабатывает при выходе в любом случае — спрятаться некуда.
// С поиском M-07 зона — только место рядом с укрытием (под кроватью героя): E — спрятаться, дальше мини-игра
// «Дыхание». Нашёл в укрытии или застал снаружи — E3; переждал — укрытие гаснет, сюжет идёт к люку (onSurvived).
public sealed class HideEndingController
{
    private readonly GameObject[] _hideZones;
    private readonly Transform[] _ambushPoints; // тот же индекс, что и в _hideZones
    private readonly Transform _playerTransform;
    private readonly Func<IEnumerator, Coroutine> _startCoroutine;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Action<bool> _lockCamera;
    private readonly Func<bool> _tryClaimEnding;
    private readonly Action<EndingId> _endGame;
    private readonly IHidingSearch _search;
    private readonly Action _onHideStarted;
    private readonly Action _onSurvived;

    private bool _resolved;
    private bool _searching;
    private int _activeZoneIndex = -1;
    private int _nearZoneIndex = -1;

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
        Action<EndingId> endGame,
        IHidingSearch search = null,
        Action onHideStarted = null,
        Action onSurvived = null)
    {
        _hideZones = hideZones;
        _ambushPoints = ambushPoints;
        _playerTransform = playerTransform;
        _startCoroutine = startCoroutine;
        _showThoughts = showThoughts;
        _lockCamera = lockCamera;
        _tryClaimEnding = tryClaimEnding;
        _endGame = endGame;
        _search = search;
        _onHideStarted = onHideStarted;
        _onSurvived = onSurvived;
    }

    public bool IsSearching => _searching;
    public bool ZonesOpen => !_resolved;

    public void ActivateZones()
    {
        SetZonesActive(true);
    }

    // Включает укрытия на старте развилки (BranchEndingController.Activate) и гасит, когда
    // развилку забрала другая концовка: начатое укрытие уже ничем не закончится.
    public void SetZonesActive(bool active)
    {
        _resolved = !active;
        _activeZoneIndex = -1;
        SetNear(-1);
        if (_searching)
        {
            _searching = false;
            _search?.Stop();
        }

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

        if (_search != null)
        {
            SetNear(idx);
            return;
        }
        _activeZoneIndex = idx;
        _startCoroutine(SearchRoutine());
    }

    public void HandleZoneExited(GameObject zone)
    {
        if (_resolved) return;

        int idx = IndexOfZone(zone);
        if (idx < 0) return;

        if (_search != null)
        {
            // В укрытии игрока двигает сама мини-игра — выход из зоны тут ничего не значит.
            if (!_searching && idx == _nearZoneIndex) SetNear(-1);
            return;
        }
        if (idx != _activeZoneIndex) return;
        Ambush(idx);
    }

    // E у укрытия (HidingBreathGame).
    public void HandleHideRequested()
    {
        if (_search == null || _resolved || _searching || _nearZoneIndex < 0) return;
        _activeZoneIndex = _nearZoneIndex;
        SetNear(-1);
        _searching = true;
        _onHideStarted?.Invoke();
        _startCoroutine(_search.Run(_activeZoneIndex, HandleSearchFinished));
    }

    void SetNear(int idx)
    {
        _nearZoneIndex = idx;
        _search?.SetPrompt(idx);
    }

    void HandleSearchFinished(bool found)
    {
        if (!_searching || _resolved) return;
        _searching = false;
        if (found)
        {
            Ambush(_activeZoneIndex);
            return;
        }
        // Переждал: укрытия больше не нужны, развилка остаётся за люком.
        SetZonesActive(false);
        if (_onSurvived != null)
        {
            _onSurvived();
            return;
        }
        _showThoughts(new string[] {
            "Ушло. Обратно вниз.",
            "Пока его нет — заколотить люк."
        }, null);
    }

    // Монстр дошёл, а игрок так и не спрятался (бросок уже показан мини-игрой).
    public void HandleCaughtInOpen()
    {
        if (_search == null || _resolved || _searching) return;
        SetNear(-1);
        _activeZoneIndex = 0;
        Ambush(0);
    }

    void Ambush(int idx)
    {
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
            "Больше ничего не слышно.",
            "Может, ушло. Надо выбираться."
        }, null);
    }

    void SpawnWatcherNearPlayer()
    {
        if (MonsterWatcherManager.Instance == null || _playerTransform == null) return;
        MonsterWatcherManager.Instance.SpawnWatcher(_playerTransform.position, forced: true);
    }

    IEnumerator AmbushRoutine(int zoneIndex)
    {
        _lockCamera?.Invoke(true);

        // С мини-игрой монстр уже стоит перед игроком — второй не нужен.
        Transform ambush = (_ambushPoints != null && zoneIndex >= 0 && zoneIndex < _ambushPoints.Length) ? _ambushPoints[zoneIndex] : null;
        if (_search == null && MonsterWatcherManager.Instance != null && ambush != null)
            MonsterWatcherManager.Instance.SpawnAt(ambush, 3f);

        yield return new WaitForSeconds(_search == null ? 1.5f : 0.6f);

        _showThoughts(new string[] {
            "Нет... Оно здесь."
        }, () => _endGame(EndingId.Hidden));
    }
}
}
