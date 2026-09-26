using UnityEngine;

namespace KnocksFromBeneath
{
    /// <summary>
    /// Слой "DoorNoRaycast" — для объектов, которые обязаны блокировать ФИЗИКУ
    /// (останавливать Rigidbody, CharacterController, брошенные предметы), но должны
    /// быть полностью прозрачны для ЛУЧЕЙ: ни одна камера взаимодействия, ни один
    /// ray-запрос игры не должен цеплять такой объект.
    ///
    /// ПОЧЕМУ НЕ excludeLayers/includeLayers (Unity 6):
    /// Эти маски влияют ТОЛЬКО на столкновения в симуляции. Проверено в редакторе:
    /// коллайдер с excludeLayers = ~0 полностью игнорировался шаром с Rigidbody
    /// (шар пролетел насквозь), но Physics.Raycast с маской ~0 продолжал его видеть.
    /// Переключение на includeLayers = Default тоже не помогло — луч так же попадал.
    /// То есть "невидим для лучей" через excludeLayers получить нельзя.
    ///
    /// ПОЧЕМУ НЕ встроенный "Ignore Raycast" (слой 2):
    /// Он исключается только запросами с Physics.DefaultRaycastLayers, а почти все
    /// запросы в проекте идут с явной маской ~0, которая этот слой насквозь видит.
    ///
    /// ПОЧЕМУ НУЖЕН ОТДЕЛЬНЫЙ ОБЪЕКТ-КОЛЛАЙДЕР, а не слой на самом пропе:
    /// Main Camera в сцене имеет cullingMask 479, в котором бит слоя 11 снят — если
    /// повесить слой 11 на визуальный проп, он просто исчезнет из кадра. Поэтому
    /// блокер вешается отдельным дочерним объектом без рендерера.
    /// </summary>
    public static class PhysicsMasks
    {
        public const string NoRaycastLayerName = "DoorNoRaycast";

        // Если слоя нет в TagManager — бит 0, и все маски остаются без изменений
        // (мягкая деградация: код не ломает игру, а просто перестаёт фильтровать).
        private static readonly int NoRaycastBit = BuildBit(NoRaycastLayerName);

        /// <summary>Бит слоя-блокера. Уже инвертирован для удобного исключения.</summary>
        public static int NoRaycastExclusionMask => ~NoRaycastBit;

        /// <summary>Все слои, кроме слоя-блокера. Для overlap-запросов.</summary>
        public static int AllLayers => Physics.AllLayers & ~NoRaycastBit;

        /// <summary>
        /// Все слои, кроме слоя-блокера. Основная маска для лучей.
        /// Раньше эти запросы шли с "~0" — то есть видели в том числе калитку.
        /// </summary>
        public static int RaycastAll => Physics.AllLayers & ~NoRaycastBit;

        /// <summary>
        /// Аналог Physics.DefaultRaycastLayers минус слой-блокер — для запросов,
        /// которые шли без явной маски.
        /// </summary>
        public static int RaycastDefault => Physics.DefaultRaycastLayers & ~NoRaycastBit;

        /// <summary>Убирает слой-блокер из произвольной маски.</summary>
        public static int WithoutNoRaycast(int mask) => mask & ~NoRaycastBit;

        /// <summary>Убирает слой-блокер из произвольной маски.</summary>
        public static LayerMask WithoutNoRaycast(LayerMask mask) => mask & ~NoRaycastBit;

        private static int BuildBit(string layerName)
        {
            int index = LayerMask.NameToLayer(layerName);
            return index < 0 ? 0 : 1 << index;
        }
    }
}
