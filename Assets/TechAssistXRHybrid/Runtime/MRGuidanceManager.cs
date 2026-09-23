using System;
using System.Collections.Generic;
using UnityEngine;

namespace TechAssistXR.Hybrid
{
    public sealed class MRGuidanceManager : MonoBehaviour
    {
        [SerializeField] private Transform machineRoot;
        [SerializeField] private TechnicianModeController mode;
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private GameObject markerPrefab;
        [SerializeField, Min(0)] private float markerOffset = 0.15f;
        private readonly Dictionary<string, Transform> targets = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly HashSet<string> duplicates = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
        private Transform target;
        private Renderer[] targetRenderers;
        private GameObject marker;
        private bool indexed;
        private void Awake() => RebuildIndex();
        public void RebuildIndex()
        {
            targets.Clear(); duplicates.Clear(); indexed = true;
            if (machineRoot == null) return;
            foreach (Transform node in machineRoot.GetComponentsInChildren<Transform>(true)) {
                if (targets.ContainsKey(node.name)) duplicates.Add(node.name);
                else targets.Add(node.name, node);
            }
        }
        public bool TryGetTarget(string id, out Transform result)
        {
            result = null;
            if (!indexed) RebuildIndex();
            return !string.IsNullOrEmpty(id) && !duplicates.Contains(id) && targets.TryGetValue(id, out result)
                && result != null && result.gameObject.activeInHierarchy;
        }
        public bool HighlightTargetComponent(string targetComponentID)
        {
            ClearHighlight();
            if (!TryGetTarget(targetComponentID, out target)) {
                Debug.LogWarning("Missing, inactive or duplicate machine node: " + targetComponentID, this); return false;
            }
            targetRenderers = target.GetComponentsInChildren<Renderer>(false);
            foreach (var renderer in targetRenderers) {
                if (renderer == null || highlightMaterial == null) continue;
                var materials = renderer.sharedMaterials;
                originals.Add(renderer, materials);
                var highlighted = new Material[materials.Length];
                for (int i = 0; i < highlighted.Length; i++) highlighted[i] = highlightMaterial;
                renderer.sharedMaterials = highlighted;
            }
            if (markerPrefab != null) {
                // Keep outside machine hierarchy so its bounds cannot shift its own placement.
                marker = Instantiate(markerPrefab);
                foreach (var collider in marker.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            }
            UpdateMarker();
            return true;
        }
        private void LateUpdate() => UpdateMarker();
        private void UpdateMarker()
        {
            if (target == null || !target.gameObject.activeInHierarchy) { if (marker != null) marker.SetActive(false); return; }
            if (marker == null) return;
            marker.SetActive(true);
            var bounds = new Bounds(target.position, Vector3.zero);
            bool hasBounds = false;
            if (targetRenderers != null) foreach (var renderer in targetRenderers) {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; } else bounds.Encapsulate(renderer.bounds);
            }
            marker.transform.position = new Vector3(bounds.center.x, bounds.max.y + markerOffset, bounds.center.z);
            var camera = mode != null ? mode.ActiveCamera : null;
            if (camera != null) {
                Vector3 forward = camera.transform.position - marker.transform.position;
                if (forward.sqrMagnitude > 0.0001f) marker.transform.rotation = Quaternion.LookRotation(forward, camera.transform.up);
            }
        }
        public void ClearHighlight()
        {
            foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            originals.Clear();
            if (marker != null) { marker.SetActive(false); Destroy(marker); }
            marker = null; target = null; targetRenderers = null;
        }
        private void OnDisable() => ClearHighlight();
    }
}
