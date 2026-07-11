using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Windows;
using System.Collections.Generic;
public class NeuroEvoBrain : IEvolvableBrain
{
    private float[][][] weights;
    private float[][] biases;
    private float[][] layerBuffers;

    public NeuroEvoBrain(int[] shape)
    {
        InitializeWeights(shape);
        InitializeBuffers();
    }

    public NeuroEvoBrain(float[][][] weights, float[][] biases)
    {
        this.weights = weights;
        this.biases = biases;
        InitializeBuffers();
    }

    private void InitializeBuffers()
    {
        layerBuffers = new float[weights.Length][];
        for (int i = 0; i < weights.Length; i++)
            layerBuffers[i] = new float[biases[i].Length];
    }

    private void InitializeWeights(int[] shape)
    {
        weights = new float[shape.Length - 1][][];
        biases = new float[shape.Length - 1][];

        for (int i = 0; i < shape.Length - 1; i++)
        {
            weights[i] = new float[shape[i]][];
            biases[i] = new float[shape[i + 1]];
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

    public void Copy(IEvolvableBrain brain)
    {
        NeuroEvoBrain other = (NeuroEvoBrain)brain;

        for (int i = 0; i < other.weights.Length; i++)
        {
            biases[i] = (float[])other.biases[i].Clone();

            for (int j = 0; j < other.weights[i].Length; j++)
            {
                for (int k = 0; k < other.weights[i][j].Length; k++)
                {
                    weights[i][j][k] = other.weights[i][j][k];
                }
            }
        }
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

            for (int j = 0; j < biases[i].Length; j++)
            {
                biases[i][j] += UnityEngine.Random.Range(-rate, rate);
            }
        }
    }

    public float[] GetControlOutputs(float[] inputs)
    {
        float[] currentInput = inputs;

        for (int i = 0; i < weights.Length; i++)
        {
            FullyConnected(currentInput, weights[i], biases[i], layerBuffers[i]);

            if (i == weights.Length - 1)
                TanhInPlace(layerBuffers[i]);
            else
                ReluInPlace(layerBuffers[i]);

            currentInput = layerBuffers[i];
        }

        return layerBuffers[weights.Length - 1];
    }

    // Read-only forward pass that captures the post-activation value of EVERY node,
    // layer by layer, for the brain visualizer. activations[0] is the raw input
    // layer; activations[l] (l >= 1) is the output of weight layer l-1 (ReLU for
    // hidden layers, Tanh for the final layer) — mirroring GetControlOutputs.
    //
    // Deliberately allocates its own scratch buffers instead of reusing
    // layerBuffers, so calling it from the UI never clobbers the action the agent
    // cached on its last decision frame (JetAgent holds a reference to layerBuffers).
    public float[][] GetLayerActivations(float[] inputs)
    {
        float[][] activations = new float[weights.Length + 1][];
        activations[0] = (float[])inputs.Clone();

        float[] current = inputs;
        for (int i = 0; i < weights.Length; i++)
        {
            float[] output = new float[biases[i].Length];
            FullyConnected(current, weights[i], biases[i], output);

            if (i == weights.Length - 1)
                TanhInPlace(output);
            else
                ReluInPlace(output);

            activations[i + 1] = output;
            current = output;
        }

        return activations;
    }

    private void FullyConnected(float[] inputs, float[][] matrix, float[] layerBiases, float[] output)
    {
        for (int i = 0; i < output.Length; i++)
        {
            float sum = layerBiases[i];
            for (int j = 0; j < inputs.Length; j++)
            {
                sum += inputs[j] * matrix[j][i];
            }
            output[i] = sum;
        }
    }

    private void ReluInPlace(float[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] < 0f) values[i] = 0f;
        }
    }

    private void TanhInPlace(float[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (float)System.Math.Tanh(values[i]);
        }
    }

    public float[] Serialize()
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

            // Append biases for this layer after its weights
            for (int j = 0; j < biases[i].Length; j++)
            {
                flatWeights.Add(biases[i][j]);
            }
        }

        return flatWeights.ToArray();
    }

    public void Deserialize(float[] savedData)
    {
        int index = 0;

        for (int i = 0; i < weights.Length; i++)
        {
            for (int j = 0; j < weights[i].Length; j++)
            {
                for (int k = 0; k < weights[i][j].Length; k++)
                {
                    weights[i][j][k] = savedData[index];
                    index++;
                }
            }

            // Read biases for this layer after its weights
            for (int j = 0; j < biases[i].Length; j++)
            {
                biases[i][j] = savedData[index];
                index++;
            }
        }
    }
}
