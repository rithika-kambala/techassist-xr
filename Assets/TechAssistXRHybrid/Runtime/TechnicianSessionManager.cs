using System;
using UnityEngine;

namespace TechAssistXR.Hybrid
{
    [DisallowMultipleComponent]
    public sealed class TechnicianSessionManager : MonoBehaviour
    {
        [SerializeField] private TechnicianTransport transport;
        [SerializeField] private TechnicianInputAdapter input;
        [SerializeField] private MRGuidanceManager guidance;
        [SerializeField, Min(0.1f)] private float completionDebounceSeconds = 0.35f;
        public TechnicianSession Session { get; } = new TechnicianSession();
        public string SelectedComponentId { get; private set; }
        public string Error { get; private set; }
        public bool SendingAcknowledgment { get; private set; }
        public bool CanComplete => Session.CanComplete(SelectedComponentId) && !SendingAcknowledgment;
        public event Action Changed;
        // Observational hook for analytics. The configured transport performs actual delivery.
        public event Action<StepAcknowledgment> OnStepAcknowledgmentDispatched;
        private int displayedStep = -1;
        private float lastCompletionTime = float.NegativeInfinity;

        private void OnEnable()
        {
            Session.Changed += Refresh;
            if (input != null) input.OnComponentSelected += SelectComponent;
            if (transport != null) {
                transport.Connected += Connected;
                transport.StepReceived += ReceiveStepJson;
                transport.Acknowledged += Acknowledged;
                transport.Failed += Failed;
            }
            Refresh();
        }
        private void OnDisable()
        {
            Session.Changed -= Refresh;
            if (input != null) input.OnComponentSelected -= SelectComponent;
            if (transport != null) {
                transport.Connected -= Connected;
                transport.StepReceived -= ReceiveStepJson;
                transport.Acknowledged -= Acknowledged;
                transport.Failed -= Failed;
                transport.Close();
            }
            Session.End();
            guidance?.ClearHighlight();
        }
        public void StartSession()
        {
            if (transport == null) { Report("Assign a technician transport."); return; }
            if (Session.State != TechnicianState.IDLE && Session.State != TechnicianState.SESSION_ENDED) return;
            Error = null; SelectedComponentId = null; SendingAcknowledgment = false; displayedStep = -1;
            if (Session.Begin()) transport.RequestSession(Session.SessionId);
        }
        public void EndSession() { transport?.CancelSession(); SendingAcknowledgment = false; Session.End(); }
        private void Connected(string id, int total)
        {
            if (id != Session.SessionId) return;
            if (!Session.Connect(id, total)) Failed(id, "Invalid connection metadata or unexpected session state.");
        }
        public void ReceiveStepJson(string sessionId, string json)
        {
            if (sessionId != Session.SessionId) return;
            try {
                var step = JsonUtility.FromJson<ProcedureStep>(json);
                if (!Session.Receive(sessionId, step)) Report("Rejected invalid, conflicting, or out-of-session procedure step.");
            }
            catch (Exception ex) { Report("Invalid procedure JSON: " + ex.Message); }
        }
        private void SelectComponent(GameObject component)
        {
            SelectedComponentId = component != null ? component.name : null;
            Changed?.Invoke();
        }
        public void CompleteCurrentStep()
        {
            if (Time.unscaledTime - lastCompletionTime < completionDebounceSeconds || !CanComplete) return;
            lastCompletionTime = Time.unscaledTime;
            if (Session.Complete(SelectedComponentId)) RetryAcknowledgment();
        }
        public void RetryAcknowledgment()
        {
            var ack = Session.PendingAcknowledgment;
            if (ack == null || SendingAcknowledgment || transport == null) return;
            Error = null; SendingAcknowledgment = true; Changed?.Invoke();
            OnStepAcknowledgmentDispatched?.Invoke(ack.Copy());
            transport.SendAcknowledgment(ack);
        }
        private void Acknowledged(string id, string eventId)
        {
            var pending = Session.PendingAcknowledgment;
            if (id != Session.SessionId || pending == null || eventId != pending.event_id) return;
            SendingAcknowledgment = false; Error = null;
            Session.Confirm(id, eventId);
        }
        private void Failed(string id, string message)
        {
            if (id != Session.SessionId || Session.State == TechnicianState.SESSION_ENDED) return;
            SendingAcknowledgment = false;
            Error = message;
            if (Session.State == TechnicianState.REQUESTING) Session.End();
            Report(message);
        }
        private void Refresh()
        {
            var step = Session.CurrentStep;
            int next = step?.step_number ?? -1;
            if (next != displayedStep) {
                displayedStep = next; SelectedComponentId = null;
                if (step == null) guidance?.ClearHighlight();
                else if (guidance != null && !guidance.HighlightTargetComponent(step.target_component))
                    Error = "Target missing, inactive, or ambiguous: " + step.target_component;
            }
            Changed?.Invoke();
        }
        private void Report(string message) { Error = message; Debug.LogWarning("[TechAssist XR] " + message, this); Changed?.Invoke(); }
    }
}
