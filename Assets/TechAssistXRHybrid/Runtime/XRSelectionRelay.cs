using UnityEngine;
namespace TechAssistXR.Hybrid
{
    // Inspector event wiring works with XRI 2.x and 3.x; no package namespace dependency.
    [RequireComponent(typeof(TechnicianComponent))]
    public sealed class XRSelectionRelay : MonoBehaviour
    {
        [SerializeField] private TechnicianInputAdapter adapter;
        public void Select() { if (adapter != null) adapter.SelectFromXR(gameObject); }
    }
}
