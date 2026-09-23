using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace TechAssistXR.Hybrid
{
    public sealed class BackendConnectionPanel : MonoBehaviour
    {
        [SerializeField] private MachineDiagnosisTransport diagnosis;
        [SerializeField] private MachineWorkbench workbench;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_InputField urlInput, tokenInput;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Button openButton, connectButton, offlineButton, closeButton;
        private void OnEnable() {
            openButton.onClick.AddListener(Open); connectButton.onClick.AddListener(Connect);
            offlineButton.onClick.AddListener(Offline); closeButton.onClick.AddListener(Hide);
            diagnosis.Changed += Refresh;
        }
        private void OnDisable() {
            openButton.onClick.RemoveListener(Open); connectButton.onClick.RemoveListener(Connect);
            offlineButton.onClick.RemoveListener(Offline); closeButton.onClick.RemoveListener(Hide);
            diagnosis.Changed -= Refresh; tokenInput.text = "";
        }
        private void Open() { workbench.Back(); panel.SetActive(true); Refresh(); }
        private void Hide() { panel.SetActive(false); tokenInput.text = ""; }
        private void Connect() { diagnosis.ConfigureLive(urlInput.text.Trim(), tokenInput.text); tokenInput.text = ""; }
        private void Offline() { diagnosis.UseOffline(); Hide(); }
        private void Refresh() { status.text = diagnosis.Status; connectButton.interactable = !diagnosis.Busy; offlineButton.interactable = !diagnosis.Busy; }
    }
}
