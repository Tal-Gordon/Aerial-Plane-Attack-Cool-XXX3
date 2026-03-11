using UnityEngine;

public interface ISensor
{
    public float[] GetObservationData();

    public int GetSensorCount();
}
