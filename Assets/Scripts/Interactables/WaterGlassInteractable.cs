using UnityEngine;

namespace KnocksFromBeneath
{
    // B-07: стакан «Стакана воды». Берётся в руки как обычный физический предмет (PickupController); налить — E у раковины
    // (KitchenSinkTarget), выпить — F. Сцену (кран, глотки, квесты) ведёт GlassOfWaterScene.
    public sealed class WaterGlassInteractable : MonoBehaviour
    {
        public GlassOfWaterScene Scene { get; private set; }

        public void Bind(GlassOfWaterScene scene) => Scene = scene;
    }
}
