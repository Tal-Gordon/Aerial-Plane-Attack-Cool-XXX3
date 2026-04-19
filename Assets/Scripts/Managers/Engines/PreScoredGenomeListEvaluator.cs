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
        for (int i = 0; i < genomeList.Count; i++)
        {
            // Ensure non-negative fitness (SharpNEAT requires fitness >= 0)
            double fitness = pendingScores != null && i < pendingScores.Count
                ? System.Math.Max(0.0, pendingScores[i])
                : 0.0;

            genomeList[i].EvaluationInfo.SetFitness(fitness);
        }
    }

    public void Reset() { }
}
