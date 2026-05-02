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
        
        // [HYPERPARAMETER TUNING]
        // You mentioned it progresses super slowly and changes are barely noticeable.
        // We will massively bump the structural mutations so it builds complex brains faster.
        genomeParams.AddNodeMutationProbability = 0.10;       // 10% chance to add a node (very high)
        genomeParams.AddConnectionMutationProbability = 0.20; // 20% chance to add a connection
        genomeParams.DeleteConnectionMutationProbability = 0.05; // 5% chance to prune dead weight
        
        // Ensure weights are mutating aggressively
        genomeParams.ConnectionWeightMutationProbability = 0.98;

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
        // You mentioned the groups (species) are barely different. 
        // With a population of 1000 and SpecieCount = 10, each species had 100 jets. This forces very different 
        // topologies to be unfairly clustered and killed off.
        // Increasing to 40 forces SharpNEAT to isolate and protect 40 distinct topological "ideas".
        eaParams.SpecieCount = 40; 

        // Speciation strategy: groups genomes into species by similarity
        var speciationStrategy = new KMeansClusteringStrategy<NeatGenome>(new ManhattanDistanceMetric());

        // Complexity regulation: controls bloat (NullStrategy = no regulation for now)
        var complexityRegulation = new NullComplexityRegulationStrategy();

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

        // Buffer the fitness scores from the simulation into the evaluator.
        // When PerformOneGeneration() calls evaluator.Evaluate(), it will
        // stamp these scores onto the genomes.
        evaluator.SetScores(fitnessScores);

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

        // Let SharpNEAT handle everything: evaluation, speciation, selection,
        // crossover, mutation, and creating the next generation.
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
}
