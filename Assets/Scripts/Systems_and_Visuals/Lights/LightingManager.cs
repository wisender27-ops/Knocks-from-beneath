using UnityEngine;
using System.Collections.Generic;

namespace KnocksFromBeneath
{

public class LightingManager : MonoBehaviour
{
    public static LightingManager Instance;
    private List<SmartLamp> _allLamps = new List<SmartLamp>();

    void Awake() { Instance = this; }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterLamp(SmartLamp lamp)
    {
        if (lamp != null && !_allLamps.Contains(lamp))
            _allLamps.Add(lamp);
    }

    // ВОТ ТВОЙ НОВЫЙ МЕТОД: ID, сколько секунд мигать, как часто (интервал)
    public void Flicker(string id, float duration, float interval)
    {
        for (int i = _allLamps.Count - 1; i >= 0; i--)
        {
            SmartLamp lamp = _allLamps[i];
            if (lamp == null)
            {
                _allLamps.RemoveAt(i);
                continue;
            }
            if (lamp.lampID == id)
            {
                lamp.StartSimpleFlicker(duration, interval);
                return;
            }
        }
        Debug.LogWarning($"Лампа {id} не найдена!");
    }
    public void TurnOffAllLamps()
    {
        // Выключаем через LightSwitch чтобы состояние isOn синхронизировалось
        LightSwitch[] allSwitches = FindObjectsByType<LightSwitch>(FindObjectsSortMode.None);
        foreach (var ls in allSwitches)
        {
            if (ls.isOn)
                ls.ForceTurnOff();
        }
    }
}
}
