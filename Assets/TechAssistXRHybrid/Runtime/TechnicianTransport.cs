using System;
using UnityEngine;

namespace TechAssistXR.Hybrid
{
    // All events must be raised on Unity's main thread. Correlation IDs reject stale responses.
    public abstract class TechnicianTransport : MonoBehaviour
    {
        public event Action<string, int> Connected;
        public event Action<string, string> StepReceived;
        public event Action<string, string> Acknowledged;
        public event Action<string, string> Failed;
        public abstract void RequestSession(string sessionId);
        public abstract void SendAcknowledgment(StepAcknowledgment acknowledgment);
        public abstract void Close();
        public virtual void CancelSession() => Close();
        protected void PublishConnected(string id, int total) => Connected?.Invoke(id, total);
        protected void PublishStep(string id, string json) => StepReceived?.Invoke(id, json);
        protected void PublishAcknowledgment(string id, string eventId) => Acknowledged?.Invoke(id, eventId);
        protected void PublishFailure(string id, string message) => Failed?.Invoke(id, message);
    }
}
