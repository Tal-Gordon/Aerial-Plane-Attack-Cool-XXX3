using UnityEngine;

public interface IBrain
{
    public float[] GetControlOutputs(float[] inputs);

    public IBrain Copy();
}
