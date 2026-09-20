using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TechAssistXR.Hybrid
{
    public sealed class MachineWorkbench : MonoBehaviour
    {
        [SerializeField] private MachineDiagnosisTransport diagnosis;
        [SerializeField] private TechnicianSessionManager session;
        [SerializeField] private TMP_InputField reportInput;
        [SerializeField] private TMP_Text status, evidence, plan, badge;
        [SerializeField] private Button analyzeButton, repairButton, backButton;
        [SerializeField] private GameObject infoPage, evidencePage, procedurePanel;
        private void OnEnable() {
            diagnosis.Changed += Refresh; session.Changed += Refresh;
            analyzeButton.onClick.AddListener(Analyze); repairButton.onClick.AddListener(StartRepair); backButton.onClick.AddListener(Back);
        }
        private void Start() { Back(); }
        private void OnDisable() {
            diagnosis.Changed -= Refresh; session.Changed -= Refresh;
            analyzeButton.onClick.RemoveListener(Analyze); repairButton.onClick.RemoveListener(StartRepair); backButton.onClick.RemoveListener(Back);
        }
        public void Analyze() { diagnosis.Analyze(reportInput.text); }
        public void StartRepair() {
            if(!diagnosis.HasExecutablePlan || diagnosis.Busy) return;
            infoPage.SetActive(false); evidencePage.SetActive(false); procedurePanel.SetActive(true);
            session.StartSession(); Refresh();
        }
        public void Back() {
            session.EndSession(); procedurePanel.SetActive(false); infoPage.SetActive(true); evidencePage.SetActive(true); Refresh();
        }
        private void Refresh() {
            badge.text = diagnosis.IsLive ? (diagnosis.ProviderMode == "mock" ? "BACKEND MOCK AI  /  SERVER CONFIRMED STEPS" : "LIVE BACKEND  /  SERVER CONFIRMED STEPS") : "SIMULATED AI  /  NO API CALLS OR CREDITS";
            status.text=diagnosis.Status;
            analyzeButton.interactable=!diagnosis.Busy;
            repairButton.interactable=diagnosis.HasExecutablePlan && !diagnosis.Busy;
            evidence.text=diagnosis.Profile==null ? "Loading machine profile…" : string.Join("\n\n",diagnosis.Profile.sources.Select(s=>s.id+"  /  "+s.title+"\n"+s.text));
            var result=diagnosis.Result;
            plan.text=result==null ? "The report and machine record become a source-linked inspection plan.\n\nTry: Oil leaking near pump\n\nOffline mode is a scripted demonstration, not a real AI diagnosis." : result.summary+"\n\n"+string.Join("\n",result.steps.Select(s=>s.step_number+". "+s.title))+"\n\nSources: "+string.Join(", ",result.source_ids);
        }
    }
}
