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

    private string AlgorithmName => settings.AIType == AIType.SAC_MLAgents ? "SAC" : "PPO";
    private string YamlFileName => settings.AIType == AIType.SAC_MLAgents ? "jet_sac.yaml" : "jet_ppo.yaml";
    private string YamlPath => Path.Combine(Application.dataPath, "..", "config", YamlFileName);

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
        string runId = settings.AIType == AIType.SAC_MLAgents ? "sac_training" : "training";
        if (!trainerLauncher.Launch($"config/{YamlFileName}", runId))
        {
            Debug.LogError($"[RLParadigm] Python trainer failed to start ({AlgorithmName}). Agents will fall back to heuristic mode.");
        }

        cachedSnapshot = new SimulationSnapshot
        {
            ParadigmName = $"RL ({AlgorithmName})",
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

            ConfigureBehaviorParameters(jetAgent);
            ConfigureDecisionRequester(go);

            mlAgent.Inject(objective, this, i, population.Count);
            mlAgent.enabled = true;
            mlAgents.Add(mlAgent);

            jetAgent.ResetAgent();
            objective.SetStartingState(jetAgent, i, population.Count);
            go.SetActive(true);
        }
    }

    private void ConfigureBehaviorParameters(JetAgent jetAgent)
    {
        var rl = settings.RLSettings;
        var bp = jetAgent.gameObject.GetComponent<BehaviorParameters>();

        // The observation size must match the active sensor exactly or ML-Agents throws
        // at connect. Derive it from the sensor so the two can't silently drift; treat
        // RLSettings.InputSize as a sanity check and warn (don't fail) on mismatch.
        int observationSize = jetAgent.Sensor?.GetSensorCount() ?? rl.InputSize;
        if (jetAgent.Sensor != null && observationSize != rl.InputSize)
            Debug.LogWarning($"[RLParadigm] RLSettings.InputSize ({rl.InputSize}) does not match the active sensor's count ({observationSize}). Using the sensor count; update InputSize to silence this.");

        bp.BrainParameters.VectorObservationSize = observationSize;
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

        string yaml = settings.RLSettings.ToYaml(settings.AIType);
        File.WriteAllText(YamlPath, yaml);
        Debug.Log($"[RLParadigm] Training config written to {YamlPath}");
    }

    public void Dispose()
    {
        trainerLauncher?.Dispose();
    }
}
