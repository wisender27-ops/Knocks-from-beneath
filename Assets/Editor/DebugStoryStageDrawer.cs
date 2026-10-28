using System;
using UnityEditor;
using UnityEngine;
using KnocksFromBeneath;

[CustomPropertyDrawer(typeof(DebugStoryStage))]
public sealed class DebugStoryStageDrawer : PropertyDrawer
{
    private readonly struct Entry
    {
        public readonly DebugStoryStage Stage;
        public readonly string Label;
        public readonly string Description;
        public Entry(DebugStoryStage stage, string label, string description)
        {
            Stage = stage;
            Label = label;
            Description = description;
        }
    }

    private static readonly Entry[] Entries =
    {
        new Entry(DebugStoryStage.None, "С начала игры", "Обычный запуск: предыстория и приезд. Пропуск этапов не используется."),
        new Entry(DebugStoryStage.StartTrashQuest, "День 1/01 — Убрать мусор", "Начало уборки дома."),
        new Entry(DebugStoryStage.StartTrashDeliveryQuest, "День 1/02 — Вынести мусор", "Мусор собран; осталось вынести его."),
        new Entry(DebugStoryStage.StartBoxQuest, "День 1/03 — Разнести коробки", "Коробки у фургона; расстановка вещей и видеокамера."),
        new Entry(DebugStoryStage.StartPieQuest, "День 1/04 — Взять пирог", "Коробки разобраны. Начинается квест пирога."),
        new Entry(DebugStoryStage.StartPieMicrowaveQuest, "День 1/05 — Разогреть пирог", "Переход к микроволновке."),
        new Entry(DebugStoryStage.StartPieEatQuest, "День 1/06 — Съесть пирог", "Переход к ужину первого дня."),
        new Entry(DebugStoryStage.StartGoToBed, "День 1/07 — Лечь спать", "Вечер первого дня перед первой ночью."),
        new Entry(DebugStoryStage.StartNightOne, "Ночь 1/01 — Пробуждение и сосед", "Первая ночь: стук, сосед и ответный перестук."),
        new Entry(DebugStoryStage.StartDay2Decoration, "День 2/01 — Убрать вещи на кухне", "Утро второго дня: кухонные вещи и улики."),
        new Entry(DebugStoryStage.PlayNightRecording, "День 2/02 — Плёнка (M2)", "Открывается ночная запись: перемотка, стоп-кадр и улика. Камера подготавливается для проверки плёнки."),
        new Entry(DebugStoryStage.StartDay2Dinner, "День 2/03 — Принести продукты", "Начало подготовки ужина."),
        new Entry(DebugStoryStage.StartDinnerCook, "День 2/04 — Приготовить ужин", "Продукты подготовлены; мини-игра готовки."),
        new Entry(DebugStoryStage.StartDinnerEat, "День 2/05 — Съесть ужин", "Ужин готов и доступен на столе."),
        new Entry(DebugStoryStage.StartDay2Evening, "День 2/06 — Вечерний обход", "Обход дома перед сном; можно проверить засов."),
        new Entry(DebugStoryStage.StartNight, "Ночь 2/01 — Пробуждение", "Начало второй ночи. Сюжет зависит от состояния засова."),
        new Entry(DebugStoryStage.StartBreakerPanel, "Ночь 2/02 — Щиток (M4)", "Игрок у щитка в гараже с фонариком; электричество отключено."),
        new Entry(DebugStoryStage.StartKitchenNoise, "Ночь 2/03 — Шум на кухне", "Поиск источника шума и пугалка на лестнице."),
        new Entry(DebugStoryStage.StartFlashlightQuest, "Ночь 2/04 — Найти фонарик", "Квест обычного фонарика после кухонного события."),
        new Entry(DebugStoryStage.StartCrowbarQuest, "Ночь 2/05 — Найти лом", "Фонарик получен; предстоит найти лом."),
        new Entry(DebugStoryStage.StartBreakFloor, "Ночь 2/06 — Вскрыть замок люка", "Квест замка наружного люка. Внутреннее имя StartBreakFloor сохранено для совместимости."),
        new Entry(DebugStoryStage.StartLookInHole, "Ночь 2/07 — Осветить лестницу подвала", "После замка: открыть люк и осмотреть лестницу с фонариком."),
        new Entry(DebugStoryStage.StartHammerQuest, "Ночь 2/08 — Найти молоток", "Люк разблокирован; поиск молотка перед прятками."),
        new Entry(DebugStoryStage.StartBranchEndings, "Ночь 2/09 — Перед прятками (старый этап)", "Старый этап StartBranchEndings теперь ведёт к молотку перед прятками. Для развилки после пряток выберите «Выходы из дома»."),
        new Entry(DebugStoryStage.StartHidingSpots, "Ночь 2/10 — Молоток и прятки (M7)", "Игрок у молотка в спальне. Взять молоток, затем спрятаться под кроватью."),
        new Entry(DebugStoryStage.StartNightTwoExits, "Ночь 2/11 — Выходы из дома", "После пряток: люк, дорога и задний двор доступны для проверки концовок."),
        new Entry(DebugStoryStage.StartFinale, "Ночь 2/12 — Заколотить люк (M3)", "Фонарик, лом и молоток получены; начинается заколачивание люка.")
    };

