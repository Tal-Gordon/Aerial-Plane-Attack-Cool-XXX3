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

        // Factory creates and manages genomes
        genomeFactory = new NeatGenomeFactory(neatSettings.InputSize, neatSettings.OutputSize, genomeParams);

        // Decoder converts genomes (genotype) into blackboxes (phenotype) for inference
        // Using Cyclic scheme with 1 timestep per simulation tick handles recurrent connections safely.
        genomeDecoder = new NeatGenomeDecoder(NetworkActivationScheme.CreateCyclicFixedTimestepsScheme(1));

        // Our custom evaluator that stamps externally-collected fitness scores
        evaluator = new PreScoredGenomeListEvaluator();

        // Evolution algorithm parameters
        var eaParams = new NeatEvolutionAlgorithmParameters();
        eaParams.SpecieCount = 10;

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
