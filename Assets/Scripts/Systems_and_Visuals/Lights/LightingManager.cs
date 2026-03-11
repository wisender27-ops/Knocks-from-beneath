using UnityEngine;
using System.Collections.Generic;

public class LightingManager : MonoBehaviour
{
    public static LightingManager Instance; // Синглтон для легкого доступа

    [Header("Настройки фонового мерцания")]
    public float globalFlickerChance = 0.1f; // Шанс (0.1 = 10% каждые пару секунд)
    public float checkInterval = 2f;

    [SerializeField] private List<SmartLamp> _allLamps = new List<SmartLamp>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        InvokeRepeating(nameof(RandomFlickerCheck), checkInterval, checkInterval);
    }

    public void RegisterLamp(SmartLamp lamp) => _allLamps.Add(lamp);

    void RandomFlickerCheck()
    {
        foreach (var lamp in _allLamps)
        {
            if (Random.value < globalFlickerChance)
            {
                lamp.RandomFlicker(Random.Range(0.1f, 0.5f), 0.1f);
            }
        }
    }


    // МЕТОД ДЛЯ ТВОИХ ДРУГИХ СКРИПТОВ
    // Позволяет выключить все лампы сразу или по списку
    public void ForceLightsState(bool isOff)
    {
        foreach (var lamp in _allLamps)
        {
            lamp.SetOverride(isOff, 0);
        }
    }

    // Выключить конкретную лампу по ID
    public void SetLampState(string id, bool isOff)
    {
        foreach (var lamp in _allLamps)
        {
            if (lamp.lampID == id)
            {
                lamp.SetOverride(isOff, 0);
                return; // Нашли — выходим
            }
        }
        Debug.LogWarning($"Лампа с ID {id} не найдена!");
    }

    // Заставить конкретную лампу мигнуть (сюжетно)
    public void ForceFlicker(string id, float duration)
    {
        foreach (var lamp in _allLamps)
        {
            if (lamp.lampID == id)
            {
                lamp.RandomFlicker(duration, 0.05f);
                return;
            }
        }
    }
}