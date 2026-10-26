using UnityEngine;

namespace KnocksFromBeneath
{
    public struct InteractionFocus
    {
        public Transform Target;
        public Vector3 Point;
        public string Prompt;
        public bool Blocked;
        public bool IsDoor;
        public bool IsValid => Target != null;
    }

    public static class InteractionFocusResolver
    {
        public static GameObject PhysicalItem(Collider collider)
        {
            if (collider == null) return null;
            Rigidbody body = collider.attachedRigidbody;
            GameObject item = body != null ? body.gameObject : collider.gameObject;
            return item.CompareTag("Pickable") && item.GetComponent<Rigidbody>() != null ? item : null;
        }

        public static bool TryResolve(PlayerInteraction actor, IntroSequence intro, RaycastHit hit, out InteractionFocus focus)
        {
            focus = default;
            if (actor == null || hit.collider == null) return false;
            Transform t = hit.collider.transform;
            QuestManager quests = QuestManager.Instance;
            BasementHatchLock hatchLock = t.GetComponentInParent<BasementHatchLock>();
            BasementHatch hatch = hatchLock != null ? hatchLock.GetComponentInParent<BasementHatch>() : t.GetComponentInParent<BasementHatch>();
            if (hatchLock != null && hatchLock.IsLocked && quests != null && quests.IsQuestActive(hatchLock.RequiredQuestTag))
                return Set(hatchLock.transform, hatchLock.CanStartBreak(actor) ? "Вскрыть замок" : "Подойдите ближе",
                    !hatchLock.CanStartBreak(actor), false, hit, out focus);
            if (hatch != null)
            {
                if (hatch.IsBoarded || hatch.IsLockedResponsePlaying) return false;
                if (intro != null && intro.CanBoardBasement(hatch))
                    return Set(hatch.transform, "Заколотить люк", false, false, hit, out focus);
                return Set(hatch.transform, hatch.IsLocked ? "Проверить замок" : hatch.IsOpen ? "Закрыть люк" : "Открыть люк",
                    false, false, hit, out focus);
            }
            FloorLogic floor = t.GetComponentInParent<FloorLogic>();
            if (floor != null)
                return floor.CanStartBreak(actor.inventory) && Set(floor.transform, "Вскрыть доски", false, false, hit, out focus);
            TrashPile trash = t.GetComponentInParent<TrashPile>();
            if (trash != null)
                return quests != null && quests.IsQuestActive("trash-collect") && Set(trash.transform, "Собрать мусор", false, false, hit, out focus);
            HammerTrap hammer = t.GetComponentInParent<HammerTrap>();
            if (hammer != null)
                return actor.inventory != null && quests != null && quests.IsQuestActive("hammer-find") &&
                    Set(hammer.transform, "Взять молоток", false, false, hit, out focus);
            LightSwitch lightSwitch = t.GetComponentInParent<LightSwitch>();
            if (lightSwitch != null)
                return Set(lightSwitch.transform, "Переключить свет", false, false, hit, out focus);
            SimpleItem item = t.GetComponentInParent<SimpleItem>();
            if (item != null)
                return item.IsAvailable && actor.inventory != null && quests != null && quests.IsItemRequired(item.itemType) &&
                    Set(item.transform, "Взять", false, false, hit, out focus);
            GameObject physical = PhysicalItem(hit.collider);
            if (physical != null)
                return actor.pickup != null && actor.pickup.holdPoint != null &&
                    Set(physical.transform, "Взять", false, false, hit, out focus);
            BedSleepInteractable bed = t.GetComponentInParent<BedSleepInteractable>();
            if (bed != null) return bed.CanInteract && Set(bed.transform, "Лечь спать", false, false, hit, out focus);
            MicrowaveInteractable microwave = t.GetComponentInParent<MicrowaveInteractable>();
            Door door = t.GetComponentInParent<Door>();
            if (microwave != null && microwave.microwaveDoor != null && microwave.IsPieReadyToTake())
                return Set(door != null ? door.transform : microwave.transform,
                    microwave.microwaveDoor != null && microwave.microwaveDoor.isOpen ? "Достать пирог" : "Открыть дверцу",
                    false, door != null && !microwave.microwaveDoor.isOpen, hit, out focus);
            IngredientItem ingredient = t.GetComponentInParent<IngredientItem>();
            if (ingredient != null) return quests != null && quests.IsQuestActive("dinner-ingredients") &&
                Set(ingredient.transform, "Взять продукт", false, false, hit, out focus);
            DinnerMealInteractable meal = t.GetComponentInParent<DinnerMealInteractable>();
            if (meal != null) return meal.CanInteract && Set(meal.transform, "Съесть ужин", false, false, hit, out focus);
            StoveInteractable stove = t.GetComponentInParent<StoveInteractable>();
            if (stove != null && door == null)
                return intro != null && intro.CanCookDinner() && !stove.IsReady && !stove.IsCooking &&
                    Set(stove.transform, "Готовить ужин", false, false, hit, out focus);
            InspectableClue clue = t.GetComponentInParent<InspectableClue>();
            if (clue != null) return clue.CanInteract && Set(clue.transform, "Осмотреть", false, false, hit, out focus);
            BreakerPanel panel = t.GetComponentInParent<BreakerPanel>();
            if (panel != null) return panel.CanInteract && Set(panel.transform, "Открыть щиток", false, false, hit, out focus);
            FrontDoorLockInteractable bolt = t.GetComponentInParent<FrontDoorLockInteractable>();
            if (bolt != null) return bolt.CanInteract && Set(bolt.transform, bolt.IsLocked ? "Отодвинуть засов" : "Задвинуть засов", false, false, hit, out focus);
            if (door != null && door.isActiveAndEnabled)
                return Set(door.transform, door.IsInteractionLocked ? "Заперто" : door.isOpen ? "Закрыть" : "Открыть",
                    door.IsInteractionLocked, true, hit, out focus);
            return false;
        }

        private static bool Set(Transform target, string prompt, bool blocked, bool door, RaycastHit hit, out InteractionFocus focus)
        {
            focus = new InteractionFocus { Target = target, Point = hit.point + hit.normal * .006f,
                Prompt = prompt, Blocked = blocked, IsDoor = door };
            return target != null;
        }
    }
}
