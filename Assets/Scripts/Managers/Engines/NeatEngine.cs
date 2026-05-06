using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using SharpNeat.Core;
using SharpNeat.Genomes.Neat;
using SharpNeat.Decoders;
using SharpNeat.Decoders.Neat;
using SharpNeat.DistanceMetrics;
using SharpNeat.EvolutionAlgorithms;
using SharpNeat.EvolutionAlgorithms.ComplexityRegulation;
using SharpNeat.Phenomes;
using SharpNeat.SpeciationStrategies;

public class NeatEngine : IEvolutionEngine
{
    private SimulationSettings currentSettings;

    private List<NeatBrain> currentBrains;
    private NeatBrain championBrain;
    private float championScore;
    
    private int currentGeneration;
    // Debug
    private uint lastLoggedGenomeId;

    // SharpNEAT internals
    private NeatGenomeFactory genomeFactory;
    private NeatGenomeDecoder genomeDecoder;
    private SteppableNeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm;
    private PreScoredGenomeListEvaluator evaluator;

    public List<IEvolvableBrain> InitializeGeneration(SimulationSettings settings)
    {
        currentSettings = settings;
        var neatSettings = currentSettings.NeatSettings;

        // Genome parameters (mutation rates, etc.)
        var genomeParams = new NeatGenomeParameters();
        
        // [HYPERPARAMETER TUNING - Post Output Fix]
        // Increased structural mutation rates to make the AI more prone to change
        // while preserving enough stability to not shred good networks.
        genomeParams.AddNodeMutationProbability = 0.04;        // 4%  (default 1%)
        genomeParams.AddConnectionMutationProbability = 0.10;  // 10% (default 2.5%)
        genomeParams.DeleteConnectionMutationProbability = 0.04; // 4% (default 2.5%)
        
        // Weight mutation is the workhorse — keep it high but at default
        genomeParams.ConnectionWeightMutationProbability = 0.94; // 94% (default)

        // Start with all possible input→output connections instead of 5%
        // so initial genomes are functional from Gen 1 rather than near-empty
        // genomeParams.InitialInterconnectionsProportion = 1.0;

        // Factory creates and manages genomes
        genomeFactory = new NeatGenomeFactory(neatSettings.InputSize, neatSettings.OutputSize, genomeParams);

        // Decoder converts genomes (genotype) into blackboxes (phenotype) for inference
        // Using Cyclic scheme with 1 timestep per simulation tick handles recurrent connections safely.
        genomeDecoder = new NeatGenomeDecoder(NetworkActivationScheme.CreateCyclicFixedTimestepsScheme(1));

        // Our custom evaluator that stamps externally-collected fitness scores
        evaluator = new PreScoredGenomeListEvaluator();

        // Evolution algorithm parameters
        var eaParams = new NeatEvolutionAlgorithmParameters();
        
        // [HYPERPARAMETER TUNING]
        // Give new structures a bit more opportunity to be tested before being wiped out:
        // 1. Increased species count to protect innovations in more niches.
        // 2. Higher selection proportion allows a wider range of parents to reproduce.
        // 3. Higher elitism proportion protects more top performers in each species.
        eaParams.SpecieCount = 25; 
        eaParams.ElitismProportion = 0.3; 
        eaParams.SelectionProportion = 0.3; 

        // Speciation strategy: groups genomes into species by similarity
        var speciationStrategy = new KMeansClusteringStrategy<NeatGenome>(new ManhattanDistanceMetric());

        // Complexity regulation: prevents network bloat by switching to a
        // simplifying mode when complexity grows too fast relative to fitness gains.
        // Replaces NullComplexityRegulationStrategy which allowed unchecked growth.
        var complexityRegulation = new DefaultComplexityRegulationStrategy(ComplexityCeilingType.Relative, 1.5);

        // Create the evolution algorithm
        evolutionAlgorithm = new SteppableNeatEvolutionAlgorithm<NeatGenome>(
            eaParams,
            speciationStrategy,
            complexityRegulation
        );

        // Create initial random population
        List<NeatGenome> genomeList = genomeFactory.CreateGenomeList(currentSettings.PopulationSize, 0);

        // Initialize the algorithm with our evaluator and population
        evolutionAlgorithm.Initialize(evaluator, genomeFactory, genomeList);

        // Decode all genomes into brains for the simulation
        currentBrains = DecodeBrains(genomeList);

        // Hold a reference to the first as initial champion
        championBrain = currentBrains[0];
        championScore = float.NegativeInfinity;
        currentGeneration = 1;
        
        // Debug
        lastLoggedGenomeId = genomeList[0].Id;
        Debug.Log($"[NeatEngine] Initialization: Starting population with example Genome ID [{lastLoggedGenomeId}] (Nodes: {genomeList[0].NeuronGeneList.Count}, Edges: {genomeList[0].ConnectionGeneList.Count})");
        
        return new List<IEvolvableBrain>(currentBrains);
    }

