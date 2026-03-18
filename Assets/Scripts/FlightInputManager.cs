using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class FlightInputManager : MonoBehaviour
{
    // Global events anyone can listen to
    public static event Action OnNextCommand;
    public static event Action OnPreviousCommand;

    [Header("Input Actions")]
    [Tooltip("Action for moving forward")]
    public InputAction nextWaypointAction;

    [Tooltip("Action for moving backward")]
    public InputAction previousWaypointAction;

    private void OnEnable()
    {
        nextWaypointAction.Enable();
        previousWaypointAction.Enable();

        nextWaypointAction.performed += _ => OnNextCommand?.Invoke();
        previousWaypointAction.performed += _ => OnPreviousCommand?.Invoke();
    }

    private void OnDisable()
    {
        nextWaypointAction.Disable();
        previousWaypointAction.Disable();

        nextWaypointAction.performed -= _ => OnNextCommand?.Invoke();
        previousWaypointAction.performed -= _ => OnPreviousCommand?.Invoke();
    }
}