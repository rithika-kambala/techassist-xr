#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TechAssistXR.Hybrid.Editor
{
    [InitializeOnLoad]
    public static class MachineWorkbenchSmokeTest
    {
        const string Key = "TechAssistXR.WorkbenchSmokeRunning";
        static double deadline;
        static double nextActionTime;
        static int completed;
        static bool started;
        static int analysisPhase;
        static MachineWorkbenchSmokeTest() { EditorApplication.update += Tick; }
        [MenuItem("TechAssist XR/Run Workbench Smoke Test")]
        public static void Run()
        {
            if (!UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("TechnicianAIWorkbench")) {
                Debug.LogError("Open the generated desktop demo scene first."); return;
            }
            SessionState.SetBool(Key, true); started = false; completed = 0; analysisPhase = 0;
            deadline = EditorApplication.timeSinceStartup + 45;
            EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 45;
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timed out."); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try {
                var manager = UnityEngine.Object.FindObjectOfType<TechnicianSessionManager>();
                var input = UnityEngine.Object.FindObjectOfType<TechnicianInputAdapter>();
                var guidance = UnityEngine.Object.FindObjectOfType<MRGuidanceManager>();
                if (manager == null || input == null || guidance == null) throw new Exception("Demo services missing.");
                var diagnosis = UnityEngine.Object.FindObjectOfType<MachineDiagnosisTransport>();
                var workbench = UnityEngine.Object.FindObjectOfType<MachineWorkbench>();
                if (analysisPhase == 0) { analysisPhase = 1; diagnosis.Analyze("machine noisy"); return; }
                if (analysisPhase == 1) {
                    if (diagnosis.Busy) return;
                    if (diagnosis.Result != null || !diagnosis.Status.Contains("oil-leak")) throw new Exception("Unsupported report not rejected.");
                    analysisPhase = 2; workbench.Analyze(); return;
                }
                if (!started) {
                    if (diagnosis.Busy) return;
                    if (diagnosis.Result == null || diagnosis.Result.steps.Length != 4) throw new Exception("Oil leak plan missing.");
                    started = true; workbench.StartRepair(); return;
                }
                if (!string.IsNullOrEmpty(manager.Error)) throw new Exception(manager.Error);
                if (manager.Session.State == TechnicianState.INSTRUCTION_RECEIVED) {
                    if (EditorApplication.timeSinceStartup < nextActionTime) return;
                    var step = manager.Session.CurrentStep;
                    if (!guidance.TryGetTarget(step.target_component, out var target)) throw new Exception("Target resolution failed.");
                    var complete = GameObject.Find("CompleteButton").GetComponent<Button>();
                    if (complete.interactable) throw new Exception("Completion enabled before selection.");
                    input.SelectComponent(GameObject.Find(step.target_component == "control_panel" ? "motor_housing" : "control_panel"));
                    if (manager.CanComplete || complete.interactable) throw new Exception("Wrong component accepted.");
                    input.SelectComponent(target.gameObject);
                    if (!manager.CanComplete || !complete.interactable) throw new Exception("Correct selection failed.");
                    if (!Array.Exists(UnityEngine.Object.FindObjectsOfType<LineRenderer>(), line => line.name.StartsWith("TargetArrow") && line.name.EndsWith("(Clone)"))) throw new Exception("Guidance marker missing.");
                    complete.onClick.Invoke();
                    if (manager.Session.State != TechnicianState.STEP_COMPLETED) throw new Exception("Completion did not enter pending state.");
                    manager.CompleteCurrentStep(); // Must not duplicate the pending step.
                    completed++;
                    nextActionTime = EditorApplication.timeSinceStartup + 0.6;
                }
                if (manager.Session.State == TechnicianState.SESSION_ENDED) {
                    if (completed != 4 || manager.Session.CompletedSteps != 4) throw new Exception("Unexpected completed count.");
                    if (manager.Session.PendingAcknowledgment != null) throw new Exception("Acknowledgment still pending.");
                    Finish(true, "4/4 steps: Start/Complete button callbacks, wrong-target rejection, correct selection, guidance marker, acknowledgment and final state passed.");
                }
            } catch (Exception ex) { Finish(false, ex.ToString()); }
        }
        static void Finish(bool success, string detail)
        {
            SessionState.SetBool(Key, false);
            string report = (success ? "PASS" : "FAIL") + "\n" + DateTime.UtcNow.ToString("O") + "\n" + detail;
            File.WriteAllText("Assets/TechAssistXRHybrid/Generated/WorkbenchSmokeTest.txt", report);
            Debug.Log("[TechAssist XR Smoke Test] " + report);
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
