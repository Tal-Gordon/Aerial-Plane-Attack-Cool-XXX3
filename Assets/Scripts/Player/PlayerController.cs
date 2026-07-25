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
    [Tooltip("Normalized throttle change per second. 1 moves from idle to full in one second.")]
    [SerializeField] private float throttleChangeRate = 1f;

    [Header("Flight Controls")]
    public InputAction flightControls; // Vector2 (W/S Pitch, A/D Roll)
    public InputAction rudderControls; // Float (Q/E Yaw)

    [Header("Weapon Controls")]
    public InputAction fireAction;
    public InputAction switchWeaponAction;

    // Internal state
    private Vector2 flightInput;
    private float yawInput;
    private bool weaponsEnabled = true;

    public float ThrustInput => thrustInput;

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

        // Challenge/race throttle: hold Up/Down to sweep from idle to full in
        // approximately one second. Use scaled delta time so pausing freezes it.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float throttleDirection = 0f;
            if (keyboard.upArrowKey.isPressed) throttleDirection += 1f;
            if (keyboard.downArrowKey.isPressed) throttleDirection -= 1f;
            thrustInput = Mathf.Clamp01(
                thrustInput + throttleDirection * throttleChangeRate * Time.deltaTime);
        }

        // Handle Weapons
        if (weaponsEnabled && weapons != null)
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

    /// <summary>Prepares the prefab's existing human controls for a race session.</summary>
    public void ConfigureForChallenge(float startingThrottle = 1f)
    {
        thrustInput = Mathf.Clamp01(startingThrottle);
        weaponsEnabled = false;
        if (weapons != null) weapons.enabled = false;
    }
}
