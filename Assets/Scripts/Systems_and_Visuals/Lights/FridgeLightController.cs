using UnityEngine;

namespace KnocksFromBeneath
{
    [DisallowMultipleComponent]
    public sealed class FridgeLightController : MonoBehaviour
    {
        [SerializeField] private Door fridgeDoor;
        [SerializeField] private Light fridgeLight;
        [SerializeField] private Light fridgeFillLight;
        [SerializeField] private Door freezerDoor;
        [SerializeField] private Light freezerLight;

        private bool _fridgeWasOpen;
        private bool _freezerWasOpen;

        private void OnEnable()
        {
            RefreshLights(true);
        }

        private void LateUpdate()
        {
            RefreshLights(false);
        }

        private void OnDisable()
        {
            SetEnabled(fridgeLight, false);
            SetEnabled(fridgeFillLight, false);
            SetEnabled(freezerLight, false);
        }

        private void RefreshLights(bool force)
        {
            bool fridgeIsOpen = fridgeDoor != null && fridgeDoor.isOpen;
            bool freezerIsOpen = freezerDoor != null && freezerDoor.isOpen;

            if (force || fridgeIsOpen != _fridgeWasOpen)
            {
                SetEnabled(fridgeLight, fridgeIsOpen);
                SetEnabled(fridgeFillLight, fridgeIsOpen);
                _fridgeWasOpen = fridgeIsOpen;
            }

            if (force || freezerIsOpen != _freezerWasOpen)
            {
                SetEnabled(freezerLight, freezerIsOpen);
                _freezerWasOpen = freezerIsOpen;
            }
        }

        private static void SetEnabled(Light source, bool value)
        {
            if (source != null && source.enabled != value)
                source.enabled = value;
        }
    }
}
