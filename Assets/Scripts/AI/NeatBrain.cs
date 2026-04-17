using UnityEngine;
using SharpNeat.Phenomes;

public class NeatBrain : IEvolvableBrain
{
    private readonly IBlackBox blackBox;

    public NeatBrain(IBlackBox blackBox)
    {
        this.blackBox = blackBox;
    }

    public void Copy(IEvolvableBrain brain)
    {
        throw new System.NotImplementedException();
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
            outputs[i] = (float)blackBox.OutputSignalArray[i];
        }

        return outputs;
    }
    
    public int[] GetShape()
    {
        // We return just the input and output counts to satisfy the interface.
        return new int[] { blackBox.InputSignalArray.Length, blackBox.OutputSignalArray.Length };
    }

    // I did not want to deal with this, mutate lives on the engine, and I can't be bothered to fix this liskov subtitution violation
    public void Mutate(float rate)
    {
    }

    public float[] Serialize()
    {
        throw new System.NotImplementedException();
    }

    public void Deserialize(float[] savedData)
    {
        throw new System.NotImplementedException();
    }
}
