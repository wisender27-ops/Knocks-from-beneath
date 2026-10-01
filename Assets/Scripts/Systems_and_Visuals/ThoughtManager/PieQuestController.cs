using System;
using UnityEngine;
using UnityEngine.Events;

namespace KnocksFromBeneath
{

// Мини-сюжет "квест с пирогом": достать из холодильника -> разогреть в
// микроволновке -> съесть. Выделен из IntroSequence (T-08) как отдельный
// самодостаточный кусок сюжета. Не MonoBehaviour — получает нужные ссылки
// и колбэки от IntroSequence при создании, сцену/префаб это не затрагивает.
public sealed class PieQuestController
{
    private readonly GameObject _pieObject;
    private readonly GameObject _microwaveZone;
    private readonly Action<string, int, UnityAction, string> _createQuest;
    private readonly Action<string[], Action> _showThoughts;
    private readonly Func<string, bool> _isQuestActive;
    private readonly Action _onDinnerFinished;
    private readonly Action<string> _recordCompletion;

    private bool _isPieTaken;
    private bool _isPieHeated;

    public PieQuestController(
        GameObject pieObject,
        GameObject microwaveZone,
        Action<string, int, UnityAction, string> createQuest,
        Action<string[], Action> showThoughts,
        Func<string, bool> isQuestActive,
        Action onDinnerFinished,
        Action<string> recordCompletion = null)
    {
        _pieObject = pieObject;
        _microwaveZone = microwaveZone;
        _createQuest = createQuest;
        _showThoughts = showThoughts;
        _isQuestActive = isQuestActive;
        _onDinnerFinished = onDinnerFinished;
        _recordCompletion = recordCompletion;
    }

    public void SetupTakePieQuest()
    {
        _isPieTaken = false;
        _isPieHeated = false;

        if (_pieObject != null) _pieObject.SetActive(true);
        if (_microwaveZone != null) _microwaveZone.SetActive(false);

        _createQuest("Достать пирог из холодильника", 1, null, "pie-take");
    }

    // Публикуется через GameEvents.OnPieGrabbed безусловно — сами решаем
    // здесь, важно ли нам сейчас, что пирог взяли в руки.
    public void HandlePieGrabbed()
    {
        if (IsPieTakeQuestActive())
            OnPieTaken();
    }

    public void OnPieTaken()
    {
        _recordCompletion?.Invoke("pie-take");
        SetupMicrowaveQuest();
    }

    public void SetupMicrowaveQuest()
    {
        _isPieTaken = true;
        _isPieHeated = false;
        if (_pieObject != null) _pieObject.SetActive(true);
        if (_microwaveZone != null) _microwaveZone.SetActive(true);
        _createQuest("Поставить пирог в микроволновку", 1, OnPiePlacedInMicrowave, "pie-microwave");
    }

    public void OnPiePlacedInMicrowave()
    {
        _recordCompletion?.Invoke("pie-microwave");
        _isPieHeated = true;
        if (_microwaveZone != null) _microwaveZone.SetActive(false);
        _createQuest("Съесть пирог", 1, OnPieEaten, "pie-eat");
    }

    public void OnPieEaten()
    {
        _recordCompletion?.Invoke("pie-eat");
        _showThoughts(new string[] {
            "Уже легче.",
            "Теперь можно поспать."
        }, _onDinnerFinished);
    }

    public bool IsPieTakeQuestActive()
    {
        return _isQuestActive("pie-take");
    }

    public bool CanPlacePieInMicrowave()
    {
        return _isPieTaken && !_isPieHeated && _isQuestActive("pie-microwave");
    }

    public bool CanEatPie()
    {
        return _isPieHeated && _isQuestActive("pie-eat");
    }
}
}
