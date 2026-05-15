using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

public class RLParadigm : ITrainingParadigm
{
    private IObjective objective;
    private SimulationSettings settings;
    private List<JetAgent> population;
    private List<JetMLAgent> mlAgents;

    private float[] cumulativeScores;
    private int totalEpisodes;
    private float bestCumulativeScore;
    private float trainingStartTime;

    private SimulationSnapshot cachedSnapshot;
    private TrainerProcessLauncher trainerLauncher;

    private static readonly string YamlPath =
        Path.Combine(Application.dataPath, "..", "config", "jet_ppo.yaml");

    public void Initialize(List<JetAgent> population, SimulationSettings settings, IObjective objective)
    {
        this.population = population;
        this.settings = settings;
        this.objective = objective;

        mlAgents = new List<JetMLAgent>(population.Count);
        cumulativeScores = new float[population.Count];

        trainingStartTime = Time.time;

        WriteYamlConfig();

        trainerLauncher = new TrainerProcessLauncher();
        if (!trainerLauncher.Launch("config/jet_ppo.yaml"))
        {
            Debug.LogError("[RLParadigm] Python trainer failed to start. Agents will fall back to heuristic mode.");
        }

        cachedSnapshot = new SimulationSnapshot
        {
            ParadigmName = "RL (PPO)",
            Population = population,
            RLData = new RLSnapshot()
        };

        for (int i = 0; i < population.Count; i++)
        {
            var jetAgent = population[i];
            var go = jetAgent.gameObject;

            var mlAgent = go.GetComponent<JetMLAgent>();
            if (mlAgent == null)
                mlAgent = go.AddComponent<JetMLAgent>();

            ConfigureBehaviorParameters(go);
            ConfigureDecisionRequester(go);

            mlAgent.Inject(objective, this, i, population.Count);
            mlAgent.enabled = true;
            mlAgents.Add(mlAgent);

            jetAgent.ResetAgent();
            objective.SetStartingState(jetAgent, i, population.Count);
            go.SetActive(true);
        }
    }

    private void ConfigureBehaviorParameters(GameObject go)
    {
        var rl = settings.RLSettings;
        var bp = go.GetComponent<BehaviorParameters>();

        bp.BrainParameters.VectorObservationSize = rl.InputSize;
        bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(rl.OutputSize);
        bp.BehaviorName = "JetBrain";
        bp.BehaviorType = BehaviorType.Default;
    }

    private void ConfigureDecisionRequester(GameObject go)
    {
        var dr = go.GetComponent<DecisionRequester>();
        if (dr == null)
            dr = go.AddComponent<DecisionRequester>();
        dr.DecisionPeriod = settings.RLSettings.DecisionPeriod;
        dr.TakeActionsBetweenDecisions = true;
        dr.enabled = true;
    }

    public void Tick()
    {
        cachedSnapshot.AgentsAlive = population.Count;
    }

    public SimulationSnapshot GetSnapshot()
    {
        cachedSnapshot.IterationNumber = totalEpisodes;
        cachedSnapshot.ChampionScore = bestCumulativeScore;
        cachedSnapshot.RLData.TotalEpisodes = totalEpisodes;
        cachedSnapshot.RLData.TrainingTime = Time.time - trainingStartTime;
        cachedSnapshot.RLData.BestCumulativeScore = bestCumulativeScore;
        cachedSnapshot.RLData.CumulativeScores = cumulativeScores;

        return cachedSnapshot;
    }

    public void RecordEpisodeEnd(int agentIndex, float episodeReward)
    {
        totalEpisodes++;
        cumulativeScores[agentIndex] += episodeReward;

        if (cumulativeScores[agentIndex] > bestCumulativeScore)
            bestCumulativeScore = cumulativeScores[agentIndex];
    }

    public IBrain GetChampionBrain()
    {
        return null;
    }

    public float GetChampionScore()
    {
        return bestCumulativeScore;
    }

    public void SaveChampion(string directoryPath)
    {
        Debug.Log("[RLParadigm] Champion saving not yet implemented. Train via mlagents-learn and export the .onnx model.");
    }

    private void WriteYamlConfig()
    {
        string dir = Path.GetDirectoryName(YamlPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string yaml = settings.RLSettings.ToYaml();
        File.WriteAllText(YamlPath, yaml);
        Debug.Log($"[RLParadigm] Training config written to {YamlPath}");
    }

    public void Dispose()
    {
        trainerLauncher?.Dispose();
    }
}
