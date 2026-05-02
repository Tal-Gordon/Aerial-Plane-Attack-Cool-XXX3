using System.Collections.Generic;
using SharpNeat.Core;
using SharpNeat.Genomes.Neat;

/// <summary>
/// A custom IGenomeListEvaluator that stamps pre-collected fitness scores
/// onto genomes. This bridges the gap between SharpNEAT's internal evolution
/// loop and our paradigm-driven architecture where agents are simulated
/// externally in Unity's physics loop.
/// </summary>
public class PreScoredGenomeListEvaluator : IGenomeListEvaluator<NeatGenome>
{
    private List<float> pendingScores;

    public ulong EvaluationCount => 0;
    public bool StopConditionSatisfied => false;

    /// <summary>
    /// Buffer the fitness scores collected from the simulation.
    /// Must be called BEFORE PerformOneGeneration().
    /// </summary>
    public void SetScores(List<float> scores)
    {
        pendingScores = scores;
    }

    /// <summary>
    /// Called internally by NeatEvolutionAlgorithm.PerformOneGeneration().
    /// We just stamp the pre-collected scores onto the genomes.
    /// </summary>
    public void Evaluate(IList<NeatGenome> genomeList)
    {
        if (pendingScores == null || pendingScores.Count == 0) return;

        // 1. Find the minimum score in this generation
        float minScore = float.MaxValue;
        for (int i = 0; i < pendingScores.Count; i++)
        {
            if (pendingScores[i] < minScore)
            {
                minScore = pendingScores[i];
            }
        }

        // 2. Calculate the shift required to make the lowest score positive
        // If minScore is negative, we shift everything up by its absolute value.
        // We also add a small baseline (e.g., 1.0) so the worst agent isn't exactly 0.0, 
        // which prevents species wipeouts or divide-by-zero in selection.
        float shift = minScore < 0f ? System.Math.Abs(minScore) : 0f;
        float baseline = 1.0f;

        for (int i = 0; i < genomeList.Count; i++)
        {
            double fitness = i < pendingScores.Count
                ? pendingScores[i] + shift + baseline
                : baseline;

            genomeList[i].EvaluationInfo.SetFitness(fitness);
        }
    }

    public void Reset() { }
}
