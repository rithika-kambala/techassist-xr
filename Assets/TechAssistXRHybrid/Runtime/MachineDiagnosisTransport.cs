using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace TechAssistXR.Hybrid
{
    [Serializable] public sealed class MachineSource { public string id; public string title; public string text; }
    [Serializable] public sealed class MachineProfile {
        public string machine_id, name, description;
        public string[] components;
        public MachineSource[] sources;
    }
    [Serializable] public sealed class DiagnosisResult {
        public string summary;
        public string diagnosis_id, mode;
        public bool needs_expert, training_only;
        public string[] source_ids;
        public ProcedureStep[] steps;
    }
    public sealed class MachineDiagnosisTransport : TechnicianTransport
    {
        [SerializeField] private TextAsset machineProfile;
        [SerializeField] private bool useLiveBackend;
        public MachineProfile Profile { get; private set; }
        public DiagnosisResult Result { get; private set; }
        public string Status { get; private set; } = "Ready for a technician report.";
        public string Report { get; private set; }
        private string accessToken, baseUrl, liveSessionId, requestId, requestReport;
        public string BackendUrl => baseUrl;
        public string ProviderMode => Result?.mode;
        public bool HasExecutablePlan => Result != null && !Result.needs_expert && Result.steps != null && Result.steps.Length > 0;
        [Serializable] private sealed class SessionRequest { public string session_id, diagnosis_id; public bool reviewed = true; }
        [Serializable] private sealed class SessionReply { public string session_id; public ProcedureStep[] steps; }
        [Serializable] private sealed class AckReply { public string session_id, event_id; public bool accepted; }
        [Serializable] private sealed class ErrorDetail { public string code, message; }
        [Serializable] private sealed class ErrorReply { public ErrorDetail error; }
        public bool Busy { get; private set; }
        public bool IsLive => useLiveBackend;
        public event Action Changed;
        private UnityWebRequest request;
        [Serializable] private sealed class DiagnosisRequest { public string technician_report, machine_id, request_id; }
        private void Awake() {
            // Never embed an access token or automatically trust a serialized live flag.
            useLiveBackend = false;
            try { if (machineProfile != null) Profile = JsonUtility.FromJson<MachineProfile>(machineProfile.text); }
            catch (Exception) { Status = "Invalid machine profile."; }
        }
        public void ConfigureLive(string url, string token)
        {
            if (Busy || !string.IsNullOrEmpty(liveSessionId)) { UpdateStatus("End the current session before reconnecting."); return; }
            if (!ValidUrl(url) || string.IsNullOrWhiteSpace(token)) { UpdateStatus("Enter an HTTPS backend URL and technician token."); return; }
            Busy = true; StartCoroutine(Connect(url.TrimEnd('/'), token.Trim()));
        }
        private static bool ValidUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) return false;
            if (uri.Scheme == "https") return true;
#if UNITY_EDITOR
            return uri.Scheme == "http" && uri.IsLoopback;
#else
            return false;
#endif
        }
        private IEnumerator Connect(string url, string token)
        {
            UpdateStatus("Connecting to authenticated machine catalog…");
            using (var web = UnityWebRequest.Get(url + "/api/machines/A102")) {
                request = web; web.timeout = 30; web.SetRequestHeader("Authorization", "Bearer " + token);
                yield return web.SendWebRequest(); request = null;
                if (web.result != UnityWebRequest.Result.Success) { Busy = false; UpdateStatus(ReadError(web)); yield break; }
                MachineProfile profile = null;
                try { profile = JsonUtility.FromJson<MachineProfile>(web.downloadHandler.text); } catch (Exception) { }
                if (profile == null || profile.machine_id != "A102" || profile.sources == null || profile.components == null) { Busy = false; UpdateStatus("Backend returned an invalid machine catalog."); yield break; }
                Profile = profile; baseUrl = url; accessToken = token; useLiveBackend = true;
                Result = null; Busy = false; requestId = null; UpdateStatus("Backend connected. Enter a report to request a source-grounded plan.");
            }
        }
        public void UseOffline()
        {
            Close(); useLiveBackend = false; accessToken = null; baseUrl = null; liveSessionId = null;
            if (machineProfile != null) Profile = JsonUtility.FromJson<MachineProfile>(machineProfile.text);
            Result = null; requestId = null; UpdateStatus("Offline demonstration selected. No API calls.");
        }
        private static string ReadError(UnityWebRequest web)
        {
            try { var result = JsonUtility.FromJson<ErrorReply>(web.downloadHandler.text); if (!string.IsNullOrEmpty(result?.error?.message)) return result.error.message; } catch (Exception) { }
            return "Backend request failed (HTTP " + web.responseCode + "). Check connection or retry.";
        }
        public void Analyze(string report)
        {
            if (Busy) return;
            Result = null; Report = report?.Trim();
            if (Profile == null || Profile.components == null || Profile.sources == null) { UpdateStatus("Machine profile missing."); return; }
            if (string.IsNullOrWhiteSpace(Report) || Report.Length > 1000) { UpdateStatus("Enter a fault report between 1 and 1000 characters."); return; }
            Busy = true; StartCoroutine(AnalyzeRoutine());
        }
        private IEnumerator AnalyzeRoutine()
        {
            UpdateStatus("1 / 3  Reading technician report + machine identity…");
            yield return new WaitForSecondsRealtime(0.6f);
            UpdateStatus("2 / 3  Retrieving machine document excerpts…");
            yield return new WaitForSecondsRealtime(0.6f);
            DiagnosisResult result = null;
            if (useLiveBackend) {
                if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(accessToken)) { Fail("Connect the backend first."); yield break; }
                if (requestId == null || requestReport != Report) { requestId = Guid.NewGuid().ToString("N"); requestReport = Report; }
                using (var web = Post("/api/diagnoses", JsonUtility.ToJson(new DiagnosisRequest { technician_report = Report, machine_id = Profile.machine_id, request_id = requestId }))) {
                    request = web; yield return web.SendWebRequest(); request = null;
                    if (web.result != UnityWebRequest.Result.Success) { Fail(ReadError(web)); yield break; }
                    try { result = JsonUtility.FromJson<DiagnosisResult>(web.downloadHandler.text); } catch (Exception) { result = null; }
                }
            } else {
                string normalized = Report.ToLowerInvariant();
                if (!normalized.Contains("oil") || !(normalized.Contains("leak") || normalized.Contains("drip"))) {
                    Fail("Offline example supports oil-leak reports only. Add detail such as 'Oil leaking near pump'."); yield break;
                }
                result = new DiagnosisResult {
                    summary = "Possible oil-path leak; the report alone cannot identify the failed part. Inspect the reservoir, pump seal and filter cover in the training model.",
                    source_ids = new[] { "TRAIN-01", "TRAIN-02", "TRAIN-03" },
                    steps = new[] {
                        Step(1,"Identify the isolation controls","Select control_panel. In this simulation, acknowledge the isolation checkpoint. Real equipment requires the approved isolation procedure before any inspection.","control_panel"),
                        Step(2,"Locate the reservoir","Select oil_reservoir. Compare its position with the reported leak location; no reservoir opening is performed in this demo.","oil_reservoir"),
                        Step(3,"Inspect the pump seal location","Select pump_seal, the amber ring beside the motor. This is a candidate inspection point, not a confirmed diagnosis. Record the location for a qualified maintainer.","pump_seal"),
                        Step(4,"Check the filter-cover location","Select filter_cover. Acknowledge this final inspection point. Do not infer torque, replacement parts or return-to-service approval from this synthetic guide.","filter_cover")
                    }
                };
            }
            UpdateStatus("3 / 3  Validating source references + 3D component IDs…");
            yield return new WaitForSecondsRealtime(0.6f);
            if (result != null && useLiveBackend && string.IsNullOrEmpty(result.diagnosis_id)) { Fail("Backend omitted diagnosis ID."); yield break; }
            string error = Validate(result);
            if (error != null) { Fail(error); yield break; }
            Result = result; Busy = false;
            UpdateStatus(useLiveBackend ? (result.needs_expert ? "Expert review required. No executable plan." : "Backend response received • review the plan before starting.") : "SIMULATED AI • source-linked example ready for review.");
        }
        private string Validate(DiagnosisResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.summary) || result.steps == null || (!result.needs_expert && result.steps.Length == 0) || result.steps.Length > 20)
                return "Rejected an incomplete diagnosis response.";
            if (result.source_ids == null || result.source_ids.Length == 0 || result.source_ids.Any(id => !Profile.sources.Any(s => s.id == id)))
                return "Rejected unknown or missing source references.";
            for (int i=0;i<result.steps.Length;i++) {
                var step=result.steps[i];
                if (step == null || !step.IsValid || step.step_number != i+1 || !Profile.components.Contains(step.target_component))
                    return "Rejected a step with invalid fields or an unknown machine component.";
            }
            return null;
        }
        private static ProcedureStep Step(int n,string title,string instruction,string target) => new ProcedureStep {step_number=n,title=title,instruction=instruction,target_component=target};
        private void Fail(string message) { Busy=false; Result=null; UpdateStatus(message); }
        private void UpdateStatus(string text) { Status=text; Changed?.Invoke(); }
        private UnityWebRequest Post(string path, string json)
        {
            var web = new UnityWebRequest(baseUrl + path, "POST"); web.timeout = 60;
            web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)); web.downloadHandler = new DownloadHandlerBuffer();
            web.SetRequestHeader("Content-Type", "application/json"); web.SetRequestHeader("Authorization", "Bearer " + accessToken); return web;
        }
        public override void RequestSession(string id)
        {
            if (!HasExecutablePlan || Busy) { PublishFailure(id, "Analyze and review an executable plan first."); return; }
            if (useLiveBackend) { StartCoroutine(StartRemoteSession(id)); return; }
            PublishConnected(id, Result.steps.Length);
            foreach (var step in Result.steps) PublishStep(id, JsonUtility.ToJson(step));
        }
        private IEnumerator StartRemoteSession(string id)
        {
            liveSessionId = id;
            using (var web = Post("/api/sessions", JsonUtility.ToJson(new SessionRequest { session_id = id, diagnosis_id = Result.diagnosis_id }))) {
                request = web; yield return web.SendWebRequest(); request = null;
                if (web.result != UnityWebRequest.Result.Success) { PublishFailure(id, ReadError(web)); yield break; }
                SessionReply reply = null;
                try { reply = JsonUtility.FromJson<SessionReply>(web.downloadHandler.text); } catch (Exception) { }
                if (reply == null || reply.session_id != id || reply.steps == null || reply.steps.Length == 0) { PublishFailure(id, "Invalid session response."); yield break; }
                PublishConnected(id, reply.steps.Length);
                foreach (var step in reply.steps) PublishStep(id, JsonUtility.ToJson(step));
            }
        }
        public override void SendAcknowledgment(StepAcknowledgment ack) { if (ack != null) StartCoroutine(Acknowledge(ack.Copy())); }
        private IEnumerator Acknowledge(StepAcknowledgment ack)
        {
            if (!useLiveBackend) { yield return new WaitForSecondsRealtime(0.2f); PublishAcknowledgment(ack.session_id, ack.event_id); yield break; }
            using (var web = Post("/api/step-completions", JsonUtility.ToJson(ack))) {
                request = web; yield return web.SendWebRequest(); request = null;
                if (web.result != UnityWebRequest.Result.Success) { PublishFailure(ack.session_id, ReadError(web)); yield break; }
                AckReply reply = null;
                try { reply = JsonUtility.FromJson<AckReply>(web.downloadHandler.text); } catch (Exception) { }
                if (reply == null || !reply.accepted || reply.session_id != ack.session_id || reply.event_id != ack.event_id) { PublishFailure(ack.session_id, "Backend did not confirm this completion event."); yield break; }
                PublishAcknowledgment(ack.session_id, ack.event_id);
            }
        }
        public override void CancelSession()
        {
            var id = liveSessionId; Close(); liveSessionId = null;
            if (useLiveBackend && !string.IsNullOrEmpty(id) && isActiveAndEnabled) StartCoroutine(EndRemoteSession(id));
        }
        private IEnumerator EndRemoteSession(string id)
        {
            using (var web = Post("/api/sessions/" + Uri.EscapeDataString(id) + "/end", "{}")) {
                yield return web.SendWebRequest();
                if (web.result != UnityWebRequest.Result.Success) UpdateStatus("Local session ended; remote cancellation was not confirmed. " + ReadError(web));
            }
        }
        public override void Close() {
            if(request!=null) { request.Abort(); request.Dispose(); request=null; }
            StopAllCoroutines(); Busy=false;
        }
        private void OnDisable() => Close();
    }
}
