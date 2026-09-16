using System;
using System.Collections.Generic;

namespace TechAssistXR.Hybrid
{
    public enum TechnicianState { IDLE, REQUESTING, CONNECTED, INSTRUCTION_RECEIVED, STEP_COMPLETED, SESSION_ENDED }
    public enum TechnicianMode { Desktop, Quest }

    [Serializable]
    public sealed class ProcedureStep
    {
        public int step_number;
        public string title;
        public string instruction;
        public string target_component;
        public ProcedureStep Copy() => (ProcedureStep)MemberwiseClone();
        public bool IsValid => step_number > 0 && !string.IsNullOrWhiteSpace(title)
            && !string.IsNullOrWhiteSpace(instruction) && !string.IsNullOrWhiteSpace(target_component);
    }

    [Serializable]
    public sealed class ProcedureEnvelope { public ProcedureStep[] steps; }

    [Serializable]
    public sealed class StepAcknowledgment
    {
        public string type = "STEP_COMPLETED";
        public string session_id;
        public string event_id;
        public int step_number;
        public string target_component;
        public string completed_at_utc;
        public StepAcknowledgment Copy() => (StepAcknowledgment)MemberwiseClone();
    }

    // Pure C#: no MonoBehaviour, XR package, network library, or singleton dependency.
    // Call from one thread. Event handlers must not re-enter this engine.
    public sealed class TechnicianSession
    {
        private readonly Dictionary<int, ProcedureStep> steps = new Dictionary<int, ProcedureStep>();
        private ProcedureStep current;
        private StepAcknowledgment pending;
        public TechnicianState State { get; private set; } = TechnicianState.IDLE;
        public string SessionId { get; private set; }
        public int TotalSteps { get; private set; }
        public int CompletedSteps { get; private set; }
        public ProcedureStep CurrentStep => current?.Copy();
        public StepAcknowledgment PendingAcknowledgment => pending?.Copy();
        public event Action Changed;

        public bool Begin()
        {
            if (State != TechnicianState.IDLE && State != TechnicianState.SESSION_ENDED) return false;
            steps.Clear(); current = null; pending = null; TotalSteps = 0; CompletedSteps = 0;
            SessionId = Guid.NewGuid().ToString("N");
            SetState(TechnicianState.REQUESTING);
            return true;
        }
        public bool Connect(string sessionId, int totalSteps)
        {
            if (State != TechnicianState.REQUESTING || sessionId != SessionId || totalSteps <= 0) return false;
            TotalSteps = totalSteps;
            SetState(TechnicianState.CONNECTED);
            return true;
        }
        // Steps must be numbered 1..N. Duplicates are accepted only when identical.
        public bool Receive(string sessionId, ProcedureStep step)
        {
            if (sessionId != SessionId || step == null || !step.IsValid || step.step_number > TotalSteps
                || State == TechnicianState.IDLE || State == TechnicianState.REQUESTING
                || State == TechnicianState.SESSION_ENDED) return false;
            if (steps.TryGetValue(step.step_number, out var existing))
                return existing.title == step.title && existing.instruction == step.instruction
                    && existing.target_component == step.target_component;
            steps.Add(step.step_number, step.Copy());
            ShowNextIfReady();
            return true;
        }
        public bool CanComplete(string selectedId) => State == TechnicianState.INSTRUCTION_RECEIVED
            && current != null && pending == null && string.Equals(selectedId, current.target_component, StringComparison.Ordinal);
        public bool Complete(string selectedId)
        {
            if (!CanComplete(selectedId)) return false;
            pending = new StepAcknowledgment {
                session_id = SessionId, event_id = Guid.NewGuid().ToString("N"),
                step_number = current.step_number, target_component = current.target_component,
                completed_at_utc = DateTime.UtcNow.ToString("O")
            };
            SetState(TechnicianState.STEP_COMPLETED);
            return true;
        }
        public bool Confirm(string sessionId, string eventId)
        {
            if (State != TechnicianState.STEP_COMPLETED || pending == null
                || sessionId != SessionId || eventId != pending.event_id) return false;
            CompletedSteps++;
            pending = null; current = null;
            if (CompletedSteps == TotalSteps) SetState(TechnicianState.SESSION_ENDED);
            else { SetState(TechnicianState.CONNECTED); ShowNextIfReady(); }
            return true;
        }
        public void End()
        {
            if (State == TechnicianState.IDLE || State == TechnicianState.SESSION_ENDED) return;
            current = null; pending = null;
            SetState(TechnicianState.SESSION_ENDED);
        }
        private void ShowNextIfReady()
        {
            if (State != TechnicianState.CONNECTED || !steps.TryGetValue(CompletedSteps + 1, out var step)) return;
            current = step;
            SetState(TechnicianState.INSTRUCTION_RECEIVED);
        }
        private void SetState(TechnicianState state) { State = state; Changed?.Invoke(); }
    }
}
