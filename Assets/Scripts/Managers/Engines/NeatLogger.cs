using UnityEngine;
using System.IO;
using System.Text;
using SharpNeat.Genomes.Neat;
using System.Collections.Generic;

// NEAT_DEBUG_LOG: Entire file added for debug logging
public static class NeatLogger
{
    public static bool IsDebugModeEnabled = false; // Set to false to easily disable all NEAT logging
    private static string logPath;
    private static int currentGeneration = 0;
    
    public static void Initialize()
    {
        if (!IsDebugModeEnabled) return;
        logPath = Path.Combine(Application.dataPath, "NeatDebugLog.txt");
        File.WriteAllText(logPath, "--- NEAT Debug Log Start ---\n");
    }

    public static void LogGenerationStart(int gen, int popSize, int speciesCount, double elitism, double selection)
    {
        if (!IsDebugModeEnabled) return;
        currentGeneration = gen;
        string msg = $"\n=== GENERATION {gen} STARTED ===\n" +
                     $"Population: {popSize} | Target Species: {speciesCount} | Elitism: {elitism:P0} | Selection: {selection:P0}\n";
        File.AppendAllText(logPath, msg);
    }

    public static void LogGenerationEnd(int gen, List<float> fitnessScores, NeatGenome bestGenome, IList<SharpNeat.Core.Specie<NeatGenome>> speciesList)
    {
        if (!IsDebugModeEnabled) return;
        float maxScore = float.NegativeInfinity;
        float minScore = float.PositiveInfinity;
        float sumScore = 0f;

        foreach (var s in fitnessScores)
        {
            sumScore += s;
            if (s > maxScore) maxScore = s;
            if (s < minScore) minScore = s;
        }
        float avgScore = fitnessScores.Count > 0 ? sumScore / fitnessScores.Count : 0f;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"--- GENERATION {gen} ENDED ---");
        sb.AppendLine($"Max Score: {maxScore:F2} | Min Score: {minScore:F2} | Avg Score: {avgScore:F2}");
        if (bestGenome != null)
        {
            sb.AppendLine($"Best Genome ID: {bestGenome.Id} | Nodes: {bestGenome.NeuronGeneList.Count} | Edges: {bestGenome.ConnectionGeneList.Count}");
        }

        if (speciesList != null)
        {
            sb.AppendLine("Species Breakdown:");
            for (int i = 0; i < speciesList.Count; i++)
            {
                var s = speciesList[i];
                double champFitness = s.GenomeList.Count > 0 ? s.GenomeList[0].EvaluationInfo.Fitness : 0;
                sb.AppendLine($"  Species [{s.Id}] - Size: {s.GenomeList.Count} | Champ Fitness: {champFitness:F2}");
            }
        }
                     
        File.AppendAllText(logPath, sb.ToString());
    }

    public static void LogGenomeIO(uint genomeId, bool isChampion, int tickCount, float[] inputs, float[] outputs)
    {
        if (!IsDebugModeEnabled) return;
        // Only log first 5 ticks to check if initial state is broken, 
        // OR log every 50th tick for the champion to see if it changes over time.
        bool shouldLog = (tickCount <= 5) || (isChampion && tickCount % 50 == 0);
        
        if (!shouldLog) return;

        string inStr = string.Join(", ", FormatArray(inputs));
        string outStr = string.Join(", ", FormatArray(outputs));
        string role = isChampion ? "CHAMPION" : "AGENT";

        string msg = $"[Gen {currentGeneration} | Tick {tickCount}] {role} {genomeId} | IN: [{inStr}] | OUT: [{outStr}]\n";
        File.AppendAllText(logPath, msg);
    }
    
    private static string[] FormatArray(float[] arr)
    {
        string[] res = new string[arr.Length];
        for(int i=0; i<arr.Length; i++) res[i] = arr[i].ToString("F2");
        return res;
    }

    public static void LogMessage(string msg)
    {
        if (!IsDebugModeEnabled) return;
        File.AppendAllText(logPath, $"[INFO] {msg}\n");
    }
}
