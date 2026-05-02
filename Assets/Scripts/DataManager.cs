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

    // ── Hard-coded defaults per mode ──────────────────────────────────────────

    private static readonly Dictionary<GameMode, SimulationSettings> Defaults =
        new()
        {
            [GameMode.MaxAltitude] = new SimulationSettings
            {
                PopulationSize = 2000,
                AIType = AIType.FixedNeuroEvo,
                SpawnRadius = 50f,
                SpawnFormation = SpawnFormation.Random,
                NeuroEvoSettings = new NeuroEvoSettings
                {
                    MutationRate = 0.1f,
                    NetworkShape = new[] { 12, 24, 12, 4 },
                },
            },
            [GameMode.FlightSchool] = new SimulationSettings
            {
                PopulationSize = 2000,
                AIType = AIType.FixedNeuroEvo,
                SpawnRadius = 0f,
                SpawnFormation = SpawnFormation.Random,
                NeuroEvoSettings = new NeuroEvoSettings
                {
                    MutationRate = 0.1f,
                    NetworkShape = new[] { 19, 16, 16, 4 },
                },
            },
            // [GameMode.FlightSchool] = new SimulationSettings
            // {
            //     PopulationSize = 2000,
            //     AIType = AIType.NEAT,
            //     SpawnRadius = 0f,
            //     SpawnFormation = SpawnFormation.Random,
            //     NeatSettings = new NeatSettings
            //     {
            //         InputSize = 19,
            //         OutputSize = 4,
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
    public float LearningRate = 3e-4f;
    public float Gamma = 0.99f;

    public RLSettings Clone() =>
        new()
        {
            LearningRate = LearningRate,
            Gamma = Gamma,
        };
}