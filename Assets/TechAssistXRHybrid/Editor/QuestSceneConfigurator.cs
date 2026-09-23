#if UNITY_EDITOR
using System;
using System.Linq;
using TechAssistXR.Hybrid;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;

namespace TechAssistXR.Hybrid.Editor
{
    public static class QuestSceneConfigurator
    {
        private const string ScenePath = "Assets/TechAssistXRHybrid/Generated/TechnicianAIWorkbench 1.unity";
        private const string RigPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.4.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
        private static readonly string[] RequiredTargets = { "pressure_gauge", "isolation_valve", "control_panel" };

        [MenuItem("TechAssist XR/Configure Quest Demo")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play mode before configuring the Quest demo.");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var systems = GameObject.Find("TechnicianSystems");
            var canvasObject = GameObject.Find("TechnicianCanvas");
            var eventSystemObject = GameObject.Find("EventSystem");
            var machineRoot = GameObject.Find("MachineRoot");
            if (systems == null || canvasObject == null || eventSystemObject == null || machineRoot == null)
                throw new InvalidOperationException("The active TechAssist workbench scene is missing a required root object.");

            var mode = systems.GetComponent<TechnicianModeController>();
            var adapter = systems.GetComponent<TechnicianInputAdapter>();
            if (mode == null || adapter == null)
                throw new InvalidOperationException("TechnicianModeController or TechnicianInputAdapter is missing.");

            var questRig = GameObject.Find("QuestRig");
            if (questRig == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
                if (prefab == null)
                    throw new InvalidOperationException("The installed XRI Starter Assets rig prefab is missing.");
                questRig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                questRig.name = "QuestRig";
            }

            var questCamera = questRig.GetComponentInChildren<Camera>(true);
            if (questCamera == null || questRig.GetComponent<XROrigin>() == null)
                throw new InvalidOperationException("The XRI Starter Assets rig does not contain a valid XR Origin and camera.");

            if (UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>() == null)
                new GameObject("XR Interaction Manager", typeof(XRInteractionManager));

            var desktopInput = eventSystemObject.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            var questInput = eventSystemObject.GetComponent<XRUIInputModule>();
            if (questInput == null) questInput = eventSystemObject.AddComponent<XRUIInputModule>();
            questInput.enabled = false;

            var canvas = canvasObject.GetComponent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("TechnicianCanvas has no Canvas component.");
            if (canvasObject.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();

            var modeSettings = new SerializedObject(mode);
            modeSettings.FindProperty("questRig").objectReferenceValue = questRig;
            modeSettings.FindProperty("questCamera").objectReferenceValue = questCamera;
            modeSettings.FindProperty("questUiInput").objectReferenceValue = questInput;
            modeSettings.ApplyModifiedPropertiesWithoutUndo();

            foreach (var targetName in RequiredTargets)
            {
                var matches = machineRoot.GetComponentsInChildren<Transform>(true).Where(t => t.name == targetName).ToArray();
                if (matches.Length != 1)
                    throw new InvalidOperationException($"Target '{targetName}' must exist exactly once under MachineRoot; found {matches.Length}.");
                var target = matches[0].gameObject;
                if (target.GetComponent<Collider>() == null || target.GetComponent<TechnicianComponent>() == null)
                    throw new InvalidOperationException($"Target '{targetName}' needs its existing collider and TechnicianComponent.");
                var interactable = target.GetComponent<XRSimpleInteractable>();
                if (interactable == null) interactable = target.AddComponent<XRSimpleInteractable>();
                var relay = target.GetComponent<XRSelectionRelay>();
                if (relay == null) relay = target.AddComponent<XRSelectionRelay>();
                var relaySettings = new SerializedObject(relay);
                relaySettings.FindProperty("adapter").objectReferenceValue = adapter;
                relaySettings.ApplyModifiedPropertiesWithoutUndo();
                while (interactable.selectEntered.GetPersistentEventCount() > 0)
                    UnityEventTools.RemovePersistentListener(interactable.selectEntered, 0);
                UnityEventTools.AddVoidPersistentListener(interactable.selectEntered, new UnityAction(relay.Select));
            }

            questRig.SetActive(false);
            if (desktopInput != null) desktopInput.enabled = true;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.rithikakambala.techassistxr");

            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (xr == null || xr.Manager == null || xr.Manager.activeLoaders.Count == 0)
                throw new InvalidOperationException("Android OpenXR loader is not configured.");
            xr.Manager.automaticLoading = true;
            xr.Manager.automaticRunning = true;
            EditorUtility.SetDirty(xr.Manager);
            AssetDatabase.SaveAssets();

            Debug.Log("TechAssist XR Quest demo configured: XRI rig, XR UI, required targets, build scene, ARM64 and IL2CPP.");
        }
    }
}
#endif
