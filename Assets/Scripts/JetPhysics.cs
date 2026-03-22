using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class JetPhysics : MonoBehaviour
{
    // --- DEPENDENCIES ---
    private Rigidbody rb;

    // --- CONFIGURATION ---
    [Header("Atmosphere Settings")]
    [Tooltip("Sea-level air density in kg/m^3.")]
    public float seaLevelDensity = 1.225f;
    [Tooltip("The altitude interval where atmospheric pressure decreases by a factor of e.")]
    public float scaleHeight = 8500f;

    [Header("F-35 Specifications")]
    [Tooltip("Reference wing area in square meters (S).")]
    public float wingArea = 42.7f;
    [Tooltip("Maximum engine thrust in Newtons.")]
    public float maxThrust = 191000f; 
    [Tooltip("Torque multipliers for pitch (X), yaw (Y), and roll (Z).")]
    public Vector3 controlPower = new(15f, 5f, 20f);
    [Tooltip("Caps the dynamic pressure for steering, simulating Fly-By-Wire and hydraulic limits.")]
    public float maxControlPressure = 5000f;

    [Header("Aerodynamic Stability & Damping")]
    [Tooltip("How strongly the tail forces the nose back into the wind (Pitch, Yaw, Roll).")]
    public Vector3 aerodynamicStability = new Vector3(0.05f, 0.05f, 0.01f);

    [Tooltip("How strongly the air resists the jet spinning (Pitch, Yaw, Roll).")]
    public Vector3 rotationalDamping = new Vector3(1.5f, 1.5f, 0.5f);

    [Header("Stall & Turbulence Dynamics")]
    [Tooltip("The Angle of Attack where smooth airflow detaches (Stall).")]
    public float criticalStallAngle = 20f;
    [Tooltip("How violently the jet shakes and tumbles when stalled.")]
    public float stallBuffetMultiplier = 250000f;

    [Header("Aerodynamic Profiles")]
    public AnimationCurve liftCurve;
    public AnimationCurve dragCurve;

    // --- AGENT STATE ---
    // Underscores denote private class-level state variables
    private float pitchInput;
    private float rollInput;
    private float yawInput;
    private float throttleInput;

    // --- INITIALIZATION ---
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // TODO delete
        rb.linearVelocity = transform.forward * 500f;

        // Disable Unity's fake air friction
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        // Force the center of mass to be exactly at the transform's origin
        rb.centerOfMass = Vector3.zero;

        // OVERRIDE: Hardcode the rotational inertia of a fighter jet
        // This stops Unity from relying on your colliders to calculate spin resistance
        rb.inertiaTensor = new Vector3(80000f, 100000f, 25000f);
        rb.inertiaTensorRotation = Quaternion.identity;
    }

    // --- PUBLIC INTERFACE ---
    public void ApplyControlInputs(float pitch, float roll, float yaw, float throttle)
    {
        pitchInput = pitch;
        rollInput = roll;
        yawInput = yaw;
        throttleInput = throttle;
    }

    // --- PHYSICS PIPELINE ---
    private void FixedUpdate()
    {
        // Core State
        Vector3 worldVelocity = rb.linearVelocity;
        Vector3 localVelocity = transform.InverseTransformDirection(worldVelocity);
        float sqrSpeed = localVelocity.sqrMagnitude;

        // Propulsion (Applied regardless of airspeed)
        ApplyThrust();

        // Aerodynamics (Requires a minimum airspeed to avoid division by zero or NaN errors)
        if (sqrSpeed > 0.1f)
        {
            float dynamicPressure = CalculateDynamicPressure(sqrSpeed);
            float angleOfAttack = CalculateAngleOfAttack(localVelocity);
            float sideslipAngle = Mathf.Atan2(localVelocity.x, localVelocity.z) * Mathf.Rad2Deg;

            ApplyAerodynamicForces(worldVelocity, dynamicPressure, angleOfAttack);
            ApplyControlSurfaces(dynamicPressure);
            ApplyAerodynamicStability(dynamicPressure, angleOfAttack, sideslipAngle);
        }
    }

    // --- WORKER METHODS ---
    private void ApplyThrust()
    {
        float currentThrust = throttleInput * maxThrust;
        rb.AddForce(transform.forward * currentThrust, ForceMode.Force);
    }

    private void ApplyAerodynamicForces(Vector3 worldVelocity, float dynamicPressure, float aoa)
    {
        // Resolve coefficients based on current angle of attack
        float liftCoefficient = liftCurve.Evaluate(aoa);
        float dragCoefficient = dragCurve.Evaluate(aoa);

        // Calculate force magnitudes (Force = q * S * Coefficient)
        float liftForce = dynamicPressure * wingArea * liftCoefficient;
        float dragForce = dynamicPressure * wingArea * dragCoefficient;

        // Resolve global directions
        Vector3 dragDirection = -worldVelocity.normalized;
        // Cross product ensures lift is always perfectly perpendicular to both wind and the physical wings
        Vector3 liftDirection = Vector3.Cross(worldVelocity, transform.right).normalized;

        rb.AddForce(liftDirection * liftForce, ForceMode.Force);
        rb.AddForce(dragDirection * dragForce, ForceMode.Force);
    }

    private void ApplyControlSurfaces(float dynamicPressure)
    {
        // Fly-By-Wire limit: Stop V^2 from infinitely scaling our torque
        float fbwPressure = Mathf.Min(dynamicPressure, maxControlPressure);

        Vector3 torque = new Vector3(
            pitchInput * controlPower.x,
            yawInput * controlPower.y,
            -rollInput * controlPower.z
        ) * fbwPressure;

        rb.AddRelativeTorque(torque, ForceMode.Force);
    }

    private void ApplyAerodynamicStability(float dynamicPressure, float aoa, float sideslip)
    {
        // ROTATIONAL DAMPING (The Shock Absorbers)
        // Damping SHOULD scale with raw dynamic pressure to stop high-speed death spins.
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        Vector3 dampingTorque = new Vector3(
            -localAngularVelocity.x * rotationalDamping.x,
            -localAngularVelocity.y * rotationalDamping.y,
            -localAngularVelocity.z * rotationalDamping.z
        ) * dynamicPressure;

        // THE WEATHERVANE EFFECT (The Restoring Force)
        // CRITICAL FIX: We clamp the pressure so the tail doesn't snap the jet in half at Mach 1.
        // We can reuse the maxControlPressure variable we created earlier.
        float stabilityPressure = Mathf.Min(dynamicPressure, maxControlPressure);

        float pitchRestoringForce = -aoa * aerodynamicStability.x;
        float yawRestoringForce = sideslip * aerodynamicStability.y;

        float rollRestoringForce = -transform.localEulerAngles.z;
        if (rollRestoringForce < -180f) rollRestoringForce += 360f;
        rollRestoringForce *= aerodynamicStability.z;

        Vector3 stabilityTorque = new Vector3(pitchRestoringForce, yawRestoringForce, rollRestoringForce) * stabilityPressure;

        // Apply both forces
        rb.AddRelativeTorque(dampingTorque + stabilityTorque, ForceMode.Force);
    }

    // --- MATH UTILITIES ---
    private float CalculateDynamicPressure(float sqrSpeed)
    {
        // Prevent negative altitude logic if the jet dips below the terrain floor
        float currentAltitude = Mathf.Max(0f, rb.position.y);
        
        // Barometric formula for atmospheric density decay
        float currentAirDensity = seaLevelDensity * Mathf.Exp(-currentAltitude / scaleHeight);

        // q = 1/2 * rho * v^2
        return 0.5f * currentAirDensity * sqrSpeed;
    }

    private float CalculateAngleOfAttack(Vector3 localVelocity)
    {
        // We negate the Y velocity so that an upward-pitched nose results in a positive Angle of Attack.
        return Mathf.Atan2(-localVelocity.y, localVelocity.z) * Mathf.Rad2Deg;
    }
}