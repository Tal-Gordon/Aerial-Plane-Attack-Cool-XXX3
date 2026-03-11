using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(JetPhysics))]
public class PlayerController : MonoBehaviour
{
    private JetPhysics physics;

    [Header("Controls")]
    public InputAction flightControls;
    public InputAction rudderControls;

    private Vector2 flightInput;
    private float yawInput;

    private void Awake()
    {
        // Link the required JetPhysics component
        physics = GetComponent<JetPhysics>();
    }

    private void OnEnable()
    {
        flightControls.Enable();
        rudderControls.Enable();
    }

    private void OnDisable()
    {
        flightControls.Disable();
        rudderControls.Disable();
    }

    public void Update()
    {
        // Read Inputs (Happens every visual frame)
        flightInput = flightControls.ReadValue<Vector2>();
        yawInput = rudderControls.ReadValue<float>();
    }

    public void FixedUpdate()
    {
        // Pass the inputs to the physics script. 
        // Our original script inverted the roll (-flightInput.x) and didn't use 
        // a throttle input, so we pass '1f' to simulate constant max throttle.
        physics.ApplyControlInputs(flightInput.y, -flightInput.x, yawInput, 1f);
    }
}