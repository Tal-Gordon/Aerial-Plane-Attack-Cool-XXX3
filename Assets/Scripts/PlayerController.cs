using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(JetPhysics))]
public class PlayerController : MonoBehaviour
{
    private JetPhysics physics;
    private WeaponSystem weapons; // Optional

    [Header("Throttle Control")]
    [Range(0f, 1f)]
    [Tooltip("1 = Full Afterburner, 0 = Engine Idle")]
    public float thrustInput = 1f;

    [Header("Flight Controls")]
    public InputAction flightControls; // Vector2 (W/S Pitch, A/D Roll)
    public InputAction rudderControls; // Float (Q/E Yaw)

    [Header("Weapon Controls")]
    public InputAction fireAction;
    public InputAction switchWeaponAction;

    // Internal state
    private Vector2 flightInput;
    private float yawInput;

    private void Awake()
    {
        physics = GetComponent<JetPhysics>();
        TryGetComponent(out weapons); // Will be null if no weapons are attached, which is fine
    }

    private void OnEnable()
    {
        // Directly enable the actions
        flightControls.Enable();
        rudderControls.Enable();
        fireAction.Enable();
        switchWeaponAction.Enable();
    }

    private void OnDisable()
    {
        flightControls.Disable();
        rudderControls.Disable();
        fireAction.Disable();
        switchWeaponAction.Disable();
    }

    public void Update()
    {
        // Read Inputs (Happens every visual frame)
        flightInput = flightControls.ReadValue<Vector2>();
        yawInput = rudderControls.ReadValue<float>();

        // Handle Weapons
        if (weapons != null)
        {
            if (switchWeaponAction.WasPressedThisFrame())
            {
                weapons.SwitchWeapon();
            }

            if (fireAction.IsPressed())
            {
                weapons.Fire();
            }
        }
    }

    public void FixedUpdate()
    {
        // Pass inputs to the physics engine
        // X and Y are passed cleanly. Let JetPhysics handle any necessary inversions.
        physics.ApplyControlInputs(flightInput.y, flightInput.x, yawInput, thrustInput);
    }
}