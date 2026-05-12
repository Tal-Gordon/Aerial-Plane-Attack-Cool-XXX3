using UnityEngine;
using SharpNeat.Phenomes;
using SharpNeat.Genomes.Neat;

public class NeatBrain : IEvolvableBrain
{
    private readonly IBlackBox blackBox;
    private readonly NeatGenome genome;

    public NeatBrain(NeatGenome genome, IBlackBox blackBox)
    {
        this.genome = genome;
        this.blackBox = blackBox;
    }

    public NeatGenome Genome => genome;

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

        float[] outputs = new float[blackBox.OutputSignalArray.Length];
        for (int i = 0; i < outputs.Length; i++)
        {
            // SharpNEAT's sigmoid outputs (0,1) — remap to (-1,1) for flight controls
            outputs[i] = (float)(blackBox.OutputSignalArray[i] * 2.0 - 1.0);
        }

        return outputs;
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
