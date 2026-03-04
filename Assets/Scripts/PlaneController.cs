using UnityEngine;
using UnityEngine.InputSystem; // We need this namespace now!

public class PlaneController : MonoBehaviour
{
    [Header("Flight Speeds")]
    public float forwardSpeed = 25f;
    public float pitchSpeed = 60f;
    public float rollSpeed = 60f;
    public float yawSpeed = 30f;

    [Header("Controls")]
    // These create handy drop-downs in the Inspector to assign your keys
    public InputAction flightControls; // For Pitch (Up/Down) and Roll (Left/Right)
    public InputAction rudderControls; // For Yaw (Turning left/right)

    private void OnEnable()
    {
        // Actions must be enabled before they can read input
        flightControls.Enable();
        rudderControls.Enable();
    }

    private void OnDisable()
    {
        flightControls.Disable();
        rudderControls.Disable();
    }

    void Update()
    {
        // Constant Forward Movement
        transform.Translate(Vector3.forward * forwardSpeed * Time.deltaTime);

        // Read the new input values
        // flightControls will read a Vector2 (X and Y axis) from WASD or a thumbstick
        Vector2 flightInput = flightControls.ReadValue<Vector2>();

        // rudderControls will read a single float (1D axis) from Q/E or triggers
        float yawInput = rudderControls.ReadValue<float>();

        // Calculate Rotations
        float pitch = flightInput.y * pitchSpeed * Time.deltaTime;
        float roll = -flightInput.x * rollSpeed * Time.deltaTime; // Inverted so 'Right' rolls right
        float yaw = yawInput * yawSpeed * Time.deltaTime;

        // Apply the Rotation
        transform.Rotate(pitch, yaw, roll);
    }
}