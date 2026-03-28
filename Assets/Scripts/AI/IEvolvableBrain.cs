using UnityEngine;

public interface IEvolvableBrain : IBrain
{
    public void Mutate(float rate);

    public float[] ExtractWeights();

    public void InjectWeights(float[] savedWeights);
}
