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
    [Tooltip("Maximum cursor distance, in screen pixels, for selecting a jet.")]
    [SerializeField] private float selectionRadius = 40f;

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
        Camera camera = Camera.main;
        var population = manager != null ? manager.Snapshot?.Population : null;
        if (camera == null || population == null) return;

        JetAgent nearest = null;
        float nearestSqrDistance = selectionRadius * selectionRadius;

        // This O(population) scan runs only on a click—not every frame—and avoids
        // adding thousands of moving collider shapes to large training runs.
        foreach (JetAgent agent in population)
        {
            if (agent == null || !agent.gameObject.activeInHierarchy) continue;

            Vector3 screenPoint = camera.WorldToScreenPoint(agent.transform.position);
            if (screenPoint.z <= 0f) continue;

            float sqrDistance = ((Vector2)screenPoint - pointerPosition).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance) continue;

            nearestSqrDistance = sqrDistance;
            nearest = agent;
        }

        if (nearest != null)
            manager.SelectAgent(nearest);
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
