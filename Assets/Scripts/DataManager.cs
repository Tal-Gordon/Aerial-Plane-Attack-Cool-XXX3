using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

/// <summary>
/// Manages loading and saving of simulation settings and brain weights.
/// Settings + baked-in defaults are keyed by game mode (the objective family),
/// but on-disk SAVE SLOTS are keyed by TRACK (the active scene) so that two
/// scenes sharing an objective type — e.g. three FlightSchool tracks — each keep
/// an independent save and never overwrite one another.
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

    /// <summary>
    /// Stable storage key for the active scene (track). Each scene owns its own
    /// save slot under GameData/&lt;track&gt;/, so two tracks that share an objective
    /// type (e.g. three FlightSchool tracks) never overwrite each other's saves.
    /// Sanitized so it is safe both as a folder name and as an mlagents --run-id
    /// token (no spaces or path separators).
    /// </summary>
    public static string CurrentTrack => ResolveTrackId(SceneManager.GetActiveScene().name);

    /// <summary>
    /// Sanitizes a scene name into a filesystem- and CLI-safe track id (letters,
    /// digits and '-' kept; everything else, including spaces, becomes '_').
    /// "Track 1" → "Track_1", "Max Altitude" → "Max_Altitude".
    /// </summary>
    public static string ResolveTrackId(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return "UnknownTrack";

        var sb = new StringBuilder(sceneName.Length);
        foreach (char c in sceneName)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '_');
        return sb.ToString();
    }

    /// <summary>Root folder for one track's save slot: GameData/&lt;track&gt;/.</summary>
    public static string TrackPath(string track) =>
        Path.Combine(RootPath, track);

    private static string SettingsPath(string track) =>
        Path.Combine(TrackPath(track), "settings.json");

    /// <summary>
    /// One save slot per (track, AI type) so each scene/AI combination is stored
    /// separately and overwrites only its own previous save. Tracks that share an
    /// objective type still get independent slots because the key is the scene,
    /// not the objective/mode.
    /// </summary>
    public static string SaveStatePath(string track, AIType aiType) =>
        Path.Combine(TrackPath(track), $"save_{aiType}.json");

    // ── Hard-coded defaults per (mode, AI type) ───────────────────────────────
    // Every (mode, AI type) the menu can pick has an entry, so any AI type works
    // in any mode. The InputSize / NetworkShape[0] values here are nominal:
    // SimulationManager overrides the input width from the mode's active sensor at
    // runtime (see SimulationManager.ApplyObservationSizeFromSensors), so these
    // stay correct even if a sensor's observation count changes.

    private static readonly Dictionary<(GameMode Mode, AIType AI), SimulationSettings> Defaults =
        new()
        {
            // ── MaxAltitude (BasicFlight sensor, 12 inputs) ───────────────────
            [(GameMode.MaxAltitude, AIType.FixedNeuroEvo)] = new SimulationSettings
            {
                PopulationSize = 1000,
                AIType = AIType.FixedNeuroEvo,
                SpawnRadius = 50f,
                SpawnFormation = SpawnFormation.Random,
                NeuroEvoSettings = new NeuroEvoSettings
                {
                    MutationRate = 0.1f,
                    NetworkShape = new[] { 12, 24, 12, 4 },
                },
            },
            [(GameMode.MaxAltitude, AIType.NEAT)] = new SimulationSettings
            {
                PopulationSize = 1000,
                AIType = AIType.NEAT,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                NeatSettings = new NeatSettings { InputSize = 12, OutputSize = 4 },
            },
            [(GameMode.MaxAltitude, AIType.PPO_MLAgents)] = new SimulationSettings
            {
                PopulationSize = 100,
                AIType = AIType.PPO_MLAgents,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                RLSettings = new RLSettings { InputSize = 12, OutputSize = 4 },
            },
            [(GameMode.MaxAltitude, AIType.SAC_MLAgents)] = new SimulationSettings
            {
                PopulationSize = 10,
                AIType = AIType.SAC_MLAgents,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                RLSettings = new RLSettings
                {
                    InputSize = 12,
                    OutputSize = 4,
                    BatchSize = 128,
                    BufferSize = 50000,
                },
            },

            // ── FlightSchool (Waypoint sensor, 19 inputs) ─────────────────────
            [(GameMode.FlightSchool, AIType.FixedNeuroEvo)] = new SimulationSettings
            {
                PopulationSize = 1111,
                AIType = AIType.FixedNeuroEvo,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                NeuroEvoSettings = new NeuroEvoSettings
                {
                    MutationRate = 0.1f,
                    NetworkShape = new[] { 19, 16, 16, 4 },
                },
            },
            [(GameMode.FlightSchool, AIType.NEAT)] = new SimulationSettings
            {
                PopulationSize = 1000,
                AIType = AIType.NEAT,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                NeatSettings = new NeatSettings { InputSize = 19, OutputSize = 4 },
            },
            [(GameMode.FlightSchool, AIType.PPO_MLAgents)] = new SimulationSettings
            {
                PopulationSize = 100,
                AIType = AIType.PPO_MLAgents,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                RLSettings = new RLSettings { InputSize = 19, OutputSize = 4 },
            },
            [(GameMode.FlightSchool, AIType.SAC_MLAgents)] = new SimulationSettings
            {
                PopulationSize = 10,
                AIType = AIType.SAC_MLAgents,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                RLSettings = new RLSettings
                {
                    InputSize = 19,
                    OutputSize = 4,
                    BatchSize = 128,
                    BufferSize = 50000,
                },
            },

            // ── Dogfight (BasicFlight sensor, 12 inputs) ──────────────────────
            [(GameMode.Dogfight, AIType.FixedNeuroEvo)] = new SimulationSettings
            {
                PopulationSize = 10,
                AIType = AIType.FixedNeuroEvo,
                SpawnRadius = 200f,
                SpawnFormation = SpawnFormation.Opposing,
                NeuroEvoSettings = new NeuroEvoSettings
                {
                    MutationRate = 0.08f,
                    NetworkShape = new[] { 12, 16, 4 },
                },
            },
        };

    // ── Baked reward-parameter defaults per mode ──────────────────────────────
    // The canonical default reward-shaping values for each objective, as a flat
    // key → value map matching that objective's GetParameters keys. These used to
    // live as field initializers on the objective MonoBehaviours; centralizing them
    // here makes DataManager the single source of truth (mirroring the hyperparameter
    // Defaults above). Objectives seed their fields from here on Awake, and the
    // hyperparameter editor's "reset to default" restores them.
    private static readonly Dictionary<GameMode, Dictionary<string, float>> RewardDefaults =
        new()
        {
            [GameMode.MaxAltitude] = new()
            {
                ["lambda"] = 10f,
                ["maxTimeAllowed"] = 15f,
            },
            [GameMode.FlightSchool] = new()
            {
                ["hoopRadius"] = 170f,
                ["lambda"] = 1f,
                ["distanceRewardMultiplier"] = 0.4f,
                ["hoopPassReward"] = 2000f,
                ["backwardsDriftPenalty"] = 2f,
                ["lookAtRewardWeight"] = 10f,
                ["maxTimeAllowed"] = 180f,
                ["timeBonusMultiplier"] = 10f,
                ["timeBetweenHoopsAllowed"] = 10f,
            },
        };

    // AI type used when only a mode is known (first run / LoadSettings, before the
    // menu selection is applied). Pick the paradigm each mode is primarily tuned for.
    private static readonly Dictionary<GameMode, AIType> PrimaryAIType =
        new()
        {
            [GameMode.MaxAltitude] = AIType.PPO_MLAgents,
            [GameMode.FlightSchool] = AIType.NEAT,
            [GameMode.Dogfight] = AIType.FixedNeuroEvo,
        };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns settings for <paramref name="track"/>. Settings are stored per track
    /// (so each scene keeps its own tuning), but the baked defaults used on first
    /// access come from <paramref name="mode"/> (the objective family). If no saved
    /// settings exist on disk the hard-coded defaults are written and returned.
    /// </summary>
    public static SimulationSettings LoadSettings(string track, GameMode mode)
    {
        string path = SettingsPath(track);

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
        SaveSettings(track, defaults);
        return defaults;
    }

    /// <summary>
    /// Persists <paramref name="settings"/> for <paramref name="track"/> to disk.
    /// </summary>
    public static void SaveSettings(string track, SimulationSettings settings)
    {
        try
        {
            EnsureDirectory(TrackPath(track));
            string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsPath(track), json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to save settings for {track}: {e.Message}");
        }
    }

    /// <summary>
    /// Returns a fresh copy of the baked default reward parameters for
    /// <paramref name="mode"/>, as a flat key → value map matching the objective's
    /// GetParameters keys. Empty if the mode has no registered objective defaults.
    /// </summary>
    public static Dictionary<string, float> GetDefaultRewardParameters(GameMode mode) =>
        RewardDefaults.TryGetValue(mode, out Dictionary<string, float> defaults)
            ? new Dictionary<string, float>(defaults)
            : new Dictionary<string, float>();

    /// <summary>
    /// Resets the settings stored for <paramref name="track"/> back to the
    /// hard-coded defaults for <paramref name="mode"/>.
    /// </summary>
    public static SimulationSettings ResetToDefaults(string track, GameMode mode)
    {
        SimulationSettings defaults = GetDefaults(mode);
        SaveSettings(track, defaults);
        return defaults;
    }



    // ── Training state (full save/load) ────────────────────────────────────────

    /// <summary>
    /// True if a saved training run exists for this (track, AI type) pair.
    /// </summary>
    public static bool HasTrainingState(string track, AIType aiType) =>
        File.Exists(SaveStatePath(track, aiType));

    /// <summary>
    /// Persists a full training snapshot for <paramref name="track"/> /
    /// <paramref name="aiType"/>, overwriting any previous save for that pair.
    /// </summary>
    public static void SaveTrainingState(string track, AIType aiType, TrainingSaveData data)
    {
        try
        {
            EnsureDirectory(TrackPath(track));
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(SaveStatePath(track, aiType), json);
            Debug.Log($"[DataManager] Saved training state for {track}/{aiType}.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to save training state for {track}/{aiType}: {e.Message}");
        }
    }

    /// <summary>
    /// Loads the saved training snapshot for <paramref name="track"/> /
    /// <paramref name="aiType"/>, or null if none exists / it is corrupt.
    /// </summary>
    public static TrainingSaveData LoadTrainingState(string track, AIType aiType)
    {
        string path = SaveStatePath(track, aiType);
        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<TrainingSaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to read training state for {track}/{aiType}: {e.Message}");
            return null;
        }
    }

    // ── Protected (temporary) save slot ────────────────────────────────────────
    // The reward-parameter commit round-trips Save→Load to apply staged changes
    // while keeping the trained brains/policy. To avoid clobbering the user's real
    // manual save, the caller backs it up before the round-trip and restores it
    // after. A save slot is the JSON file plus, for RL, the checkpoint directory.

    /// <summary>RL checkpoint directory for a slot — mirrors RLParadigm.SaveCheckpointDir.</summary>
    private static string CheckpointDirPath(string track, AIType aiType) =>
        Path.Combine(TrackPath(track), $"rl_checkpoint_{aiType}");

    private static string BackupStatePath(string track, AIType aiType) =>
        Path.Combine(TrackPath(track), $"save_{aiType}.bak.json");

    private static string BackupCheckpointDirPath(string track, AIType aiType) =>
        Path.Combine(TrackPath(track), $"rl_checkpoint_{aiType}.bak");

    /// <summary>
    /// Copies the current save slot (JSON + RL checkpoint dir) aside so a
    /// subsequent overwrite can be undone with <see cref="RestoreTrainingStateBackup"/>.
    /// Copies (not moves) so the original is always recoverable if interrupted.
    /// </summary>
    public static void BackupTrainingState(string track, AIType aiType)
    {
        try
        {
            string json = SaveStatePath(track, aiType);
            if (File.Exists(json))
                File.Copy(json, BackupStatePath(track, aiType), overwrite: true);

            string checkpointDir = CheckpointDirPath(track, aiType);
            if (Directory.Exists(checkpointDir))
            {
                string backupDir = BackupCheckpointDirPath(track, aiType);
                if (Directory.Exists(backupDir)) Directory.Delete(backupDir, true);
                CopyDirectory(checkpointDir, backupDir);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to back up training state for {track}/{aiType}: {e.Message}");
        }
    }

    /// <summary>
    /// Restores the backup made by <see cref="BackupTrainingState"/> over the real
    /// slot, discarding whatever was written in between and removing the backup.
    /// </summary>
    public static void RestoreTrainingStateBackup(string track, AIType aiType)
    {
        try
        {
            string backupJson = BackupStatePath(track, aiType);
            if (File.Exists(backupJson))
            {
                string json = SaveStatePath(track, aiType);
                if (File.Exists(json)) File.Delete(json);
                File.Move(backupJson, json);
            }

            string backupDir = BackupCheckpointDirPath(track, aiType);
            if (Directory.Exists(backupDir))
            {
                string checkpointDir = CheckpointDirPath(track, aiType);
                if (Directory.Exists(checkpointDir)) Directory.Delete(checkpointDir, true);
                Directory.Move(backupDir, checkpointDir);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to restore training state backup for {track}/{aiType}: {e.Message}");
        }
    }

    /// <summary>
    /// Deletes the save slot (JSON + RL checkpoint dir) for a (track, AI type).
    /// Used to remove a temporary slot created only as a Save→Load vehicle when
    /// the user had no real save to protect.
    /// </summary>
    public static void DeleteTrainingState(string track, AIType aiType)
    {
        try
        {
            string json = SaveStatePath(track, aiType);
            if (File.Exists(json)) File.Delete(json);

            string checkpointDir = CheckpointDirPath(track, aiType);
            if (Directory.Exists(checkpointDir)) Directory.Delete(checkpointDir, true);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to delete training state for {track}/{aiType}: {e.Message}");
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (string file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);

        foreach (string subDir in Directory.GetDirectories(sourceDir))
            CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SimulationSettings GetDefaults(GameMode mode)
    {
        AIType primary = PrimaryAIType.TryGetValue(mode, out AIType ai) ? ai : AIType.NEAT;
        return GetDefaults(mode, primary);
    }

    /// <summary>
    /// Returns a deep clone of the baked defaults for a (mode, AI type) pair. If
    /// the exact pair has no entry, synthesizes one from the mode's primary default
    /// (keeping its universal fields) plus a fresh paradigm-specific block for the
    /// requested AI type, so every AI type is selectable in every mode. InputSize is
    /// nominal here — SimulationManager overrides it from the active sensor.
    /// </summary>
    public static SimulationSettings GetDefaults(GameMode mode, AIType aiType)
    {
        if (Defaults.TryGetValue((mode, aiType), out SimulationSettings exact))
            return exact.Clone();

        Debug.LogWarning($"[DataManager] No baked default for {mode}/{aiType}; synthesizing from the mode's primary default.");

        SimulationSettings basis =
            PrimaryAIType.TryGetValue(mode, out AIType primary) &&
            Defaults.TryGetValue((mode, primary), out SimulationSettings p)
                ? p.Clone()
                : new SimulationSettings();

        basis.AIType = aiType;
        EnsureSubSettings(basis);
        return basis;
    }

    // Ensures the paradigm-specific sub-settings object the AI type needs is
    // non-null. Universal fields (population, spawn) are left untouched.
    private static void EnsureSubSettings(SimulationSettings s)
    {
        switch (s.AIType)
        {
            case AIType.FixedNeuroEvo:
                s.NeuroEvoSettings ??= new NeuroEvoSettings();
                break;
            case AIType.NEAT:
                s.NeatSettings ??= new NeatSettings();
                break;
            case AIType.PPO_MLAgents:
            case AIType.SAC_MLAgents:
                s.RLSettings ??= new RLSettings();
                break;
        }
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

    // Frame-skip / action-repeat cadence for evolutionary agents (the mirror of
    // RLSettings.DecisionPeriod, which RL keeps separately). 1 = decide every frame,
    // i.e. the original behavior; existing saves lacking this field deserialize to 1.
    public int DecisionPeriod = 1;

    public virtual EvoSettings Clone() =>
        new()
        {
            MutationRate = MutationRate,
            Lambda = Lambda,
            DecisionPeriod = DecisionPeriod,
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
            DecisionPeriod = DecisionPeriod,
            NetworkShape = (int[])NetworkShape.Clone(),
        };
}

[Serializable]
public class NeatSettings : EvoSettings
{
    public int InputSize = 19;
    public int OutputSize = 4;

    // SharpNEAT tunables. Defaults match the values NeatEngine.BuildScaffolding
    // used to hard-code, so existing saves (which lack these fields) behave
    // exactly as before. All are "hot": a save→load round-trip rebuilds the
    // SharpNEAT scaffolding from these, and the restored population keeps
    // evolving under the new values. (MutationRate, inherited from EvoSettings,
    // is unused by NEAT — SharpNEAT drives mutation via the probabilities below.)
    public int SpecieCount = 10;
    public float ElitismProportion = 0.2f;
    public float SelectionProportion = 0.4f;
    public float AddNodeMutationProbability = 0.02f;
    public float AddConnectionMutationProbability = 0.05f;
    public float DeleteConnectionMutationProbability = 0.02f;
    public float ConnectionWeightMutationProbability = 0.96f;

    public override EvoSettings Clone() =>
        new NeatSettings
        {
            MutationRate = MutationRate,
            Lambda = Lambda,
            DecisionPeriod = DecisionPeriod,
            InputSize = InputSize,
            OutputSize = OutputSize,
            SpecieCount = SpecieCount,
            ElitismProportion = ElitismProportion,
            SelectionProportion = SelectionProportion,
            AddNodeMutationProbability = AddNodeMutationProbability,
            AddConnectionMutationProbability = AddConnectionMutationProbability,
            DeleteConnectionMutationProbability = DeleteConnectionMutationProbability,
            ConnectionWeightMutationProbability = ConnectionWeightMutationProbability,
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

    // Window the trainer asks Unity to adopt on connect. ML-Agents applies these via
    // Screen.SetResolution in a STANDALONE build (the editor ignores them, which is why
    // this only bites in a build). Without them the trainer's 84x84 default shrinks the
    // build window to a tiny, unmaximizable square.
    public int WindowWidth = 1280;
    public int WindowHeight = 720;

    // Render frame cap the trainer pushes on connect. ML-Agents defaults to -1 (uncapped),
    // which pegs the GPU rendering as fast as possible. This caps RENDERING only; raise
    // TrainingTimeScale for faster simulation. Set to -1 to restore uncapped behaviour.
    public int TargetFrameRate = 60;

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
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            TargetFrameRate = TargetFrameRate,
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
  width: {WindowWidth}
  height: {WindowHeight}
  time_scale: {TrainingTimeScale}
  target_frame_rate: {TargetFrameRate}
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
  width: {WindowWidth}
  height: {WindowHeight}
  time_scale: {TrainingTimeScale}
  target_frame_rate: {TargetFrameRate}
";
    }
}