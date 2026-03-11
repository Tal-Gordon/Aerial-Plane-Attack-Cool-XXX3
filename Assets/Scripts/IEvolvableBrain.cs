using UnityEngine;

public interface IEvolvableBrain : IBrain
{
    public void Mutate(float rate);

    public IEvolvableBrain Copy();

    public float[] ExtractWeights();

    public void InjectWeights(float[] savedWeights);
}
