using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class MachineSelectHandler : MonoBehaviour
{
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    private Renderer cubeRenderer;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        cubeRenderer = GetComponent<Renderer>();
    }

    void OnEnable()
    {
        interactable.selectEntered.AddListener(OnMachineSelected);
    }

    void OnDisable()
    {
        interactable.selectEntered.RemoveListener(OnMachineSelected);
    }

    private void OnMachineSelected(SelectEnterEventArgs args)
    {
        // Change color to green as visual feedback
        if (cubeRenderer != null)
        {
            cubeRenderer.material.color = Color.green;
        }

        // Trigger the state machine change
        if (SessionStateMachine.Instance != null)
        {
            SessionStateMachine.Instance.SetState(SessionState.MachineSelected);
        }
    }
}