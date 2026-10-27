using UnityEngine;

namespace KnocksFromBeneath
{
    // B-07: раковина на кухне — со стаканом в руках E наливает воду (GlassOfWaterScene.TryFill).
    public sealed class KitchenSinkTarget : MonoBehaviour
    {
        public GlassOfWaterScene Scene { get; private set; }

        public void Bind(GlassOfWaterScene scene) => Scene = scene;
    }
}
