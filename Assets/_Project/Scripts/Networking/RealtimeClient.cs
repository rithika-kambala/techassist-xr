using System;
using System.Text;
using UnityEngine;
using NativeWebSocket;

[Serializable]
public class RealtimePayload
{
    public int step_number;
    public string title;
    public string instruction;
    public string target_component;

    public string reason;
    public string role;
    public string status;
}

[Serializable]
public class RealtimeEnvelope
{
    public string type;
    public string sessionId;
    public string from;
    public string eventId;
    public RealtimePayload payload;
}

public class RealtimeClient : MonoBehaviour
{
    [SerializeField]
    private string serverUrl = "ws://localhost:8080";

    [SerializeField]
    private string sessionId = "A102-local-demo";

    [SerializeField]
    private string role = "technician";

    private WebSocket websocket;

    private async void Start()
    {
        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () =>
        {
            Debug.Log("[TechAssist XR] WebSocket CONNECTED");
            SendJoin();
        };

        websocket.OnError += (error) =>
        {
            Debug.LogError(
                $"[TechAssist XR] WebSocket error: {error}"
            );
        };

        websocket.OnClose += (code) =>
        {
            Debug.Log(
                $"[TechAssist XR] WebSocket closed: {code}"
            );
        };

        websocket.OnMessage += (bytes) =>
        {
            string message = Encoding.UTF8.GetString(bytes);

            Debug.Log(
                $"[TechAssist XR] WS RECEIVED: {message}"
            );

            HandleMessage(message);
        };

        Debug.Log(
            $"[TechAssist XR] Connecting to {serverUrl}..."
        );

        await websocket.Connect();
    }

    private void HandleMessage(string json)
    {
        RealtimeEnvelope envelope;

        try
        {
            envelope = JsonUtility.FromJson<RealtimeEnvelope>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                $"[TechAssist XR] Could not parse WS message: {ex.Message}"
            );

            return;
        }

        if (envelope == null || string.IsNullOrEmpty(envelope.type))
            return;

        switch (envelope.type)
        {
            case "SESSION_JOINED":
                Debug.Log("[TechAssist XR] Session joined.");

                if (SessionStateMachine.Instance != null)
                    SessionStateMachine.Instance.SessionConnected();

                break;

            case "STEP_STARTED":
                HandleStepStarted(envelope.payload);
                break;

            case "ANNOTATION_CREATED":
                HandleAnnotation(envelope.payload);
                break;

            case "SESSION_ENDED":
                Debug.Log("[TechAssist XR] Session ended remotely.");

                if (ComponentRegistry.Instance != null)
                    ComponentRegistry.Instance.ClearHighlight();

                if (SessionStateMachine.Instance != null)
                    SessionStateMachine.Instance.EndSession();

                break;

            case "CONNECTION_STATE":
                Debug.Log(
                    $"[TechAssist XR] Connection state: {envelope.payload?.status}"
                );
                break;
        }
    }

    private void HandleStepStarted(RealtimePayload payload)
    {
        if (payload == null)
            return;

        Debug.Log(
            $"[TechAssist XR] REMOTE STEP {payload.step_number}: {payload.title}"
        );

        Debug.Log(
            $"[TechAssist XR] Instruction: {payload.instruction}"
        );

        Debug.Log(
            $"[TechAssist XR] Target component: {payload.target_component}"
        );

        if (ComponentRegistry.Instance != null)
        {
            ComponentRegistry.Instance.HighlightComponent(
                payload.target_component
            );
        }

        if (SessionStateMachine.Instance != null)
        {
            SessionStateMachine.Instance.InstructionReceived();
        }
    }

    private void HandleAnnotation(RealtimePayload payload)
    {
        if (payload == null ||
            string.IsNullOrEmpty(payload.target_component))
            return;

        Debug.Log(
            $"[TechAssist XR] Annotation received for: {payload.target_component}"
        );

        if (ComponentRegistry.Instance != null)
        {
            ComponentRegistry.Instance.HighlightComponent(
                payload.target_component
            );
        }
    }

    private async void SendJoin()
    {
        if (websocket == null ||
            websocket.State != WebSocketState.Open)
        {
            return;
        }

        string joinMessage =
            $"{{\"type\":\"JOIN\",\"sessionId\":\"{sessionId}\",\"role\":\"{role}\"}}";

        await websocket.SendText(joinMessage);

        Debug.Log(
            $"[TechAssist XR] JOIN sent: {sessionId} as {role}"
        );
    }

    private void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            await websocket.Close();
        }
    }
}