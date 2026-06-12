using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Manages loading and saving of simulation settings and brain weights,
/// keyed by game mode. Each mode has baked-in defaults that are written
/// to disk on first access, then overridable by the user.
/// </summary>
public static class DataManager
{
    // ── Game modes ────────────────────────────────────────────────────────────

    public enum GameMode
    {
        MaxAltitude,
        FlightSchool,
        Dogfight,
        // Add future modes here
    }

    // ── Paths ─────────────────────────────────────────────────────────────────

    private static readonly string RootPath =
        Path.Combine(Application.persistentDataPath, "GameData");

    public static string ModePath(GameMode mode) =>
        Path.Combine(RootPath, mode.ToString());

    private static string SettingsPath(GameMode mode) =>
        Path.Combine(ModePath(mode), "settings.json");

    /// <summary>
    /// One save slot per (mode, AI type) so each AI/objective combination is
    /// stored separately and overwrites only its own previous save.
    /// </summary>
    public static string SaveStatePath(GameMode mode, AIType aiType) =>
        Path.Combine(ModePath(mode), $"save_{aiType}.json");

    // ── Hard-coded defaults per mode ──────────────────────────────────────────

    private static readonly Dictionary<GameMode, SimulationSettings> Defaults =
        new()
        {
            // [GameMode.MaxAltitude] = new SimulationSettings
            // {
            //     PopulationSize = 1000,
            //     AIType = AIType.FixedNeuroEvo,
            //     SpawnRadius = 50f,
            //     SpawnFormation = SpawnFormation.Random,
            //     NeuroEvoSettings = new NeuroEvoSettings
            //     {
            //         MutationRate = 0.1f,
            //         NetworkShape = new[] { 12, 24, 12, 4 },
            //     },
            // },
            // [GameMode.MaxAltitude] = new SimulationSettings
            // {
            //     PopulationSize = 1000,
            //     AIType = AIType.NEAT,
            //     SpawnRadius = 0f,
            //     SpawnFormation = SpawnFormation.Random,
            //     NeatSettings = new NeatSettings
            //     {
            //         InputSize = 12,
            //         OutputSize = 4,
            //     },
            //     RLSettings = new RLSettings
            //     {
            //         InputSize = 12,
            //         OutputSize = 4,
            //     },
            // },
            [GameMode.MaxAltitude] = new SimulationSettings
            {
                PopulationSize = 100,
                AIType = AIType.PPO_MLAgents,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                RLSettings = new RLSettings
                {
                    InputSize = 12,
                    OutputSize = 4,
                },
            },
            // [GameMode.MaxAltitude] = new SimulationSettings
            // {
            //     PopulationSize = 10,
            //     AIType = AIType.SAC_MLAgents,
            //     SpawnRadius = 0f,
            //     SpawnFormation = SpawnFormation.Random,
            //     RLSettings = new RLSettings
            //     {
            //         InputSize = 12,
            //         OutputSize = 4,
            //         BatchSize = 128,
            //         BufferSize = 50000,
            //     },
            // },
            // [GameMode.FlightSchool] = new SimulationSettings
            // {
            //     PopulationSize = 1111,
            //     AIType = AIType.FixedNeuroEvo,
            //     SpawnRadius = 0f,
            //     SpawnFormation = SpawnFormation.Random,
            //     NeuroEvoSettings = new NeuroEvoSettings
            //     {
            //         MutationRate = 0.1f,
            //         NetworkShape = new[] { 19, 16, 16, 4 },
            //     },
            // },
            // [GameMode.FlightSchool] = new SimulationSettings
            // {
            //     PopulationSize = 1000,
            //     AIType = AIType.NEAT,
            //     SpawnRadius = 0f,
            //     SpawnFormation = SpawnFormation.Random,
            //     NeatSettings = new NeatSettings
            //     {
            //         InputSize = 19,
            //         OutputSize = 4,
            //     },
            //     RLSettings = new RLSettings
            //     {
            //         InputSize = 19,
            //         OutputSize = 4,
            //     },
            // },
            [GameMode.FlightSchool] = new SimulationSettings
            {
                PopulationSize = 100,
                AIType = AIType.PPO_MLAgents,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                RLSettings = new RLSettings
                {
                    InputSize = 19,
                    OutputSize = 4,
                },
            },
            // [GameMode.FlightSchool] = new SimulationSettings
            // {
            //     PopulationSize = 10,
            //     AIType = AIType.SAC_MLAgents,
            //     SpawnRadius = 0f,
            //     SpawnFormation = SpawnFormation.Random,
            //     RLSettings = new RLSettings
            //     {
            //         InputSize = 19,
            //         OutputSize = 4,
            //         BatchSize = 128,
            //         BufferSize = 50000,
            //     },
            // },
            [GameMode.Dogfight] = new SimulationSettings
            {
                PopulationSize = 10,
                AIType = AIType.FixedNeuroEvo,
                SpawnRadius = 200f,
                SpawnFormation = SpawnFormation.Opposing,
                NeuroEvoSettings = new NeuroEvoSettings
                {
                    MutationRate = 0.08f,
                    NetworkShape = new[] { 12, 16, 4 }, // TODO change the input based on the assigned sensors
                },
            },
        };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns settings for <paramref name="mode"/>. If no saved settings exist
    /// on disk the hard-coded defaults are written and returned.
    /// </summary>
    public static SimulationSettings LoadSettings(GameMode mode)
    {
        string path = SettingsPath(mode);

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                SimulationSettings loaded =
                    JsonConvert.DeserializeObject<SimulationSettings>(json);

                if (loaded != null)
                {
                    /// TODO: this block patches older JSON files
                    /// that are missing the new subsettings for the current AI.
                    /// might not be needed in production.
                    SimulationSettings defaultSettings = GetDefaults(mode);
                    if (loaded.AIType == AIType.FixedNeuroEvo && loaded.NeuroEvoSettings == null)
                        loaded.NeuroEvoSettings = defaultSettings.NeuroEvoSettings ?? new NeuroEvoSettings();
                    if (loaded.AIType == AIType.NEAT && loaded.NeatSettings == null)
                        loaded.NeatSettings = defaultSettings.NeatSettings ?? new NeatSettings();
                    if ((loaded.AIType == AIType.PPO_MLAgents || loaded.AIType == AIType.SAC_MLAgents) && loaded.RLSettings == null)
                        loaded.RLSettings = defaultSettings.RLSettings ?? new RLSettings();
                    
                    return loaded;
                }

                Debug.LogWarning($"[DataManager] Corrupt settings for {mode}, reverting to defaults.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DataManager] Failed to read settings for {mode}: {e.Message}");
            }
        }

        // First run (or corrupt file) — persist defaults and return them
        SimulationSettings defaults = GetDefaults(mode);
        SaveSettings(mode, defaults);
        return defaults;
    }

    /// <summary>
    /// Persists <paramref name="settings"/> for <paramref name="mode"/> to disk.
    /// </summary>
    public static void SaveSettings(GameMode mode, SimulationSettings settings)
    {
        try
        {
            EnsureDirectory(ModePath(mode));
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsPath(mode), json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to save settings for {mode}: {e.Message}");
        }
    }

    /// <summary>
    /// Resets settings for <paramref name="mode"/> back to hard-coded defaults.
    /// </summary>
    public static SimulationSettings ResetToDefaults(GameMode mode)
    {
        SimulationSettings defaults = GetDefaults(mode);
        SaveSettings(mode, defaults);
        return defaults;
    }



    // ── Training state (full save/load) ────────────────────────────────────────

    /// <summary>
    /// True if a saved training run exists for this (mode, AI type) pair.
    /// </summary>
    public static bool HasTrainingState(GameMode mode, AIType aiType) =>
        File.Exists(SaveStatePath(mode, aiType));

    /// <summary>
    /// Persists a full training snapshot for <paramref name="mode"/> /
    /// <paramref name="aiType"/>, overwriting any previous save for that pair.
    /// </summary>
    public static void SaveTrainingState(GameMode mode, AIType aiType, TrainingSaveData data)
    {
        try
        {
            EnsureDirectory(ModePath(mode));
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(SaveStatePath(mode, aiType), json);
            Debug.Log($"[DataManager] Saved training state for {mode}/{aiType}.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to save training state for {mode}/{aiType}: {e.Message}");
        }
    }

    /// <summary>
    /// Loads the saved training snapshot for <paramref name="mode"/> /
    /// <paramref name="aiType"/>, or null if none exists / it is corrupt.
    /// </summary>
    public static TrainingSaveData LoadTrainingState(GameMode mode, AIType aiType)
    {
        string path = SaveStatePath(mode, aiType);
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<TrainingSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to read training state for {mode}/{aiType}: {e.Message}");
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SimulationSettings GetDefaults(GameMode mode)
    {
        if (Defaults.TryGetValue(mode, out SimulationSettings settings))
            return settings.Clone();

        Debug.LogError($"[DataManager] No defaults registered for mode {mode}. Returning empty settings.");
        return new SimulationSettings();
    }

    public static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}

