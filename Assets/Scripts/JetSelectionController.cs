using UnityEngine;

/// Bridges the raw click-to-select input (SelectionInputManager, which only knows
/// about Transforms) to the UI's agent selection (UIManager.SelectAgent, which the
/// BrainVisualizerWidget and other per-agent widgets read from the snapshot).
///
/// Without this, clicking a jet moves the camera and fades the others, but nothing
/// ever tells the UI which JetAgent was picked — so SelectedAgent stays null and the
/// brain visualizer can never show anything.
///
/// Setup: drop this on any always-active scene object (e.g. the same GameObject as
/// the SelectionInputManager). The jets' selectable colliders must be on the layer
/// SelectionInputManager raycasts (selectableLayer), and JetAgent can live on the
/// collider object or any parent of it.
public class JetSelectionController : MonoBehaviour
{
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
