using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))] // Automatically adds a Rigidbody to our plane. Not truly necessary, but good practice
public class PlaneController : MonoBehaviour
{
    // TODO public to private
    [Header("Physics Forces")]
    // These numbers must be high to move a heavy Rigidbody (I set mass to 10)
    public float forwardThrust = 1000f;
    public float pitchTorque = 1000f;
    public float rollTorque = 500f;
    public float yawTorque = 750f;

    [Header("Aerodynamics")]
    // How much upward push we get per unit of forward speed
    public float liftPower = 1f;
    // How hard the air pushes back when we slide sideways
    public float lateralDrag = 1f;

    [Header("Controls")]
    // These create handy drop-downs in the Inspector to assign our keys
    public InputAction flightControls; // For Pitch (Up/Down) and Roll (Left/Right)
    public InputAction rudderControls; // For Yaw (Turning left/right)

    // TODO look at this shit again
    private Rigidbody rb;
    // Variables to hold our input between Update and FixedUpdate
    private Vector2 flightInput;
    private float yawInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.linearDamping = 2f;  // Adds air resistance so we don't accelerate infinitely
        rb.angularDamping = 2f; // Adds rotational resistance so we don't spin out of control
    }

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
        // Read Inputs (Happens every visual frame)
        // flightControls will read a Vector2 (X and Y axis) from WASD (or a thumbstick, but we don't use that)
        flightInput = flightControls.ReadValue<Vector2>();
        // rudderControls will read a single float (1D axis) from Q/E (or, again, triggers which we won't use)
        yawInput = rudderControls.ReadValue<float>();
    }

    void FixedUpdate()
    {
        // Apply Physics (Happens on a fixed physics timer)

        // Thrust: Push the plane forward along its local Z axis
        rb.AddRelativeForce(Vector3.forward * forwardThrust);


        // Steering Torques: Calculate how hard we are twisting
        float pitch = flightInput.y * pitchTorque;
        float roll = -flightInput.x * rollTorque; // Inverted so 'Right' rolls right
        float yaw = yawInput * yawTorque;

        // Apply twisting force along local axes (X = Pitch, Y = Yaw, Z = Roll)
        rb.AddRelativeTorque(pitch, yaw, roll);

        // TODO can we simplify?
        // Lift: Find out how fast we are moving strictly in the direction the nose is pointing
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        // If we are moving forward, calculate an upward force
        if (forwardSpeed > 0)
        {
            float lift = forwardSpeed * liftPower;
            // Push the plane "Up" relative to its own roof
            rb.AddRelativeForce(Vector3.up * lift);
        }

        // Lateral Drag (The Weather Vane Effect): Find out how fast we are sliding left or right
        float sidewaysSpeed = Vector3.Dot(rb.linearVelocity, transform.right);

        // Push in the exact opposite direction to kill the slide
        // We use AddForce (world space) instead of AddRelativeForce because we are already multiplying by the local 'right' vector
        rb.AddForce(lateralDrag * sidewaysSpeed * -transform.right);
    }
}