using UnityEngine;
using UnityEngine.InputSystem;

public class ProcedureManager : MonoBehaviour
{
    public static ProcedureManager Instance { get; private set; }

    [SerializeField]
    private string procedureResourceName = "demo_procedure";

    private ProcedureData procedure;
    private int currentStepIndex = -1;

    public ProcedureStep CurrentStep { get; private set; }

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
        LoadProcedure();
        StartProcedure();
    }

    private void LoadProcedure()
    {
        TextAsset jsonFile =
            Resources.Load<TextAsset>(procedureResourceName);

        if (jsonFile == null)
        {
            Debug.LogError(
                "[TechAssist XR] Procedure JSON not found."
            );
            return;
        }

        procedure =
            JsonUtility.FromJson<ProcedureData>(jsonFile.text);

        Debug.Log(
            $"[TechAssist XR] Loaded procedure: {procedure.procedure_id}"
        );
    }

    public void StartProcedure()
    {
        if (procedure == null ||
            procedure.steps == null ||
            procedure.steps.Length == 0)
        {
            Debug.LogError(
                "[TechAssist XR] No procedure steps available."
            );
            return;
        }

        currentStepIndex = 0;

        SessionStateMachine.Instance.SessionConnected();

        ShowCurrentStep();
    }

    private void ShowCurrentStep()
    {
        CurrentStep = procedure.steps[currentStepIndex];

        Debug.Log(
            $"[TechAssist XR] STEP {CurrentStep.step_number}: " +
            $"{CurrentStep.title}"
        );

        Debug.Log(
            $"[TechAssist XR] Instruction: {CurrentStep.instruction}"
        );

        Debug.Log(
            $"[TechAssist XR] Target: {CurrentStep.target_component}"
        );

        ComponentRegistry.Instance.HighlightComponent(
            CurrentStep.target_component
        );

        SessionStateMachine.Instance.InstructionReceived();
    }

    public void CompleteCurrentStep()
    {
        if (CurrentStep == null)
            return;

        Debug.Log(
            $"[TechAssist XR] Completed step {CurrentStep.step_number}"
        );

        SessionStateMachine.Instance.CompleteStep();

        currentStepIndex++;

        if (currentStepIndex >= procedure.steps.Length)
        {
            EndProcedure();
            return;
        }

        ShowCurrentStep();
    }

    private void EndProcedure()
    {
        ComponentRegistry.Instance.ClearHighlight();

        CurrentStep = null;

        SessionStateMachine.Instance.EndSession();

        Debug.Log(
            "[TechAssist XR] Procedure completed."
        );
    }

    private void Update()
    {
        // Temporary Editor test:
        // press SPACE to complete the current step.
        if (Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CompleteCurrentStep();
        }
    }
}