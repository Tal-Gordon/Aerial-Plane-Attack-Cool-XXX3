using UnityEngine;
using SharpNeat.Phenomes;
using SharpNeat.Genomes.Neat;

public class NeatBrain : IEvolvableBrain
{
    private readonly IBlackBox blackBox;
    private readonly NeatGenome genome;

    // NEAT_DEBUG_LOG: tracking variables
    public bool IsDebugTarget { get; set; }
    public uint GenomeId { get; set; }
    private int tickCount = 0;

    public NeatBrain(NeatGenome genome, IBlackBox blackBox)
    {
        this.genome = genome;
        this.blackBox = blackBox;
    }

    /// <summary>
    /// Exposes the genome for the engine to save/load.
    /// </summary>
    public NeatGenome Genome => genome;

    public void Copy(IEvolvableBrain brain)
    {
        // NEAT brains are immutable phenomes — Copy doesn't apply.
        // The engine manages reproduction at the genome level.
        throw new System.NotSupportedException("NeatBrain does not support Copy. Use the engine's evolution methods.");
    }

    public float[] GetControlOutputs(float[] inputs)
    {
        // NEAT_DEBUG_LOG: increment tick count
        tickCount++;

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

        // NEAT_DEBUG_LOG: Log genome IO
        NeatLogger.LogGenomeIO(GenomeId, IsDebugTarget, tickCount, inputs, outputs);

        return outputs;
    }
    
    public int[] GetShape()
    {
        // We return just the input and output counts to satisfy the interface.
        return new int[] { blackBox.InputSignalArray.Length, blackBox.OutputSignalArray.Length };
    }

    // TODO: I did not want to deal with this, mutate lives on the engine, and I can't be bothered to fix this liskov subtitution violation
    public void Mutate(float rate)
    {
    }
}
