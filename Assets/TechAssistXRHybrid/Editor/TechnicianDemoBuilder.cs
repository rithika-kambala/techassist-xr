#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace TechAssistXR.Hybrid.Editor
{
    public static class TechnicianDemoBuilder
    {
        private const string Root = "Assets/TechAssistXRHybrid";
        [MenuItem("TechAssist XR/Create Desktop Demo Scene")]
        public static void Create()
        {
            if (Application.isPlaying) { Debug.LogWarning("Exit Play mode before building the scene."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (TMP_Settings.defaultFontAsset == null) {
                EditorUtility.DisplayDialog("TextMesh Pro resources", "Import TMP Essential Resources from Window > TextMeshPro, then run this command again.", "OK"); return;
            }
            string output = Root + "/Generated";
            Directory.CreateDirectory(output);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var services = new GameObject("TechnicianSystems");
            var mode = services.AddComponent<TechnicianModeController>();
            var guidance = services.AddComponent<MRGuidanceManager>();
            var input = services.AddComponent<TechnicianInputAdapter>();
            var session = services.AddComponent<TechnicianSessionManager>();
            var transport = services.AddComponent<DemoTechnicianTransport>();
            var ui = services.AddComponent<TechnicianUIManager>();
            var rig = new GameObject("DesktopRig");
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(rig.transform);
            cameraObject.transform.position = new Vector3(0, 1.5f, -5);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.09f);
            var light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(40, -25, 0);
            var machine = new GameObject("MachineRoot");
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) { Debug.LogError("No supported Lit shader found."); return; }
            var material = SaveMaterial(output, "Machine", shader, new Color(0.25f, 0.4f, 0.55f));
            var highlight = SaveMaterial(output, "Highlight", shader, new Color(1f, 0.65f, 0.05f));
            Component(machine.transform, "ControlPanel", new Vector3(-1.6f, 1.3f, 0), new Vector3(0.8f, 1.1f, 0.5f), material);
            Component(machine.transform, "MotorHousing", new Vector3(-0.5f, 1.3f, 0), new Vector3(0.8f, 0.7f, 0.7f), material);
            Component(machine.transform, "FilterCover", new Vector3(0.5f, 1.3f, 0), new Vector3(0.65f, 0.8f, 0.3f), material);
            // A camera-facing down arrow made from line segments; no collider or custom shader needed.
            var arrow = new GameObject("TargetArrow");
            var line = arrow.AddComponent<LineRenderer>();
            line.useWorldSpace = false; line.widthMultiplier = 0.035f; line.positionCount = 5;
            line.SetPositions(new[] { new Vector3(0, 0.35f, 0), Vector3.zero, new Vector3(-0.12f, 0.14f, 0), Vector3.zero, new Vector3(0.12f, 0.14f, 0) });
            line.sharedMaterial = highlight;
            var markerPath = AssetDatabase.GenerateUniqueAssetPath(output + "/TargetArrow.prefab");
            var marker = PrefabUtility.SaveAsPrefabAsset(arrow, markerPath);
            Object.DestroyImmediate(arrow);
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            var uiInput = eventSystem.AddComponent<InputSystemUIInputModule>();
            uiInput.AssignDefaultActions();
#else
            var uiInput = eventSystem.AddComponent<StandaloneInputModule>();
#endif
            var canvasObject = new GameObject("TechnicianCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("ProcedurePanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            Rect(panel.GetComponent<RectTransform>(), new Vector2(0.68f, 0.08f), new Vector2(0.98f, 0.92f));
            panel.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.15f, 0.97f);
            var title = Label(panel.transform, "Title", 0.82f, 0.96f, 26);
            var instruction = ScrollText(panel.transform);
            var progress = Label(panel.transform, "Progress", 0.35f, 0.42f, 18);
            var status = Label(panel.transform, "Status", 0.28f, 0.35f, 16);
            var feedback = Label(panel.transform, "Feedback", 0.15f, 0.28f, 15);
            var start = Button(panel.transform, "Start", 0.02f, 0.25f);
            var complete = Button(panel.transform, "Complete", 0.26f, 0.54f);
            var retry = Button(panel.transform, "Retry", 0.55f, 0.76f);
            var end = Button(panel.transform, "End", 0.77f, 0.98f);
            Set(mode, "desktopRig", rig); Set(mode, "desktopCamera", camera); Set(mode, "desktopUiInput", uiInput);
            Set(guidance, "machineRoot", machine.transform); Set(guidance, "mode", mode);
            Set(guidance, "highlightMaterial", highlight); Set(guidance, "markerPrefab", marker);
            Set(input, "mode", mode); Set(input, "guidance", guidance); Set(input, "session", session);
            Set(session, "transport", transport); Set(session, "input", input); Set(session, "guidance", guidance);
            Set(transport, "procedureJson", AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/demo_procedure.json"));
            Set(ui, "session", session); Set(ui, "mode", mode); Set(ui, "canvas", canvas);
            Set(ui, "procedurePanel", panel.GetComponent<RectTransform>());
            Set(ui, "titleText", title); Set(ui, "instructionText", instruction); Set(ui, "progressText", progress);
            Set(ui, "statusText", status); Set(ui, "feedbackText", feedback);
            Set(ui, "startButton", start); Set(ui, "completeButton", complete); Set(ui, "retryButton", retry); Set(ui, "endButton", end);
            string scenePath = AssetDatabase.GenerateUniqueAssetPath(output + "/TechnicianDesktopDemo.unity");
            EditorSceneManager.SaveScene(scene, scenePath);
            Selection.activeGameObject = services;
            Debug.Log("TechAssist XR demo created: " + scenePath + ". Press Play, then Start.");
        }
        private static Material SaveMaterial(string folder, string name, Shader shader, Color color)
        {
            var mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name + ".mat"));
            return mat;
        }
        private static void Component(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; go.AddComponent<TechnicianComponent>();
        }
        private static void Rect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static TMP_Text Label(Transform parent, string name, float bottom, float top, float size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            Rect(go.GetComponent<RectTransform>(), new Vector2(0.05f, bottom), new Vector2(0.95f, top));
            var text = go.AddComponent<TextMeshProUGUI>(); text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = size; text.color = Color.white; text.raycastTarget = false;
            text.richText = false; text.text = name; return text;
        }
        private static TMP_Text ScrollText(Transform parent)
        {
            var viewport = new GameObject("InstructionScroll", typeof(RectTransform), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewport.transform.SetParent(parent, false);
            var rect = viewport.GetComponent<RectTransform>();
            Rect(rect, new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.80f));
            viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            var text = Label(viewport.transform, "Instruction", 0, 1, 21);
            var content = text.rectTransform;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); content.offsetMin = content.offsetMax = Vector2.zero;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = rect; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return text;
        }
        private static Button Button(Transform parent, string label, float left, float right)
        {
            var go = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Rect(go.GetComponent<RectTransform>(), new Vector2(left, 0.02f), new Vector2(right, 0.12f));
            go.GetComponent<Image>().color = new Color(0.12f, 0.4f, 0.55f);
            var text = Label(go.transform, label, 0, 1, 16); text.alignment = TextAlignmentOptions.Center;
            var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>(); return button;
        }
        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
