using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TechAssistXR.Hybrid
{
    // Example backend contract; not a direct OpenAI client and not the legacy WebSocket protocol.
    public sealed class HttpTechnicianTransport : TechnicianTransport
    {
        [SerializeField] private string baseUrl = "";
        [SerializeField, Min(1)] private int timeoutSeconds = 20;
        private UnityWebRequest activeRequest;
        // Set at runtime from your login flow. Never serialize service credentials into a build.
        public string AccessToken { private get; set; }
        [Serializable] private sealed class StartRequest { public string session_id; }
        [Serializable] private sealed class StartResponse { public string session_id; public ProcedureStep[] steps; }
        [Serializable] private sealed class AckResponse { public string session_id; public string event_id; public bool accepted; }

        public override void RequestSession(string sessionId)
        {
            Close();
            StartCoroutine(Post("/technician/sessions", sessionId,
                JsonUtility.ToJson(new StartRequest { session_id = sessionId }), null, body => {
                    var response = JsonUtility.FromJson<StartResponse>(body);
                    if (response == null || response.session_id != sessionId || response.steps == null || response.steps.Length == 0)
                        throw new FormatException("Invalid session response.");
                    for (int i = 0; i < response.steps.Length; i++)
                        if (response.steps[i] == null || !response.steps[i].IsValid || response.steps[i].step_number != i + 1)
                            throw new FormatException("Expected valid steps numbered 1..N in order.");
                    PublishConnected(sessionId, response.steps.Length);
                    foreach (var step in response.steps) PublishStep(sessionId, JsonUtility.ToJson(step));
                }));
        }
        public override void SendAcknowledgment(StepAcknowledgment acknowledgment)
        {
            if (acknowledgment == null) return;
            var ack = acknowledgment.Copy();
            if (activeRequest != null) { PublishFailure(ack.session_id, "Transport busy; retry completion."); return; }
            StartCoroutine(Post("/technician/step-completions", ack.session_id, JsonUtility.ToJson(ack), ack.event_id, body => {
                var response = JsonUtility.FromJson<AckResponse>(body);
                if (response == null || !response.accepted || response.session_id != ack.session_id || response.event_id != ack.event_id)
                    throw new FormatException("Backend did not confirm this completion event.");
                PublishAcknowledgment(ack.session_id, ack.event_id);
            }));
        }
        private IEnumerator Post(string path, string id, string json, string idempotencyKey, Action<string> onSuccess)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != "https" && !(Debug.isDebugBuild && uri.Scheme == "http" && uri.IsLoopback))) {
                PublishFailure(id, "Configure an HTTPS backend URL (localhost HTTP allowed in development)."); yield break;
            }
            using (var request = new UnityWebRequest(baseUrl.TrimEnd('/') + path, "POST")) {
                activeRequest = request;
                request.timeout = Mathf.Max(1, timeoutSeconds);
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(AccessToken)) request.SetRequestHeader("Authorization", "Bearer " + AccessToken);
                if (!string.IsNullOrEmpty(idempotencyKey)) request.SetRequestHeader("Idempotency-Key", idempotencyKey);
                yield return request.SendWebRequest();
                activeRequest = null;
                if (request.result != UnityWebRequest.Result.Success) {
                    PublishFailure(id, "Backend request failed (HTTP " + request.responseCode + "). Retry or end the session."); yield break;
                }
                try { onSuccess(request.downloadHandler.text); }
                catch (Exception ex) { PublishFailure(id, "Invalid backend response: " + ex.Message); }
            }
        }
        public override void Close()
        {
            if (activeRequest != null) { activeRequest.Abort(); activeRequest.Dispose(); activeRequest = null; }
            StopAllCoroutines();
        }
        private void OnDisable() => Close();
    }
}
