using UnityEngine;

public class BasicFlightSensors : MonoBehaviour, ISensor
{
    private Rigidbody rb;
    private float[] cachedObs;
    
    // These max values are used to normalize the sensor data.
    // They should be set based on the expected maximums for the plane's performance.
    // Needs to be tuned based on the actual plane and environment settings.
    private float maxSpeed = 1000f;
    private float maxPitchRate = 3f;
    private float maxYawRate = 1f;
    private float maxRollRate = 8f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cachedObs = new float[12];
    }

    public virtual float[] GetObservationData()
    {
        float[] obs = cachedObs;

        // Local Velocity
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity) / maxSpeed;

        obs[0] = localVelocity.x;
        obs[1] = localVelocity.y;
        obs[2] = localVelocity.z;

        // Angular velocity
        Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);

        obs[3] = localAngularVel.x / maxPitchRate;
        obs[4] = localAngularVel.y / maxYawRate;
        obs[5] = localAngularVel.z / maxRollRate;

        // Nose direction
        obs[6] = transform.forward.x;
        obs[7] = transform.forward.y;
        obs[8] = transform.forward.z;

        obs[9] = transform.up.x;
        obs[10] = transform.up.y;
        obs[11] = transform.up.z;

        return obs;
    }

    public virtual int GetSensorCount()
    {
        return 12;
    }
}
