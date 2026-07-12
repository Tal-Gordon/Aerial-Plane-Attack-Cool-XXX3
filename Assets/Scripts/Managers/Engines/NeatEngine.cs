using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Newtonsoft.Json;
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
    private float championScore = float.NegativeInfinity;

    private int currentGeneration;

    // SharpNEAT internals
    private NeatGenomeFactory genomeFactory;
    private NeatGenomeDecoder genomeDecoder;
    private SteppableNeatEvolutionAlgorithm<NeatGenome> evolutionAlgorithm;
    private NeatEvolutionAlgorithmParameters eaParams;
    private PreScoredGenomeListEvaluator evaluator;
    private int lastGenerationBestEliteIndex;

    // TODO: Make it so that in the flight school objective it starts with 0 edges
    // TODO: Make sure the negative score handling doesn't cause issues (NEAT expects positive fitness values, so we shift them all up by the absolute value of the most negative score + a small baseline to avoid zero fitness)
    public List<IEvolvableBrain> InitializeGeneration(SimulationSettings settings)
    {
        currentSettings = settings;

        BuildScaffolding();

        List<NeatGenome> genomeList = genomeFactory.CreateGenomeList(currentSettings.PopulationSize, 0);
        evolutionAlgorithm.Initialize(evaluator, genomeFactory, genomeList);

        currentBrains = DecodeBrains(genomeList);

        championBrain = currentBrains[0];
        championScore = float.NegativeInfinity;
        currentGeneration = 1;

        return new List<IEvolvableBrain>(currentBrains);
    }

    // Builds the SharpNEAT scaffolding (genome factory, decoder, evaluator, EA)
    // from currentSettings. Shared by InitializeGeneration (fresh genomes) and
    // RestoreState (loaded genomes) so both run on identical configuration.
    // Leaves the EA uninitialized — the caller supplies the seed genome list.
    private void BuildScaffolding()
    {
        var neatSettings = currentSettings.NeatSettings;

        var genomeParams = new NeatGenomeParameters();

        // Read from NeatSettings (was hard-coded). These are runtime-tunable via
        // the hyperparameter tuner; BuildScaffolding re-runs on RestoreState, so a
        // committed change is picked up on the save→load round-trip.
        genomeParams.AddNodeMutationProbability = neatSettings.AddNodeMutationProbability;
        genomeParams.AddConnectionMutationProbability = neatSettings.AddConnectionMutationProbability;
        genomeParams.DeleteConnectionMutationProbability = neatSettings.DeleteConnectionMutationProbability;
        genomeParams.ConnectionWeightMutationProbability = neatSettings.ConnectionWeightMutationProbability;

        // NEAT paper starts fully connected input→output with zero hidden nodes.
        // At 5% default, 19×4=76 possible connections yields ~4 actual connections,
        // leaving most outputs unconnected (stuck at sigmoid(0)=0.5 → remapped 0).
        // genomeParams.InitialInterconnectionsProportion = 1.0;

        genomeFactory = new NeatGenomeFactory(neatSettings.InputSize, neatSettings.OutputSize, genomeParams);
        genomeDecoder = new NeatGenomeDecoder(NetworkActivationScheme.CreateCyclicFixedTimestepsScheme(1));
        evaluator = new PreScoredGenomeListEvaluator();

        eaParams = new NeatEvolutionAlgorithmParameters();

        eaParams.SpecieCount = neatSettings.SpecieCount;
        eaParams.ElitismProportion = neatSettings.ElitismProportion;
        eaParams.SelectionProportion = neatSettings.SelectionProportion;

        var speciationStrategy = new KMeansClusteringStrategy<NeatGenome>(new ManhattanDistanceMetric());

        // No complexity ceiling — let NEAT grow freely. The 1.5x relative ceiling
        // was suppressing structural mutations while the population was stuck at
        // minimal complexity due to the reward function punishing any behavior.
        var complexityRegulation = new NullComplexityRegulationStrategy();

        evolutionAlgorithm = new SteppableNeatEvolutionAlgorithm<NeatGenome>(
            eaParams,
            speciationStrategy,
            complexityRegulation
        );
    }

    public List<IEvolvableBrain> EvolveNextGeneration(List<float> fitnessScores)
    {
        if (currentSettings.PopulationSize == 0) return new List<IEvolvableBrain>(currentBrains);

        // Stamp fitness scores directly onto the genome objects BEFORE calling
        // StepOneGeneration(). This is critical because PerformOneGeneration()
        // internally creates offspring and rebuilds the genome list as
        // [elites... | offspring...] BEFORE calling Evaluate(). If we relied on
        // index-based stamping inside Evaluate(), scores would be assigned to
        // the wrong genomes.
        List<NeatGenome> currentGenomeList = evolutionAlgorithm.GenomeList as List<NeatGenome>;
        StampFitnessScores(currentGenomeList, fitnessScores);

        // Track the all-time champion
        float maxScore = float.NegativeInfinity;
        int bestIndex = 0;
        for (int i = 0; i < fitnessScores.Count; i++)
        {
            if (fitnessScores[i] > maxScore)
            {
                maxScore = fitnessScores[i];
                bestIndex = i;
            }
        }

        if (maxScore > championScore)
        {
            championScore = maxScore;
            NeatGenome bestGenomeThisGen = currentGenomeList[bestIndex];
            IBlackBox bestBlackBox = genomeDecoder.Decode(bestGenomeThisGen);
            championBrain = new NeatBrain(bestGenomeThisGen, bestBlackBox);
        }

        uint bestGenomeIdThisGeneration = currentGenomeList[bestIndex].Id;

        // SharpNEAT 2.4 uses probabilistic rounding for per-species elite counts,
        // which can randomly wipe small species (including ones with top performers).
        // We save the top genomes and re-inject any that get lost.
        int eliteGuardCount = Mathf.Max(1, Mathf.CeilToInt(currentGenomeList.Count * 0.02f));
        var savedElites = new List<NeatGenome>(eliteGuardCount);
        {
            var sorted = new List<NeatGenome>(currentGenomeList);
            sorted.Sort((a, b) => b.EvaluationInfo.Fitness.CompareTo(a.EvaluationInfo.Fitness));
            for (int i = 0; i < eliteGuardCount && i < sorted.Count; i++)
                savedElites.Add(sorted[i]);
        }

        evolutionAlgorithm.StepOneGeneration();

        List<NeatGenome> genomeList = evolutionAlgorithm.GenomeList as List<NeatGenome>;

        InjectMissingElites(savedElites, genomeList);

        // SharpNEAT groups the new list by species, so the overall winner is not
        // necessarily at index zero. The elite guard above guarantees it survives;
        // locate its new slot for the spectator camera.
        lastGenerationBestEliteIndex = genomeList.FindIndex(g => g.Id == bestGenomeIdThisGeneration);
        if (lastGenerationBestEliteIndex < 0) lastGenerationBestEliteIndex = 0;

        currentBrains = DecodeBrains(genomeList);

        currentGeneration++;
        return new List<IEvolvableBrain>(currentBrains);
    }

    public int GetLastGenerationBestEliteIndex() => lastGenerationBestEliteIndex;

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

    public string CaptureState()
    {
        // Snapshot the live genomes backing the current brains. The whole population
        // and the champion are serialized with SharpNEAT's complete-genome XML
        // (same format as SaveChampion), each stored as an XML string inside an
        // opaque NeatEngineState JSON blob.
        var genomes = new List<NeatGenome>(currentBrains.Count);
        foreach (NeatBrain brain in currentBrains)
            genomes.Add(brain.Genome);

        var state = new NeatEngineState
        {
            PopulationXml = WriteGenomesToXml(genomes),
            ChampionXml = championBrain != null ? WriteGenomesToXml(new List<NeatGenome> { championBrain.Genome }) : null,
            ChampionScore = championScore,
            Generation = currentGeneration,
        };

        return JsonConvert.SerializeObject(state);
    }

    public List<IEvolvableBrain> RestoreState(string stateJson, SimulationSettings settings)
    {
        currentSettings = settings;

        var state = JsonConvert.DeserializeObject<NeatEngineState>(stateJson);

        // Rebuild the SharpNEAT scaffolding before any genome can be read — genomes
        // cannot be deserialized without a live factory + decoder.
        BuildScaffolding();

        // ReadCompleteGenomeList reseeds the factory's genome/innovation ID
        // generators above the max loaded ID and reattaches each genome to the
        // factory, so subsequent mutation/crossover never reissues an existing ID.
        // (Verified in SharpNeatLib 2.4.4: it calls GenomeIdGenerator.Reset /
        // InnovationIdGenerator.Reset with Math.Max(Peek, maxId + 1).)
        List<NeatGenome> genomeList = ReadGenomesFromXml(state.PopulationXml);

        evolutionAlgorithm.Initialize(evaluator, genomeFactory, genomeList);

        currentBrains = DecodeBrains(genomeList);

        // Restore the champion from its own genome (decoded independently — it is
        // not part of the live EA population, only used for reporting). Reading it
        // through the same factory keeps the ID generators monotonic.
        if (!string.IsNullOrEmpty(state.ChampionXml))
        {
            NeatGenome championGenome = ReadGenomesFromXml(state.ChampionXml)[0];
            IBlackBox championBlackBox = genomeDecoder.Decode(championGenome);
            championBrain = new NeatBrain(championGenome, championBlackBox);
        }
        else
        {
            championBrain = currentBrains[0];
        }

        championScore = state.ChampionScore;
        currentGeneration = state.Generation;

        return new List<IEvolvableBrain>(currentBrains);
    }

    private static string WriteGenomesToXml(IList<NeatGenome> genomes)
    {
        var sb = new StringBuilder();
        var writerSettings = new XmlWriterSettings { Indent = false };
        using (XmlWriter writer = XmlWriter.Create(sb, writerSettings))
        {
            NeatGenomeXmlIO.WriteComplete(writer, genomes, true);
        }
        return sb.ToString();
    }

    private List<NeatGenome> ReadGenomesFromXml(string xml)
    {
        using (var stringReader = new StringReader(xml))
        using (XmlReader reader = XmlReader.Create(stringReader))
        {
            return NeatGenomeXmlIO.ReadCompleteGenomeList(reader, true, genomeFactory);
        }
    }

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

    private void InjectMissingElites(List<NeatGenome> savedElites, List<NeatGenome> genomeList)
    {
        var postEvoIds = new HashSet<uint>();
        foreach (var g in genomeList)
            postEvoIds.Add(g.Id);

        foreach (var elite in savedElites)
        {
            if (postEvoIds.Contains(elite.Id))
                continue;

            int worstIdx = 0;
            double worstFitness = genomeList[0].EvaluationInfo.Fitness;
            for (int i = 1; i < genomeList.Count; i++)
            {
                if (genomeList[i].EvaluationInfo.Fitness < worstFitness)
                {
                    worstFitness = genomeList[i].EvaluationInfo.Fitness;
                    worstIdx = i;
                }
            }

            NeatGenome replaced = genomeList[worstIdx];
            genomeList[worstIdx] = elite;

            var specieList = evolutionAlgorithm.SpecieList;
            for (int s = 0; s < specieList.Count; s++)
            {
                var specieGenomes = specieList[s].GenomeList;
                int idx = specieGenomes.IndexOf(replaced);
                if (idx >= 0)
                {
                    specieGenomes[idx] = elite;
                    break;
                }
            }

            postEvoIds.Add(elite.Id);
        }
    }

    private void StampFitnessScores(List<NeatGenome> genomeList, List<float> scores)
    {
        float minScore = float.MaxValue;
        for (int i = 0; i < scores.Count; i++)
        {
            if (scores[i] < minScore) minScore = scores[i];
        }

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

/// <summary>
/// Serializable genome payload for NeatEngine. Stored as the opaque EngineState
/// string inside a TrainingSaveData. The genomes are kept as SharpNEAT complete
/// XML strings (same format as NeatEngine.SaveChampion) — opaque to everyone but
/// NeatEngine.
/// </summary>
[Serializable]
public class NeatEngineState
{
    public string PopulationXml;   // complete-genome XML for the whole population
    public string ChampionXml;     // complete-genome XML for the champion (single genome)
    public float ChampionScore;    // raw champion score (pre fitness-shift)
    public int Generation;
}