// ── Data types ─────────────────────────────────────────────────────────────────

public enum AIType
{
    // Evolvable Brains
    FixedNeuroEvo,
    NEAT,

    // RL Brains
    PPO_MLAgents,
    SAC_MLAgents
}

public enum SpawnFormation
{
    Random,
    Grid,
    Circle,
    Opposing,
}

[Serializable]
public class SimulationSettings
{
    // ── Universal (every AI type needs these) ─────────────────────
    public int PopulationSize = 1000;
    public AIType AIType = AIType.NEAT;
    public float SpawnRadius = 0f;
    public SpawnFormation SpawnFormation = SpawnFormation.Random;

    // ── Paradigm-specific (null when irrelevant) ──────────────────
    public NeuroEvoSettings NeuroEvoSettings;
    public NeatSettings NeatSettings;
    public RLSettings RLSettings;

    // Add more universal fields here as your game modes require them
    // e.g. public float TimeLimit, public bool FriendlyFire, etc.

    /// <summary>
    /// Convenience: returns whichever EvoSettings sub-object is active,
    /// or null for non-evolutionary AI types.
    /// </summary>
    public EvoSettings ActiveEvoSettings
    {
        get
        {
            if (AIType == AIType.FixedNeuroEvo) return NeuroEvoSettings;
            if (AIType == AIType.NEAT) return NeatSettings;
            return null;
        }
    }