    private static readonly string[] Labels = BuildLabels();

    private static string[] BuildLabels()
    {
        var labels = new string[Entries.Length];
        for (int i = 0; i < Entries.Length; i++) labels[i] = Entries[i].Label;
        return labels;
    }

    private static int IndexOf(int value)
    {
        for (int i = 0; i < Entries.Length; i++)
            if ((int)Entries[i].Stage == value) return i;
        return -1;
    }

    private static string Description(SerializedProperty property)
    {
        int index = IndexOf(property.intValue);
        if (index < 0) return "Неизвестный этап: " + property.intValue + ". Выберите доступный этап; текущее значение не изменяется автоматически.";
        bool enabled = property.serializedObject.FindProperty("useDebugSkip")?.boolValue ?? true;
        string settings = "";
        if (Entries[index].Label.StartsWith("День 2/", StringComparison.Ordinal) && Entries[index].Stage != DebugStoryStage.PlayNightRecording)
        {
            var camera = property.serializedObject.FindProperty("debugCameraPlaced");
            if (camera != null) settings = camera.boolValue ? " Камера будет установлена." : " Камера не установлена: ночной записи нет.";
        }
        return Entries[index].Description + settings + (Entries[index].Stage == DebugStoryStage.None ? "" :
            enabled ? "\nДебаг-запуск не сохраняет прогресс глав." : "\nДля запуска включите Use Debug Skip.");
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float width = Mathf.Max(160f, EditorGUIUtility.currentViewWidth - 65f);
        return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing +
            EditorStyles.helpBox.CalcHeight(new GUIContent(Description(property)), width);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var popup = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        int selected = IndexOf(property.intValue);
        string[] labels = Labels;
        if (selected < 0)
        {
            labels = new string[Labels.Length + 1];
            Array.Copy(Labels, labels, Labels.Length);
            labels[Labels.Length] = "Неизвестный этап (" + property.intValue + ")";
            selected = Labels.Length;
        }
        bool disabled = property.serializedObject.targetObject is IntroSequence &&
            !(property.serializedObject.FindProperty("useDebugSkip")?.boolValue ?? true);
        EditorGUI.BeginDisabledGroup(disabled);
        EditorGUI.BeginChangeCheck();
        int next = EditorGUI.Popup(popup, "Начать с этапа", selected, labels);
        if (EditorGUI.EndChangeCheck() && next < Entries.Length)
            property.intValue = (int)Entries[next].Stage;
        EditorGUI.EndDisabledGroup();
        float y = popup.yMax + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.HelpBox(new Rect(position.x, y, position.width, position.yMax - y), Description(property),
            IndexOf(property.intValue) < 0 ? MessageType.Warning : MessageType.Info);
        EditorGUI.EndProperty();
    }
}
