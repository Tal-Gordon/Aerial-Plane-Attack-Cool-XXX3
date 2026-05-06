using System.Collections.Generic;
using SharpNeat.Core;
using SharpNeat.Genomes.Neat;

/// <summary>
/// A custom IGenomeListEvaluator that acts as a no-op pass-through.
///
/// ARCHITECTURE NOTE: SharpNEAT's PerformOneGeneration() calls Evaluate() AFTER
/// it has already created offspring and rebuilt the genome list in a new order
/// ([elites... | offspring...]). This means we CANNOT rely on index-based score
/// stamping inside Evaluate() — the genomes are no longer in the order we
/// simulated them.
///
/// Instead, NeatEngine stamps fitness scores directly onto the genome objects
/// BEFORE calling StepOneGeneration(). By the time Evaluate() is called,
/// the elites already have correct fitness from the previous step, and
/// offspring will be evaluated in the NEXT generation after they are simulated.
/// </summary>
public class PreScoredGenomeListEvaluator : IGenomeListEvaluator<NeatGenome>
{
    public ulong EvaluationCount => 0;
    public bool StopConditionSatisfied => false;

    /// <summary>
    /// Called internally by NeatEvolutionAlgorithm.PerformOneGeneration().
    /// This is intentionally a no-op because fitness has already been stamped
    /// onto genomes by NeatEngine before StepOneGeneration() is called.
    /// </summary>
    public void Evaluate(IList<NeatGenome> genomeList)
    {
        // No-op: fitness was pre-stamped by NeatEngine.EvolveNextGeneration()
        // before PerformOneGeneration() was called.
    }

    public void Reset() { }
}
