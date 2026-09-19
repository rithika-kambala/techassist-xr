using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TechAssistXR.Hybrid
{
    public sealed class TechnicianInputAdapter : MonoBehaviour
    {
        [SerializeField] private TechnicianModeController mode;
        [SerializeField] private MRGuidanceManager guidance;
        [SerializeField] private TechnicianSessionManager session;
        [SerializeField] private LayerMask raycastMask = ~0;
        [SerializeField, Min(0.1f)] private float maxDistance = 20f;
        public event Action<GameObject> OnComponentSelected;
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        private void Update()
        {
            if (mode == null || mode.Mode != TechnicianMode.Desktop || mode.ActiveCamera == null) return;
            Vector2 position;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            position = Mouse.current.position.ReadValue();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (!Input.GetMouseButtonDown(0)) return;
            position = Input.mousePosition;
#else
            return;
#endif
#if ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
            // Explicit UI raycast avoids relying on EventSystem's execution order this frame.
            if (EventSystem.current != null) {
                uiHits.Clear();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, uiHits);
                if (uiHits.Count > 0) return;
            }
            if (Physics.Raycast(mode.ActiveCamera.ScreenPointToRay(position), out var hit,
                maxDistance, raycastMask, QueryTriggerInteraction.Ignore)) SelectComponent(hit.collider.gameObject);
#endif
        }
        public void SelectComponent(GameObject hitObject)
        {
            if (!isActiveAndEnabled || hitObject == null || guidance == null) return;
            var component = hitObject.GetComponentInParent<TechnicianComponent>();
            if (component == null || !guidance.TryGetTarget(component.name, out var target) || target != component.transform) return;
            OnComponentSelected?.Invoke(component.gameObject);
        }
        // Wire XRSimpleInteractable -> Select Entered -> XRSelectionRelay.Select().
        public void SelectFromXR(GameObject component)
        {
            if (mode != null && mode.Mode == TechnicianMode.Quest) SelectComponent(component);
        }
        // Optional dedicated confirmation action. Default: trigger clicks the UI Complete button.
        public void CompleteFromController()
        {
            if (mode != null && mode.Mode == TechnicianMode.Quest) session?.CompleteCurrentStep();
        }
    }
}
