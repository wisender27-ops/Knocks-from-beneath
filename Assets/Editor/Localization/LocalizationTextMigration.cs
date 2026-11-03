using System.Collections.Generic;
using KnocksFromBeneath;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// L-01: переписанные реплики героя, которые лежат не в коде, а в данных: сцена Main (заставка ArrivalCutscene,
// улики InspectableClue) и ассеты предметов (Assets/Inventory/Items). Заменяет строки ровно по совпадению
// со старым текстом во всех сериализованных string-полях (и массивах) компонентов открытых сцен и в ассетах
// InventoryItemDefinition. Повторный запуск ничего не меняет. Сцену не сохраняет: проверь и сохрани сам.
public static class LocalizationTextMigration
{
    public static readonly Dictionary<string, string> Rewrites = new Dictionary<string, string>
    {
        { "Из старого дома мне пришлось уехать.\nБыстро. Почти без вещей.",
          "Из старого дома я уехал за одну ночь.\nВещи потом пришлось отправлять следом." },
        { "Что там случилось, не важно.\nЛюк заколочен, и возвращаться туда я не собираюсь.",
          "О том, что там случилось, я стараюсь не думать.\nЛюк заколочен. Туда я не вернусь." },
        { "Цена смутила.\nСлишком дёшево для двух этажей, гаража и участка.",
          "Цена настораживала.\nДва этажа, гараж, участок — и почти ничего за это." },
        { "И семья торопилась.\nСами нашли меня, сами привезли бумаги.",
          "И они очень торопились.\nСами меня нашли, сами привезли бумаги." },
        { "Но кто в моём положении отказался бы\nот такого дома за такие деньги?",
          "Но в моём положении\nот таких предложений не отказываются." },
        { "Я подписал бумаги, не задавая лишних вопросов.",
          "Я подписал всё и не стал задавать вопросов." },
        { "На дне нацарапано: «Постучи в пол: оно повторит столько же. Без пауз. Это не я».",
          "На дне нацарапано: «Постучи в пол — оно повторит столько же. Без пауз. Это не я»." },
        { "Оно. Не «он». И «это не я»… Тогда кто отвечает?",
          "«Оно», не «он». И «это не я»… Тогда что же мне отвечает?" },
        { "Внизу… на первом этаже. Над подвалом.",
          "Проверить можно только внизу, над подвалом." },
        { "На обложке наклейка: «Эта книга принадлежит Уолтеру Хейлу». Книга прежнего хозяина. Он её не забрал.",
          "На обложке наклейка: «Эта книга принадлежит Уолтеру Хейлу». Прежний хозяин её не забрал." },
        { "Уолтер Хейл. Так звали прежнего хозяина.",
          "Уолтер Хейл. Значит, так звали прежнего хозяина." },
        { "Кто переезжает и бросает свои книги?",
          "Книги бросают, когда уезжают в спешке. Уж я-то знаю." },
        { "Повторить его стук… вместе с паузами.",
          "Повторить стук. Вместе с паузами." },
        { "Кто-то оставил инструкцию. Для того, кто будет жить здесь после него?",
          "Кто-то оставил инструкцию. Для следующего жильца?" },
        { "Лежала тут с самого переезда. Я и не заметил.",
          "Лежала здесь с самого переезда, а я не заметил." },
        { "Я платил через контору и ни разу его не видел. Хейл.",
          "Я платил через агентство и ни разу не видел хозяина. Хейл." },
        { "Телефон",
          "ТЕЛЕФОН" },
        { "Тяжёлый старый лом. Пригодится, чтобы вскрывать и ломать непрочные преграды.",
          "Тяжёлый старый лом. Им можно сбить замок или что-нибудь вскрыть." },
        { "Потёртый фонарик. Луч слабый, но без него в тёмных комнатах почти ничего не видно.",
          "Потёртый фонарик. Луч слабый, но в темноте с ним спокойнее." },
        { "Прочный рабочий молоток. Нужен для ремонта и некоторых механизмов в доме.",
          "Тяжёлый плотницкий молоток. Гвоздь войдёт с пары ударов." },
        { "Старый ключ с верстака в подвале. Что-то в этом доме заперто.",
          "Старый ключ с верстака в подвале. Где-то в этом доме есть запертая дверь." },
        { "Мой телефон. Фонарик можно включить в любое время.",
          "Мой телефон. Связи почти нет, зато есть фонарик." },
        { "Плоская отвёртка с верстака в подвале.",
          "Плоская отвёртка. Болты на решётке ей поддадутся." },
    };

    [MenuItem("Tools/Localization/Apply Text Rewrite (L-01)")]
    public static void Apply()
    {
        int changed = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            int before = changed;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component != null) changed += Rewrite(component);
            if (changed > before) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[L-01] Сцена {scene.name}: заменено строк {changed - before}.");
        }
        foreach (string guid in AssetDatabase.FindAssets("t:InventoryItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            int count = Rewrite(item);
            if (count > 0) Debug.Log($"[L-01] {AssetDatabase.GUIDToAssetPath(guid)}: заменено строк {count}.");
            changed += count;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[L-01] Всего заменено строк: {changed}. Сцены помечены изменёнными — сохраните их.");
    }

    static int Rewrite(Object target)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.GetIterator();
        int count = 0;
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.String) continue;
            if (!Rewrites.TryGetValue(property.stringValue, out string replacement)) continue;
            property.stringValue = replacement;
            count++;
        }
        if (count > 0) serialized.ApplyModifiedProperties();
        return count;
    }
}
