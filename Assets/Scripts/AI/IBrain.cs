using UnityEngine;

public interface IBrain
{
    public float[] GetControlOutputs(float[] inputs);

    public void Copy(IBrain brain);
}
