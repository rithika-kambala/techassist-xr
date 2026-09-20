using System;
using System.Collections;
using UnityEngine;

namespace TechAssistXR.Hybrid
{
    public sealed class DemoTechnicianTransport : TechnicianTransport
    {
        [SerializeField] private TextAsset procedureJson;
        [SerializeField, Min(0)] private float delaySeconds = 0.2f;
        [SerializeField] private bool simulateAcknowledgmentFailure;
        public override void RequestSession(string sessionId)
        {
            Close();
            StartCoroutine(Load(sessionId));
        }
        private IEnumerator Load(string id)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            ProcedureEnvelope envelope = null;
            try { if (procedureJson != null) envelope = JsonUtility.FromJson<ProcedureEnvelope>(procedureJson.text); }
            catch (Exception) { /* Report below, outside the iterator catch. */ }
            if (envelope?.steps == null || envelope.steps.Length == 0) {
                PublishFailure(id, "Assign a valid demo JSON asset with a steps array."); yield break;
            }
            for (int i = 0; i < envelope.steps.Length; i++)
                if (envelope.steps[i] == null || !envelope.steps[i].IsValid || envelope.steps[i].step_number != i + 1) {
                    PublishFailure(id, "Demo steps must be valid and numbered consecutively from 1."); yield break;
                }
            PublishConnected(id, envelope.steps.Length);
            foreach (var step in envelope.steps) PublishStep(id, JsonUtility.ToJson(step));
        }
        public override void SendAcknowledgment(StepAcknowledgment acknowledgment)
        {
            if (acknowledgment != null) StartCoroutine(Acknowledge(acknowledgment.Copy()));
        }
        private IEnumerator Acknowledge(StepAcknowledgment ack)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            if (simulateAcknowledgmentFailure) PublishFailure(ack.session_id, "Simulated delivery failure. Disable it and retry.");
            else PublishAcknowledgment(ack.session_id, ack.event_id);
        }
        public override void Close() => StopAllCoroutines();
        private void OnDisable() => Close();
    }
}