    /// <summary>Deep clone so defaults dict is never mutated.</summary>
    public SimulationSettings Clone() =>
        new()
        {
            PopulationSize = PopulationSize,
            AIType = AIType,
            SpawnRadius = SpawnRadius,
            SpawnFormation = SpawnFormation,
            NeuroEvoSettings = NeuroEvoSettings?.Clone() as NeuroEvoSettings,
            NeatSettings = NeatSettings?.Clone() as NeatSettings,
            RLSettings = RLSettings?.Clone(),
        };
}

[Serializable]
public class EvoSettings
{
    public float MutationRate = 0.1f;
    public float Lambda = 1.0f;

    public virtual EvoSettings Clone() =>
        new()
        {
            MutationRate = MutationRate,
            Lambda = Lambda,
        };
}

[Serializable]
public class NeuroEvoSettings : EvoSettings
{
    public int[] NetworkShape = { 6, 8, 4 };

    public override EvoSettings Clone() =>
        new NeuroEvoSettings
        {
            MutationRate = MutationRate,
            Lambda = Lambda,
            NetworkShape = (int[])NetworkShape.Clone(),
        };
}

[Serializable]
public class NeatSettings : EvoSettings
{
    // Future: complexity threshold, speciation params, etc.
    public int InputSize = 19;
    public int OutputSize = 4;

    public override EvoSettings Clone() =>
        new NeatSettings
        {
            MutationRate = MutationRate,
            Lambda = Lambda,
            InputSize = InputSize,
            OutputSize = OutputSize,
        };
}

[Serializable]
public class RLSettings
{
    // Network
    public int InputSize = 12;
    public int OutputSize = 4;
    public int HiddenUnits = 256;
    public int NumLayers = 2;
    public bool Normalize = true;

    // PPO hyperparameters
    public float LearningRate = 3e-4f;
    public int BatchSize = 4096;
    public int BufferSize = 20480;
    public float Beta = 5e-3f;
    public float Epsilon = 0.2f;
    public float Lambd = 0.95f;
    public int NumEpoch = 2;

