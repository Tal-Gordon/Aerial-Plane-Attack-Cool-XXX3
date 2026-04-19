using UnityEngine;

public interface IBrain
{
    // TODO optimization: Add a "ref float[] output" to avoid memory allocation
    public float[] GetControlOutputs(float[] inputs);
}
