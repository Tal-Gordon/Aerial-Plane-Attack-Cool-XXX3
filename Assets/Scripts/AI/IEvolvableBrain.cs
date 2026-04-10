using UnityEngine;

public interface IEvolvableBrain : IBrain
{
    // TODO Opus Note #1: Add Copy(IBrain source) here when it's removed from IBrain.
    // Cloning genomes is intrinsically evolution work.
    public void Mutate(float rate);

    public float[] ExtractWeights();

    public void InjectWeights(float[] savedWeights);

    public int[] GetShape();
}