    public List<IEvolvableBrain> EvolveNextGeneration(List<float> fitnessScores)
    {
        Debug.Log($"[NeatEngine] Evolving next generation. Population size: {currentSettings.PopulationSize}");
        if (currentSettings.PopulationSize == 0) return new List<IEvolvableBrain>(currentBrains);

        // Stamp fitness scores directly onto the genome objects BEFORE calling
        // StepOneGeneration(). This is critical because PerformOneGeneration()
        // internally creates offspring and rebuilds the genome list as
        // [elites... | offspring...] BEFORE calling Evaluate(). If we relied on
        // index-based stamping inside Evaluate(), scores would be assigned to
        // the wrong genomes.
        List<NeatGenome> currentGenomeList = evolutionAlgorithm.GenomeList as List<NeatGenome>;
        StampFitnessScores(currentGenomeList, fitnessScores);

        // Debug
        float maxScore = float.NegativeInfinity;
        float sumScore = 0f;
        foreach (var s in fitnessScores) 
        { 
            sumScore += s; 
            if (s > maxScore) maxScore = s; 
        }
        float avgScore = fitnessScores.Count > 0 ? sumScore / fitnessScores.Count : 0f;
        Debug.Log($"[NeatEngine] Generation {currentGeneration} received scores -> Max: {maxScore}, Avg: {avgScore}");

        // Let SharpNEAT handle speciation, selection, crossover, mutation,
        // and creating the next generation. The evaluator is a no-op since
        // fitness was already stamped above.
        evolutionAlgorithm.StepOneGeneration();

        // Get the new population of genomes from the algorithm
        List<NeatGenome> genomeList = evolutionAlgorithm.GenomeList as List<NeatGenome>;

        // Decode all new genomes into usable brains
        currentBrains = DecodeBrains(genomeList);

        // Track the champion
        NeatGenome bestGenome = evolutionAlgorithm.CurrentChampGenome;
        float bestFitness = (float)bestGenome.EvaluationInfo.Fitness;

        if (bestFitness > championScore)
        {
            IBlackBox bestBlackBox = genomeDecoder.Decode(bestGenome);
            championBrain = new NeatBrain(bestGenome, bestBlackBox);
            championScore = bestFitness;
        }
        
        // Debug
        // Log a sample genome ID that is NOT the current champion to show evolution is happening.
        // This avoids the "elitism trap" where the champion genome ID remains the same across generations.
        NeatGenome sampleOffspring = null;
        foreach (var genome in genomeList)
        {
            if (genome.Id != bestGenome.Id && genome.Id != lastLoggedGenomeId)
            {
                sampleOffspring = genome;
                break;
            }
        }

        if (sampleOffspring != null)
        {
            // Debug
            Debug.Log($"[NeatEngine] Generation {currentGeneration} evolution check: Example offspring Genome ID [{sampleOffspring.Id}] (Nodes: {sampleOffspring.NeuronGeneList.Count}, Edges: {sampleOffspring.ConnectionGeneList.Count})");
            lastLoggedGenomeId = sampleOffspring.Id;
        }

        currentGeneration++;
        return new List<IEvolvableBrain>(currentBrains);
    }

    public IEvolvableBrain GetChampionBrain()
    {
        return championBrain;
    }

    public float GetChampionScore()
    {
        return championScore;
    }

    public void SaveChampion(string directoryPath)
    {
        try
        {
            string filePath = Path.Combine(directoryPath, "champion.genome.xml");

            XmlWriterSettings writerSettings = new XmlWriterSettings { Indent = true };
            using (XmlWriter writer = XmlWriter.Create(filePath, writerSettings))
            {
                NeatGenomeXmlIO.WriteComplete(writer, championBrain.Genome, true);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NeatEngine] Failed to save champion: {e.Message}");
        }
    }

    public void LoadChampion(string directoryPath)
    {
        try
        {
            string filePath = Path.Combine(directoryPath, "champion.genome.xml");
            if (!File.Exists(filePath))
            {
                Debug.LogWarning("[NeatEngine] No saved champion found.");
                return;
            }

            using (XmlReader reader = XmlReader.Create(filePath))
            {
                NeatGenome genome = NeatGenomeXmlIO.ReadCompleteGenomeList(reader, true, genomeFactory)[0];
                IBlackBox blackBox = genomeDecoder.Decode(genome);
                championBrain = new NeatBrain(genome, blackBox);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NeatEngine] Failed to load champion: {e.Message}");
        }
    }

    // Helper: decode a list of genomes into NeatBrain wrappers
    private List<NeatBrain> DecodeBrains(List<NeatGenome> genomeList)
    {
        var brains = new List<NeatBrain>(genomeList.Count);
        for (int i = 0; i < genomeList.Count; i++)
        {
            IBlackBox blackBox = genomeDecoder.Decode(genomeList[i]);
            brains.Add(new NeatBrain(genomeList[i], blackBox));
        }
        return brains;
    }

    /// <summary>
    /// Stamps fitness scores directly onto genome objects. Must be called
    /// BEFORE StepOneGeneration() while the genome list is still in the
    /// same order as our decoded brains.
    /// 
    /// SharpNEAT requires fitness >= 0, so we shift negative scores up
    /// and add a small baseline to prevent zero-fitness species wipeouts.
    /// </summary>
    private void StampFitnessScores(List<NeatGenome> genomeList, List<float> scores)
    {
        // Find minimum score for shifting
        float minScore = float.MaxValue;
        for (int i = 0; i < scores.Count; i++)
        {
            if (scores[i] < minScore) minScore = scores[i];
        }

        // Shift negative scores to positive; add baseline so worst != 0
        float shift = minScore < 0f ? System.Math.Abs(minScore) : 0f;
        float baseline = 1.0f;

        for (int i = 0; i < genomeList.Count; i++)
        {
            double fitness = i < scores.Count
                ? scores[i] + shift + baseline
                : baseline;

            genomeList[i].EvaluationInfo.SetFitness(fitness);
        }
    }
}
