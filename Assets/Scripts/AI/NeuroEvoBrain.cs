using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Windows;
using System.Collections.Generic;
public class NeuroEvoBrain : IEvolvableBrain
{
    private float[][][] weights;

    public NeuroEvoBrain(int[] shape)
    {
        InitializeWeights(shape);
    }

    public NeuroEvoBrain(float[][][] weights)
    {
        this.weights = weights;
    }

    private void InitializeWeights(int[] shape)
    {
        weights = new float[shape.Length - 1][][];

        for (int i = 0; i < shape.Length - 1; i++)
        {
            weights[i] = new float[shape[i]][];

            for (int j = 0; j < shape[i]; j++)
            {
                weights[i][j] = new float[shape[i + 1]];

                for(int k = 0; k < shape[i + 1]; k++)
                {
                    weights[i][j][k] = UnityEngine.Random.Range(-1.0f, 1.0f); // Kfir was here!!!!
                }
            }
        }
    }

    public IEvolvableBrain Copy()
    {
        float[][][] weightsCopy = new float[weights.Length][][];

        for (int i = 0; i < weights.Length; i++)
        {
            weightsCopy[i] = new float[weights[i].Length][];

            for (int j = 0; j < weights[i].Length; j++)
            {
                weightsCopy[i][j] = new float[weights[i][j].Length];

                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    weightsCopy[i][j][k] = weights[i][j][k];
                }
            }
        }
        return new NeuroEvoBrain(weightsCopy);
    }

    public float[][][] GetWeights()
    {
        return weights;
    }

    public int[] GetShape()
    {
        int[] shape = new int[weights.Length + 1];
        
        for (int i = 0; i < weights.Length; i++)
        {
            shape[i] = weights[i].Length;
        }

        shape[weights.Length] = weights[weights.Length - 1][0].Length;

        return shape;
    }

    // TODO potentially add clamp to regularize the weights
    public void Mutate(float rate)
    {
        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    weights[i][j][k] += UnityEngine.Random.Range(-rate, rate);
                }
            }
        }
    }

    public float[] GetControlOutputs(float[] inputs)
    {
        for (int i = 0; i < weights.Length; i++)
        {
            float[] FCLayer = FullyConnected(inputs, weights[i]);

            // Use relu for each layer besides the last
            if (i == weights.Length - 1)
            {
                // Output Layer: Use Tanh to allow negative flight controls (-1.0 to 1.0)
                inputs = Tanh(FCLayer);
            }
            else
            {
                // Hidden Layers: Use ReLU for fast internal processing
                inputs = Relu(FCLayer);
            }
        }

        return inputs;
    }

    private float[] FullyConnected(float[] inputs, float[][] matrix)
    {
        float[] outputs = new float[matrix[0].Length];

        for (int i = 0; i < outputs.Length; i++)
        {
            for (int j = 0; j < inputs.Length; j++)
            {
                outputs[i] += inputs[j] * matrix[j][i];
            }
        }

        return outputs;
    }

    private float[] Relu(float[] inputs)
    {
        float[] outputs = new float[inputs.Length];

        for (int i = 0; i < outputs.Length; i++)
        {
            outputs[i] = System.Math.Max(0f, inputs[i]);
        }

        return outputs;
    }

    private float[] Tanh(float[] inputs)
    {
        float[] outputs = new float[inputs.Length];

        for (int i = 0; i < outputs.Length; i++)
        {
            outputs[i] = (float)System.Math.Tanh(inputs[i]);
        }

        return outputs;
    }

    public float[] ExtractWeights()
    {
        List<float> flatWeights = new();

        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    flatWeights.Add(weights[i][j][k]);
                }
            }
        }

        return flatWeights.ToArray();
    }

    public void InjectWeights(float[] savedWeights)
    {
        int index = 0;

        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    weights[i][j][k] = savedWeights[index];
                    index++;
                }
            }
        }
    }
}
