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

// TODO: Remove the debugging logs
public class NeatEngine : IEvolutionEngine
{
    private SimulationSettings currentSettings;

    private List<NeatBrain> currentBrains;
    private NeatBrain championBrain;
    private float championScore = float.NegativeInfinity;
    
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
        
        genomeParams.AddNodeMutationProbability = 0.02;        // 2% (default 1%)
        genomeParams.AddConnectionMutationProbability = 0.05;  // 5% (default 2.5%)
        genomeParams.DeleteConnectionMutationProbability = 0.02; // 2% (default 2.5%)
        genomeParams.ConnectionWeightMutationProbability = 0.96; // 96% (default 94%)

        // NEAT paper starts fully connected input→output with zero hidden nodes.
        // At 5% default, 19×4=76 possible connections yields ~4 actual connections,
        // leaving most outputs unconnected (stuck at sigmoid(0)=0.5 → remapped 0).
        genomeParams.InitialInterconnectionsProportion = 1.0;

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
        eaParams.ElitismProportion = 0.2;
        eaParams.SelectionProportion = 0.4;

        // Speciation strategy: groups genomes into species by similarity
        var speciationStrategy = new KMeansClusteringStrategy<NeatGenome>(new ManhattanDistanceMetric());

        // No complexity ceiling — let NEAT grow freely. The 1.5x relative ceiling
        // was suppressing structural mutations while the population was stuck at
        // minimal complexity due to the reward function punishing any behavior.
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

        // NEAT_DEBUG_LOG: Initialize logger
        NeatLogger.Initialize();
        NeatLogger.LogGenerationStart(1, currentSettings.PopulationSize, eaParams.SpecieCount, eaParams.ElitismProportion, eaParams.SelectionProportion);

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
        int bestIndex = 0;
        for (int i = 0; i < fitnessScores.Count; i++) 
        { 
            sumScore += fitnessScores[i]; 
            if (fitnessScores[i] > maxScore) 
            {
                maxScore = fitnessScores[i];
                bestIndex = i;
            }
        }
        float avgScore = fitnessScores.Count > 0 ? sumScore / fitnessScores.Count : 0f;
        Debug.Log($"[NeatEngine] Generation {currentGeneration} received scores -> Max: {maxScore}, Avg: {avgScore}");

        if (maxScore > championScore)
        {
            championScore = maxScore;
            NeatGenome bestGenomeThisGen = currentGenomeList[bestIndex];
            IBlackBox bestBlackBox = genomeDecoder.Decode(bestGenomeThisGen);
            championBrain = new NeatBrain(bestGenomeThisGen, bestBlackBox);
        }

        // NEAT_DEBUG_LOG: Log generation stats before creating next one
        NeatLogger.LogGenerationEnd(currentGeneration, fitnessScores, evolutionAlgorithm.CurrentChampGenome, evolutionAlgorithm.SpecieList);

        // Let SharpNEAT handle speciation, selection, crossover, mutation,
        // and creating the next generation. The evaluator is a no-op since
        // fitness was already stamped above.
        evolutionAlgorithm.StepOneGeneration();

        // NEAT_DEBUG_LOG: Log generation start
        NeatLogger.LogGenerationStart(currentGeneration + 1, currentSettings.PopulationSize, 25 /* eaParams.SpecieCount */, 0.3, 0.3);

        // Get the new population of genomes from the algorithm
        List<NeatGenome> genomeList = evolutionAlgorithm.GenomeList as List<NeatGenome>;

        // Decode all new genomes into usable brains
        currentBrains = DecodeBrains(genomeList);

        // Track the champion
        NeatGenome bestGenome = evolutionAlgorithm.CurrentChampGenome;
        
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
            NeatBrain brain = new NeatBrain(genomeList[i], blackBox);
            
            // NEAT_DEBUG_LOG: assign tracking info
            brain.GenomeId = genomeList[i].Id;
            bool isChamp = (evolutionAlgorithm != null && evolutionAlgorithm.CurrentChampGenome != null && genomeList[i].Id == evolutionAlgorithm.CurrentChampGenome.Id);
            brain.IsDebugTarget = isChamp || (i == 0 && (evolutionAlgorithm == null || evolutionAlgorithm.CurrentChampGenome == null));
            
            brains.Add(brain);
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
