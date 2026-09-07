using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ComponentRegistry : MonoBehaviour
{
    public static ComponentRegistry Instance { get; private set; }

    [SerializeField]
    private Transform machineRoot;

    [SerializeField]
    private Color highlightColor = Color.yellow;

    private readonly Dictionary<string, GameObject> components =
        new Dictionary<string, GameObject>();

    private Renderer highlightedRenderer;
    private Color originalColor;

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
        RegisterComponents();
    }

    private void RegisterComponents()
    {
        components.Clear();

        if (machineRoot == null)
        {
            Debug.LogError("[TechAssist XR] Machine Root is not assigned.");
            return;
        }

        foreach (Transform child in machineRoot)
        {
            components[child.name] = child.gameObject;

            Debug.Log(
                $"[TechAssist XR] Registered component: {child.name}"
            );
        }
    }

    public GameObject GetComponentById(string componentId)
    {
        if (components.TryGetValue(componentId, out GameObject component))
        {
            return component;
        }

        Debug.LogWarning(
            $"[TechAssist XR] Component not found: {componentId}"
        );

        return null;
    }

    public void HighlightComponent(string componentId)
    {
        ClearHighlight();

        GameObject target = GetComponentById(componentId);

        if (target == null)
            return;

        Renderer renderer = target.GetComponentInChildren<Renderer>();

        if (renderer == null)
        {
            Debug.LogWarning(
                $"[TechAssist XR] No Renderer found on {componentId}"
            );

            return;
        }

        highlightedRenderer = renderer;
        originalColor = renderer.material.color;

        renderer.material.color = highlightColor;

        Debug.Log(
            $"[TechAssist XR] Highlighting: {componentId}"
        );
    }

    public void ClearHighlight()
    {
        if (highlightedRenderer == null)
            return;

        highlightedRenderer.material.color = originalColor;
        highlightedRenderer = null;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            HighlightComponent("isolation_valve");

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            HighlightComponent("filter_housing");

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            HighlightComponent("filter_element");
    }
}