using UnityEngine;

public interface IBrain
{
    // TODO optimization: Add a "ref float[] output" to avoid memory allocation
    public float[] GetControlOutputs(float[] inputs);

    // TODO Opus Note #1: Remove Copy() from IBrain — it's evolution-specific.
    // Move it to IEvolvableBrain. RLBrain has no meaningful Copy().
    public void Copy(IBrain brain);
}
