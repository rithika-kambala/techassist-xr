using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TechAssistXR.Hybrid
{
    public sealed class TechnicianUIManager : MonoBehaviour
    {
        [SerializeField] private TechnicianSessionManager session;
        [SerializeField] private TechnicianModeController mode;
        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform procedurePanel;
        [SerializeField] private TMP_Text titleText, instructionText, progressText, statusText, feedbackText;
        [SerializeField] private Button startButton, completeButton, retryButton, endButton;
        [SerializeField] private Vector3 worldPosition = new Vector3(0, 1.4f, 1.5f);
        [SerializeField] private Vector2 canvasSize = new Vector2(800, 600);
        [SerializeField, Min(0.0001f)] private float worldScale = 0.001f;
        private Transform originalParent;
        private int shownStep = -1;
        private void Awake() { if (canvas != null) originalParent = canvas.transform.parent; }
        private void OnEnable()
        {
            if (session != null) session.Changed += Refresh;
            if (mode != null) mode.Changed += ApplyMode;
            if (startButton != null) startButton.onClick.AddListener(StartSession);
            if (completeButton != null) completeButton.onClick.AddListener(Complete);
            if (retryButton != null) retryButton.onClick.AddListener(Retry);
            if (endButton != null) endButton.onClick.AddListener(End);
            ApplyMode(); Refresh();
        }
        private void OnDisable()
        {
            if (session != null) session.Changed -= Refresh;
            if (mode != null) mode.Changed -= ApplyMode;
            if (startButton != null) startButton.onClick.RemoveListener(StartSession);
            if (completeButton != null) completeButton.onClick.RemoveListener(Complete);
            if (retryButton != null) retryButton.onClick.RemoveListener(Retry);
            if (endButton != null) endButton.onClick.RemoveListener(End);
        }
        private void StartSession() => session?.StartSession();
        private void Complete() => session?.CompleteCurrentStep();
        private void Retry() => session?.RetryAcknowledgment();
        private void End() => session?.EndSession();
        private void ApplyMode()
        {
            if (canvas == null || mode == null) return;
            bool desktop = mode.Mode == TechnicianMode.Desktop;
            canvas.transform.SetParent(desktop ? originalParent : mode.QuestOrigin, false);
            canvas.renderMode = desktop ? RenderMode.ScreenSpaceOverlay : RenderMode.WorldSpace;
            canvas.worldCamera = mode.ActiveCamera;
            if (procedurePanel != null) {
                procedurePanel.anchorMin = desktop ? new Vector2(0.68f, 0.08f) : new Vector2(0.05f, 0.05f);
                procedurePanel.anchorMax = desktop ? new Vector2(0.98f, 0.92f) : new Vector2(0.95f, 0.95f);
                procedurePanel.offsetMin = procedurePanel.offsetMax = Vector2.zero;
            }
            var rect = (RectTransform)canvas.transform;
            rect.localScale = desktop ? Vector3.one : Vector3.one * worldScale;
            if (!desktop) {
                rect.sizeDelta = canvasSize; rect.localPosition = worldPosition; rect.localRotation = Quaternion.identity;
            }
        }
        private void Refresh()
        {
            if (session == null) return;
            var state = session.Session;
            var step = state.CurrentStep;
            if (shownStep != (step?.step_number ?? -1)) {
                shownStep = step?.step_number ?? -1;
                if (instructionText != null) {
                    var scroll = instructionText.GetComponentInParent<ScrollRect>();
                    if (scroll != null) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
                }
            }
            Set(titleText, step != null ? step.title : state.State == TechnicianState.SESSION_ENDED ? "Session ended" : "TechAssist XR");
            Set(instructionText, step?.instruction ?? "Start a session to receive the repair procedure.");
            Set(progressText, state.TotalSteps > 0 ? $"Step {step?.step_number ?? state.CompletedSteps} of {state.TotalSteps}" : "Waiting for procedure");
            Set(statusText, state.State.ToString());
            Set(feedbackText, !string.IsNullOrEmpty(session.Error) ? session.Error : session.SendingAcknowledgment
                ? "Sending completion…" : step != null ? "Selected: " + (session.SelectedComponentId ?? "none") + " | Target: " + step.target_component : "");
            if (startButton != null) startButton.interactable = state.State == TechnicianState.IDLE || state.State == TechnicianState.SESSION_ENDED;
            if (completeButton != null) completeButton.interactable = session.CanComplete;
            if (retryButton != null) retryButton.interactable = state.PendingAcknowledgment != null && !session.SendingAcknowledgment;
            if (endButton != null) endButton.interactable = state.State != TechnicianState.IDLE && state.State != TechnicianState.SESSION_ENDED;
        }
        private static void Set(TMP_Text label, string value) { if (label != null) { label.richText = false; label.text = value; } }
    }
}
