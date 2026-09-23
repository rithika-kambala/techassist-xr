using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TechAssistXR.Hybrid
{
    [DefaultExecutionOrder(-100)]
    public sealed class TechnicianModeController : MonoBehaviour
    {
        [SerializeField] private TechnicianMode mode = TechnicianMode.Desktop;
        [SerializeField] private GameObject desktopRig;
        [SerializeField] private GameObject questRig;
        [SerializeField] private Camera desktopCamera;
        [SerializeField] private Camera questCamera;
        [SerializeField] private BaseInputModule desktopUiInput;
        [SerializeField] private BaseInputModule questUiInput;
        public TechnicianMode Mode => mode;
        public Camera ActiveCamera => mode == TechnicianMode.Desktop ? desktopCamera : questCamera;
        public Transform QuestOrigin => questRig != null ? questRig.transform : null;
        public event Action Changed;
        private TechnicianMode appliedMode;
        private void Awake()
        {
#if TECHASSIST_FORCE_QUEST
            mode = TechnicianMode.Quest;
#elif UNITY_ANDROID && !UNITY_EDITOR
            mode = TechnicianMode.Quest;
#elif TECHASSIST_FORCE_DESKTOP
            mode = TechnicianMode.Desktop;
#endif
            SetMode(mode);
        }
        private void Update() { if (mode != appliedMode) SetMode(mode); }
        public void SetDesktopMode() => SetMode(TechnicianMode.Desktop);
        public void SetQuestMode() => SetMode(TechnicianMode.Quest);
        public void SetMode(TechnicianMode requested)
        {
            if (requested == TechnicianMode.Quest && (questRig == null || questCamera == null || questUiInput == null)) {
                Debug.LogError("Assign Quest rig, camera and XR UI Input Module before switching mode.", this);
                requested = TechnicianMode.Desktop;
            }
            mode = appliedMode = requested;
            bool desktop = mode == TechnicianMode.Desktop;
            if (desktopRig != null) desktopRig.SetActive(desktop);
            if (questRig != null) questRig.SetActive(!desktop);
            if (desktopUiInput != null) desktopUiInput.enabled = desktop;
            if (questUiInput != null) questUiInput.enabled = !desktop;
            Changed?.Invoke();
        }
    }
}
