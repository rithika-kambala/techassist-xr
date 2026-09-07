using System;
using UnityEngine;

public enum TechAssistSessionState
{
    Idle,
    Requesting,
    Connected,
    InstructionReceived,
    StepCompleted,
    SessionEnded
}

public class SessionStateMachine : MonoBehaviour
{
    public static SessionStateMachine Instance { get; private set; }

    public TechAssistSessionState CurrentState { get; private set; }

    public event Action<TechAssistSessionState> OnStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        SetState(TechAssistSessionState.Idle);
    }

    public void SetState(TechAssistSessionState newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;

        Debug.Log($"[TechAssist XR] State changed to: {CurrentState}");

        OnStateChanged?.Invoke(CurrentState);
    }

    // Technician presses START SESSION
    public void StartSession()
    {
        SetState(TechAssistSessionState.Requesting);
    }

    // Expert/backend connects
    public void SessionConnected()
    {
        SetState(TechAssistSessionState.Connected);
    }

    // A procedure step/instruction arrives
    public void InstructionReceived()
    {
        SetState(TechAssistSessionState.InstructionReceived);
    }

    // Technician finishes the current step
    public void CompleteStep()
    {
        SetState(TechAssistSessionState.StepCompleted);
    }

    // Session is finished
    public void EndSession()
    {
        SetState(TechAssistSessionState.SessionEnded);
    }

    // Useful while testing in Unity Editor
    public void ResetSession()
    {
        SetState(TechAssistSessionState.Idle);
    }
}