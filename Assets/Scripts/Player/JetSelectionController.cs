using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// Bridges the raw click-to-select input (SelectionInputManager, which only knows
/// about Transforms) to the UI's agent selection (UIManager.SelectAgent, which the
/// BrainVisualizerWidget and other per-agent widgets read from the snapshot).
///
/// Without this, clicking a jet moves the camera and fades the others, but nothing
/// ever tells the UI which JetAgent was picked — so SelectedAgent stays null and the
/// brain visualizer can never show anything.
///
/// UIManager installs this automatically. It keeps compatibility with the older
/// SelectionInputManager events, while collider-free training scenes select the
/// closest visible jet to the cursor in screen space.
public class JetSelectionController : MonoBehaviour
{
    [Tooltip("Minimum cursor distance, in screen pixels, for selecting a jet.")]
    [SerializeField] private float selectionRadius = 60f;
    [Tooltip("Extra pixels added around the jet's visible bounds, giving the screen-space fallback a forgiving collider-like target.")]
    [SerializeField] private float selectionPadding = 18f;

    // Renderer lookup is stable for a jet's lifetime. Cache it so a click across a
    // large population doesn't repeatedly traverse every F35 hierarchy.
    private readonly Dictionary<JetAgent, Renderer> selectionRenderers = new();
    private Camera selectionCamera;

    private void OnEnable()
    {
        SelectionInputManager.OnCubeSelected += HandleSelected;
        SelectionInputManager.OnCubeDeselected += HandleDeselected;
    }

    private void OnDisable()
    {
        SelectionInputManager.OnCubeSelected -= HandleSelected;
        SelectionInputManager.OnCubeDeselected -= HandleDeselected;
    }

    private void Update()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            HandleDeselected();
            return;
        }

        if (!Mouse.current.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        SelectNearestJet(Mouse.current.position.ReadValue());
    }

    private void SelectNearestJet(Vector2 pointerPosition)
    {
        UIManager manager = UIManager.Instance;
        Camera camera = ResolveSelectionCamera();
        var population = manager != null ? manager.Snapshot?.Population : null;
        if (camera == null || population == null) return;

        JetAgent nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;

        // This O(population) scan runs only on a click—not every frame—and avoids
        // adding thousands of moving collider shapes to large training runs.
        foreach (JetAgent agent in population)
        {
            if (agent == null || !agent.gameObject.activeInHierarchy) continue;

            Vector3 worldCenter = agent.transform.position;
            float hitRadius = selectionRadius;

            Renderer renderer = GetSelectionRenderer(agent);
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                worldCenter = bounds.center;

                // Project the render bounds' sphere onto the screen. This approximates
                // the old collider-sized hit area while retaining the collider-free
                // fallback needed by large training populations.
                Vector3 edgePoint = bounds.center + camera.transform.right * bounds.extents.magnitude;
                Vector3 centerScreen = camera.WorldToScreenPoint(bounds.center);
                Vector3 edgeScreen = camera.WorldToScreenPoint(edgePoint);
                if (centerScreen.z > 0f && edgeScreen.z > 0f)
                    hitRadius = Mathf.Max(hitRadius, Vector2.Distance(centerScreen, edgeScreen) + selectionPadding);
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(worldCenter);
            if (screenPoint.z <= 0f) continue;

            float sqrDistance = ((Vector2)screenPoint - pointerPosition).sqrMagnitude;
            if (sqrDistance > hitRadius * hitRadius) continue;
            if (sqrDistance >= nearestSqrDistance) continue;

            nearestSqrDistance = sqrDistance;
            nearest = agent;
        }

        if (nearest != null)
            manager.SelectAgent(nearest);
    }

    private Camera ResolveSelectionCamera()
    {
        if (selectionCamera != null && selectionCamera.isActiveAndEnabled)
            return selectionCamera;

        selectionCamera = Camera.main;
        if (selectionCamera != null) return selectionCamera;

        // A scene camera can render perfectly while lacking the MainCamera tag.
        // Prefer the flight controller's own Camera over an arbitrary UI/overlay
        // camera; Track 2 historically ships with exactly this misconfiguration.
        CameraControllerFlight flightCamera = FindFirstObjectByType<CameraControllerFlight>();
        if (flightCamera != null)
            selectionCamera = flightCamera.GetComponent<Camera>();

        return selectionCamera;
    }

    private Renderer GetSelectionRenderer(JetAgent agent)
    {
        if (selectionRenderers.TryGetValue(agent, out Renderer cached))
            return cached;

        Renderer renderer = agent.GetComponentInChildren<Renderer>();
        selectionRenderers[agent] = renderer;
        return renderer;
    }

    private void HandleSelected(Transform selected)
    {
        if (UIManager.Instance == null || selected == null) return;

        // The collider that was hit may be a child of the jet root, so search upward.
        JetAgent agent = selected.GetComponentInParent<JetAgent>();
        if (agent != null)
            UIManager.Instance.SelectAgent(agent);
    }

    private void HandleDeselected()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ClearSelection();
    }
}
