using UnityEngine;
using System.Collections.Generic;

public class LightingManager : MonoBehaviour
{
    public static LightingManager Instance;
    private List<SmartLamp> _allLamps = new List<SmartLamp>();

    void Awake() { Instance = this; }

    public void RegisterLamp(SmartLamp lamp) => _allLamps.Add(lamp);

    // ВОТ ТВОЙ НОВЫЙ МЕТОД: ID, сколько секунд мигать, как часто (интервал)
    public void Flicker(string id, float duration, float interval)
    {
        foreach (var lamp in _allLamps)
        {
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
        LightSwitch[] allSwitches = FindObjectsOfType<LightSwitch>();
        foreach (var ls in allSwitches)
        {
            if (ls.isOn)
                ls.ForceTurnOff();
        }
    }
}