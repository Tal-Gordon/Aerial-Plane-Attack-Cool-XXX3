using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CameraControllerFlightSchool))]
public class CameraInputHandler : MonoBehaviour
{
    [Header("Input Actions")]
    [Tooltip("Action for moving forward")]
    public InputAction nextWaypointAction;

    [Tooltip("Action for moving backward")]
    public InputAction previousWaypointAction;

    private CameraControllerFlightSchool cameraController;

    private void Awake()
    {
        cameraController = GetComponent<CameraControllerFlightSchool>();
    }

    private void OnEnable()
    {
        if (nextWaypointAction != null)
        {
            nextWaypointAction.Enable();
            nextWaypointAction.performed += OnNextPerformed;
        }

        if (previousWaypointAction != null)
        {
            previousWaypointAction.Enable();
            previousWaypointAction.performed += OnPreviousPerformed;
        }
    }

    private void OnDisable()
    {
        if (nextWaypointAction != null)
        {
            nextWaypointAction.performed -= OnNextPerformed;
            nextWaypointAction.Disable();
        }

        if (previousWaypointAction != null)
        {
            previousWaypointAction.performed -= OnPreviousPerformed;
            previousWaypointAction.Disable();
        }
    }

    private void OnNextPerformed(InputAction.CallbackContext context)
    {
        cameraController.GoToNextWaypoint();
    }

    private void OnPreviousPerformed(InputAction.CallbackContext context)
    {
        cameraController.GoToPreviousWaypoint();
    }
}