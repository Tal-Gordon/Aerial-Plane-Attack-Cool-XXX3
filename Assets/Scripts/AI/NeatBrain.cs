using UnityEngine;
using SharpNeat.Phenomes;
using SharpNeat.Genomes.Neat;

public class NeatBrain : IEvolvableBrain
{
    private readonly IBlackBox blackBox;
    private readonly NeatGenome genome;
    private readonly float[] cachedOutputs;

    public NeatBrain(NeatGenome genome, IBlackBox blackBox)
    {
        this.genome = genome;
        this.blackBox = blackBox;
        cachedOutputs = new float[blackBox.OutputSignalArray.Length];
    }

    public NeatGenome Genome => genome;

    // The control outputs produced by the most recent GetControlOutputs call,
    // already remapped to (-1,1). Exposed read-only for the brain visualizer so it
    // can colour the output nodes WITHOUT re-activating the black box — a second
    // Activate() would advance the recurrent network's state and perturb the jet
    // it's visualizing. Zero-filled until the agent makes its first decision.
    public float[] LastOutputs => cachedOutputs;

    public void Copy(IEvolvableBrain brain)
    {
        throw new System.NotSupportedException("NeatBrain does not support Copy. Use the engine's evolution methods.");
    }

    public float[] GetControlOutputs(float[] inputs)
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            blackBox.InputSignalArray[i] = inputs[i];
        }

        blackBox.Activate();

        for (int i = 0; i < cachedOutputs.Length; i++)
        {
            // SharpNEAT's sigmoid outputs (0,1) — remap to (-1,1) for flight controls
            cachedOutputs[i] = (float)(blackBox.OutputSignalArray[i] * 2.0 - 1.0);
        }

        return cachedOutputs;
    }

    public int[] GetShape()
    {
        return new int[] { blackBox.InputSignalArray.Length, blackBox.OutputSignalArray.Length };
    }

    // TODO: Mutate lives on the engine — Liskov violation to address later
    public void Mutate(float rate)
    {
    }
}
