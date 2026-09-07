using UnityEngine;

public enum SessionState 
{ 
    StartSession, 
    MachineSelected, 
    SessionEnded 
}

public class SessionStateMachine : MonoBehaviour
{
    public static SessionStateMachine Instance;
    public SessionState CurrentState { get; private set; }
    public event System.Action<SessionState> OnStateChanged;

    void Awake() 
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Initialize default state
        SetState(SessionState.StartSession);
    }

    public void SetState(SessionState newState)
    {
        CurrentState = newState;
        Debug.Log($"State Changed to: {CurrentState}");
        OnStateChanged?.Invoke(newState);
    }
}