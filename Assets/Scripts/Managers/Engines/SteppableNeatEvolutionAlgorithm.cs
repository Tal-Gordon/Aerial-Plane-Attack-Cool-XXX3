using SharpNeat.Core;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.SpeciationStrategies;

/// <summary>
/// Thin wrapper that exposes PerformOneGeneration() as a public method.
/// SharpNEAT 2.4 makes it protected because it expects to drive its own
/// background thread loop via StartContinue(). We need manual stepping
/// instead, since Unity's EvolutionaryParadigm drives the timing.
/// </summary>
public class SteppableNeatEvolutionAlgorithm<TGenome> : NeatEvolutionAlgorithm<TGenome>
    where TGenome : class, IGenome<TGenome>
{
    public SteppableNeatEvolutionAlgorithm(
        NeatEvolutionAlgorithmParameters eaParams,
        ISpeciationStrategy<TGenome> speciationStrategy,
        IComplexityRegulationStrategy complexityRegulationStrategy)
        : base(eaParams, speciationStrategy, complexityRegulationStrategy)
    {
    }

    /// <summary>
    /// Manually advances the algorithm by one generation.
    /// Calls the protected PerformOneGeneration() on the base class.
    /// </summary>
    public void StepOneGeneration()
    {
        base.PerformOneGeneration();
    }
}
