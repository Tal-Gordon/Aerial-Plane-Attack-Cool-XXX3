using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(JetPhysics))]
public class PlayerController : MonoBehaviour
{
    private JetPhysics _physics;
    private WeaponSystem _weapons; // Optional

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
    private Vector2 _flightInput;
    private float _yawInput;

    private void Awake()
    {
        _physics = GetComponent<JetPhysics>();
        TryGetComponent(out _weapons); // Will be null if no weapons are attached, which is fine
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
        // 1. Read Inputs (Happens every visual frame)
        _flightInput = flightControls.ReadValue<Vector2>();
        _yawInput = rudderControls.ReadValue<float>();

        // 2. Handle Weapons
        if (_weapons != null)
        {
            if (switchWeaponAction.WasPressedThisFrame())
            {
                _weapons.SwitchWeapon();
            }

            if (fireAction.IsPressed())
            {
                _weapons.Fire();
            }
        }
    }

    public void FixedUpdate()
    {
        // 3. Pass inputs to the physics engine
        // X and Y are passed cleanly. Let JetPhysics handle any necessary inversions.
        _physics.ApplyControlInputs(_flightInput.y, _flightInput.x, _yawInput, thrustInput);
    }
}