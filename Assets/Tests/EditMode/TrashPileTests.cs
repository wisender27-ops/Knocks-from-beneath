using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using KnocksFromBeneath;

// Кучи мусора (garbagepile 1-3) видны с самого начала игры — не скрываются
// TrashManager'ом. Недоступность подбора до старта квеста 'trash-collect'
// обеспечивает сам TrashPile.Collect(), а не активность/неактивность GameObject'а.
public class TrashPileTests
{
    private GameObject _questManagerGo;
    private GameObject _trashManagerGo;
    private TrashManager _trashManager;
    private GameObject _pileGo;
    private TrashPile _pile;

    private void CreateManagerAndPile()
    {
        _questManagerGo = new GameObject("QuestManager");
        QuestManager.Instance = _questManagerGo.AddComponent<QuestManager>();
        QuestManager.Instance.questList = new List<QuestManager.QuestData>();

        _trashManagerGo = new GameObject("TrashManager");
        _trashManager = _trashManagerGo.AddComponent<TrashManager>();
        TrashManager.Instance = _trashManager;

        _pileGo = new GameObject("garbagepile 1");
        _pile = _pileGo.AddComponent<TrashPile>();
        _trashManager.trashPiles = new[] { _pileGo };
    }

    private static void SetActiveQuest(string tag, int required)
    {
        QuestManager.Instance.questList.Add(new QuestManager.QuestData
        {
            questTitle = tag,
            questTag = tag,
            requiredAmount = required,
            currentAmount = 0,
            onQuestComplete = new UnityEngine.Events.UnityEvent()
        });
        QuestManager.Instance.currentQuestIndex = QuestManager.Instance.questList.Count - 1;
    }

    [TearDown]
    public void TearDown()
    {
        QuestManager.Instance = null;
        TrashManager.Instance = null;
        if (_questManagerGo != null) Object.DestroyImmediate(_questManagerGo);
        if (_trashManagerGo != null) Object.DestroyImmediate(_trashManagerGo);
        if (_pileGo != null) Object.DestroyImmediate(_pileGo);
    }

    [Test]
    public void Initialize_DoesNotHidePiles()
    {
        CreateManagerAndPile();

        _trashManager.Initialize();

        Assert.That(_pileGo.activeSelf, Is.True, "куча мусора должна оставаться видимой после Initialize()");
    }

    [Test]
    public void Collect_WrongActiveQuest_DoesNotDestroyPileOrAddProgress()
    {
        CreateManagerAndPile();
        _trashManager.Initialize();
        SetActiveQuest("box-delivery", 1); // не 'trash-collect' — подбор ещё недоступен

        _pile.Collect();

        Assert.That(_pileGo != null && !_pileGo.Equals(null), Is.True, "куча не должна быть уничтожена вне этапа сбора мусора");
        Assert.That(_pileGo.activeSelf, Is.True);
        Assert.That(QuestManager.Instance.questList[0].currentAmount, Is.EqualTo(0));
    }

    [Test]
    public void Collect_ActiveTrashCollectQuest_AddsProgressAndDestroysPile()
    {
        CreateManagerAndPile();
        _trashManager.Initialize();
        SetActiveQuest("trash-collect", 3);

        // Collect() calls Object.Destroy(), which is a no-op-with-error outside Play Mode —
        // expected here, this EditMode test runner never enters Play Mode (same caveat as
        // QuestCollectorTests). Progress being added is what actually proves the pile was
        // accepted; real destruction is exercised in Play Mode, not here.
        LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode.*", RegexOptions.Singleline));
        _pile.Collect();

        Assert.That(QuestManager.Instance.questList[0].currentAmount, Is.EqualTo(1));
    }
}
