using UnityEngine;

public interface IBrain
{
    // TODO optimization: Add a "ref float[] output" to avoid memory allocation
    public float[] GetControlOutputs(float[] inputs);

    public float[] Serialize();
    
    public void Deserialize(float[] savedData);
}