    // Reward
    public float Gamma = 0.99f;

    // Run settings
    public int MaxSteps = 5000000;
    public int TimeHorizon = 128;
    public int DecisionPeriod = 5;

    // How often (in trainer steps) mlagents-learn writes a checkpoint to
    // results/<run-id>/. This bounds save granularity: SaveState can only
    // capture the latest checkpoint, so anything trained since it is not in
    // the save. Keep this small enough that pressing save shortly after
    // progress actually captures it; each checkpoint also exports an .onnx,
    // so very small values add periodic hitches during training.
    public int CheckpointInterval = 25000;

    // Engine settings. ML-Agents pushes this to Unity's Time.timeScale on connect.
    // Defaults to 1 so RL runs start at normal speed (like the evolutionary modes)
    // instead of mlagents-learn's hardcoded default of 20. The in-game slider can
    // still scale time up afterward. Raise this for faster headless training.
    public float TrainingTimeScale = 1f;

    // SAC hyperparameters
    public float InitEntCoef = 1.0f;
    public float Tau = 0.005f;
    public float StepsPerUpdate = 10f;
    public int BufferInitSteps = 0;

    public RLSettings Clone() =>
        new()
        {
            InputSize = InputSize,
            OutputSize = OutputSize,
            HiddenUnits = HiddenUnits,
            NumLayers = NumLayers,
            Normalize = Normalize,
            LearningRate = LearningRate,
            BatchSize = BatchSize,
            BufferSize = BufferSize,
            Beta = Beta,
            Epsilon = Epsilon,
            Lambd = Lambd,
            NumEpoch = NumEpoch,
            Gamma = Gamma,
            MaxSteps = MaxSteps,
            TimeHorizon = TimeHorizon,
            DecisionPeriod = DecisionPeriod,
            CheckpointInterval = CheckpointInterval,
            TrainingTimeScale = TrainingTimeScale,
            InitEntCoef = InitEntCoef,
            Tau = Tau,
            StepsPerUpdate = StepsPerUpdate,
            BufferInitSteps = BufferInitSteps,
        };

    public string ToYaml(AIType aiType, string behaviorName = "JetBrain")
    {
        if (aiType == AIType.SAC_MLAgents)
        {
            return $@"behaviors:
  {behaviorName}:
    trainer_type: sac

    hyperparameters:
      batch_size: {BatchSize}
      buffer_size: {BufferSize}
      learning_rate: {LearningRate:E1}
      buffer_init_steps: {BufferInitSteps}
      tau: {Tau}
      steps_per_update: {StepsPerUpdate:F1}
      init_entcoef: {InitEntCoef}
      learning_rate_schedule: constant

    network_settings:
      normalize: {Normalize.ToString().ToLower()}
      hidden_units: {HiddenUnits}
      num_layers: {NumLayers}

    reward_signals:
      extrinsic:
        gamma: {Gamma}
        strength: 1.0

    max_steps: {MaxSteps}
    time_horizon: {TimeHorizon}
    summary_freq: 10000
    keep_checkpoints: 5
    checkpoint_interval: {CheckpointInterval}

engine_settings:
  time_scale: {TrainingTimeScale}
";
        }

        return $@"behaviors:
  {behaviorName}:
    trainer_type: ppo

    hyperparameters:
      batch_size: {BatchSize}
      buffer_size: {BufferSize}
      learning_rate: {LearningRate:E1}
      beta: {Beta:E1}
      epsilon: {Epsilon}
      lambd: {Lambd}
      num_epoch: {NumEpoch}
      learning_rate_schedule: linear

    network_settings:
      normalize: {Normalize.ToString().ToLower()}
      hidden_units: {HiddenUnits}
      num_layers: {NumLayers}

    reward_signals:
      extrinsic:
        gamma: {Gamma}
        strength: 1.0

    max_steps: {MaxSteps}
    time_horizon: {TimeHorizon}
    summary_freq: 10000
    keep_checkpoints: 5
    checkpoint_interval: {CheckpointInterval}

engine_settings:
  time_scale: {TrainingTimeScale}
";
    }
}