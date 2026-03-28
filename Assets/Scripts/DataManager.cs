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

    private static string ModePath(GameMode mode) =>
        Path.Combine(RootPath, mode.ToString());

    private static string SettingsPath(GameMode mode) =>
        Path.Combine(ModePath(mode), "settings.json");

    private static string BrainPath(GameMode mode, string brainName) =>
        Path.Combine(ModePath(mode), $"{brainName}.brain.json");

    // ── Hard-coded defaults per mode ──────────────────────────────────────────

    private static readonly Dictionary<GameMode, SimulationSettings> Defaults =
        new()
        {
            [GameMode.MaxAltitude] = new SimulationSettings
            {
                PopulationSize = 20,
                MutationRate = 0.5f,
                NetworkShape = new[] { 12, 24, 12, 4 },   // inputs, hidden, outputs
                AIType = AIType.FixedNeuroEvo,
                SpawnRadius = 50f,
                SpawnFormation = SpawnFormation.Random,
            },
            [GameMode.FlightSchool] = new SimulationSettings
            {
                PopulationSize = 20,
                MutationRate = 0.5f,
                NetworkShape = new[] { 12, 24, 12, 4 },   // TODO change the input based on the assigned sensors
                AIType = AIType.FixedNeuroEvo,
                SpawnRadius = 50f,
                SpawnFormation = SpawnFormation.Random,
            },
            [GameMode.Dogfight] = new SimulationSettings
            {
                PopulationSize = 10,
                MutationRate = 0.08f,
                NetworkShape = new[] { 12, 16, 4 },
                AIType = AIType.FixedNeuroEvo,
                SpawnRadius = 200f,
                SpawnFormation = SpawnFormation.Opposing,
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
                    return loaded;

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

    /// <summary>
    /// Saves a brain's weight array for <paramref name="mode"/>.
    /// </summary>
    public static void SaveBrain(GameMode mode, string brainName, float[] weights)
    {
        try
        {
            EnsureDirectory(ModePath(mode));
            string json = JsonConvert.SerializeObject(weights, Formatting.Indented);
            File.WriteAllText(BrainPath(mode, brainName), json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to save brain '{brainName}' for {mode}: {e.Message}");
        }
    }

    /// <summary>
    /// Loads a brain's weight array for <paramref name="mode"/>.
    /// Returns <c>null</c> if no saved brain exists.
    /// </summary>
    public static float[] LoadBrain(GameMode mode, string brainName)
    {
        string path = BrainPath(mode, brainName);

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[DataManager] No saved brain '{brainName}' found for {mode}.");
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<float[]>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataManager] Failed to load brain '{brainName}' for {mode}: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Returns <c>true</c> if a saved brain file exists for this mode.
    /// </summary>
    public static bool HasSavedBrain(GameMode mode, string brainName) =>
        File.Exists(BrainPath(mode, brainName));

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SimulationSettings GetDefaults(GameMode mode)
    {
        if (Defaults.TryGetValue(mode, out SimulationSettings settings))
            return settings.Clone();

        Debug.LogError($"[DataManager] No defaults registered for mode {mode}. Returning empty settings.");
        return new SimulationSettings();
    }

    private static void EnsureDirectory(string path)
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
    public int PopulationSize = 10;
    public float MutationRate = 0.1f;
    public int[] NetworkShape = { 6, 8, 4 };
    public AIType AIType = AIType.FixedNeuroEvo;
    public float SpawnRadius = 100f;
    public SpawnFormation SpawnFormation = SpawnFormation.Random;

    // Add more fields here as your game modes require them
    // e.g. public float TimeLimit, public bool FriendlyFire, etc.

    /// <summary>Deep clone so defaults dict is never mutated.</summary>
    public SimulationSettings Clone() =>
        new()
        {
            PopulationSize = PopulationSize,
            MutationRate = MutationRate,
            NetworkShape = (int[])NetworkShape.Clone(),
            AIType = AIType,
            SpawnRadius = SpawnRadius,
            SpawnFormation = SpawnFormation,
        };
}