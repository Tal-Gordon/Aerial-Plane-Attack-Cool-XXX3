using UnityEngine;

public enum SensorType
{
    BasicFlight,
    Waypoint,
}

public interface ISensor
{
    SensorType SensorType { get; }

    float[] GetObservationData();

    int GetSensorCount();
}
