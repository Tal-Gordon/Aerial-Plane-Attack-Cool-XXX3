using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(JetPhysics))]
public class PlayerController : MonoBehaviour
{
    private JetPhysics physics;

    [Header("Controls")]
    public InputAction flightControls;
    public InputAction rudderControls;

    [Header("Weapon Controls")]
    public InputAction fireAction;
    public InputAction switchWeaponAction;

    private Vector2 flightInput;
    private float yawInput;

    // Reference to the weapon system (can be null)
    private WeaponSystem weapons;

    private void Awake()
    {
        physics = GetComponent<JetPhysics>();
        TryGetComponent(out weapons);
    }

    private void OnEnable()
    {
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

        if (weapons != null)
        {
            // Switch weapon on single button press
            if (switchWeaponAction.WasPressedThisFrame())
            {
                weapons.SwitchWeapon();
            }

            // Fire weapons (continuous press for machine guns, single fire for missiles handled in WeaponSystem)
            if (fireAction.IsPressed())
            {
                weapons.Fire();
            }
        }
    }

    public void FixedUpdate()
    {
        // Pass the inputs to the physics script. 
        // Our original script inverted the roll (-flightInput.x) and didn't use 
        // a throttle input, so we pass '1f' to simulate constant max throttle.
        physics.ApplyControlInputs(flightInput.y, -flightInput.x, yawInput, 1f);
    }
}