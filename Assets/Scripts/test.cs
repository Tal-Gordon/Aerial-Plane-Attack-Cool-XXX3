using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class test : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Physics Forces")]
    public float thrustPower = 1000f; // Renamed from forwardThrust to match UML
    public float pitchTorque = 3f;
    public float rollTorque = 8f;
    public float yawTorque = 1f;

    [Header("Aerodynamics")]
    public float liftPower = 1f;
    public float lateralDrag = 1f;

    // Internal variables to hold control states
    private float currentPitch;
    private float currentRoll;
    private float currentYaw;
    private float currentThrottle;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.linearDamping = 2f;
        rb.angularDamping = 2f;
    }

    // Stores inputs to be applied on the next physics step
    public void ApplyControlInputs(float pitch, float roll, float yaw, float throttle)
    {
        currentPitch = pitch;
        currentRoll = roll;
        currentYaw = yaw;
        currentThrottle = throttle;
    }

    private void FixedUpdate()
    {
        // Apply Thrust
        rb.AddRelativeForce(Vector3.forward * thrustPower * currentThrottle);

        // Calculate and Apply Steering Torques
        float pitchForce = currentPitch * pitchTorque;
        float rollForce = currentRoll * rollTorque;
        float yawForce = currentYaw * yawTorque;

        rb.AddRelativeTorque(pitchForce, yawForce, rollForce);

        // Apply aerodynamics
        CalculateLiftAndDrag();
    }

    private void CalculateLiftAndDrag()
    {
        // LIFT
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        if (forwardSpeed > 0)
        {
            float lift = forwardSpeed * liftPower;
            rb.AddRelativeForce(Vector3.up * lift);
        }

        // LATERAL DRAG (Weather Vane Effect)
        float sidewaysSpeed = Vector3.Dot(rb.linearVelocity, transform.right);
        rb.AddForce(lateralDrag * sidewaysSpeed * -transform.right);
    }
}