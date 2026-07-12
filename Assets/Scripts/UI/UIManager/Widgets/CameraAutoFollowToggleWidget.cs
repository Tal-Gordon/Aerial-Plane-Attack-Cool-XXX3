using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggle for Flight School's automatic section-camera progression. Like the
/// inference toggle, it writes a requested state and then continuously reflects
/// the controller's real state. Manual A/D camera movement disables auto-follow,
/// which is therefore shown by the toggle on its next Tick.
/// </summary>
public class CameraAutoFollowToggleWidget : UIWidget
{
    [SerializeField] private Toggle toggle;
    [Tooltip("Flight School camera controller. Found automatically when left empty.")]
    [SerializeField] private CameraControllerFlight cameraController;

    protected override void OnInitialize()
    {
        // This control belongs only to Flight School. Max Altitude uses its own
        // always-automatic camera, so hide the shared-prefab widget before looking
        // for or wiring a CameraControllerFlight.
        if (FindFirstObjectByType<FlightSchoolObjective>() == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraControllerFlight>();

        if (toggle == null)
        {
            Debug.LogWarning($"{nameof(CameraAutoFollowToggleWidget)} on {gameObject.name} is missing a Toggle reference!");
            return;
        }

        // Auto-follow starts off. Set both the real controller and visual state
        // without notifying the listener, matching InferenceToggleWidget's sync.
        cameraController?.SetAutoFollow(false);
        toggle.SetIsOnWithoutNotify(false);
        toggle.onValueChanged.AddListener(OnToggleChanged);

        if (cameraController == null)
            Debug.LogWarning($"{nameof(CameraAutoFollowToggleWidget)} could not find a {nameof(CameraControllerFlight)} in the scene.");
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        if (toggle == null) return;

        bool actualState = cameraController != null && cameraController.IsAutoFollowEnabled;
        toggle.SetIsOnWithoutNotify(actualState);
        toggle.interactable = cameraController != null;
    }

    private void OnToggleChanged(bool on)
    {
        cameraController?.SetAutoFollow(on);
    }

    private void OnDestroy()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }
}
