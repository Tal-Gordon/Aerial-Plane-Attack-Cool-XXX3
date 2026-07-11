using System;
using System.Collections.Generic;

/// <summary>
/// A complete, restorable snapshot of a training run for one
/// (GameMode, AIType) pair. Serialized to JSON by <see cref="DataManager"/>.
///
/// The bundle is split into four parts so each can evolve independently:
///   1. Settings           — jet/AI parameters (population size, mutation rate,
///                            network shape, RL hyperparameters, ...). Stored as
///                            the full SimulationSettings so every AI type
///                            round-trips its own knobs without bespoke code.
///   2. ObjectiveParameters — per-objective reward/terminal tuning
///                            (lambda for MaxAltitude, ~9 knobs for FlightSchool).
///   3. Stats               — informational numbers worth keeping around, plus
///                            the generation counter so training resumes its
///                            numbering instead of restarting at 1.
///   4. EngineState         — the actual brains. Opaque, engine-specific JSON
///                            produced by IEvolutionEngine.CaptureState() so the
///                            paradigm never needs to know a brain's layout.
/// </summary>
[Serializable]
public class TrainingSaveData
{
    // ── Identity ──────────────────────────────────────────────
    public AIType AIType;
    public DataManager.GameMode Mode;

    // ── Jet / AI parameters ───────────────────────────────────
    public SimulationSettings Settings;

    // ── Objective parameters ──────────────────────────────────
    public Dictionary<string, float> ObjectiveParameters;

    // ── Misc stats (informational; Generation is also restored) ──
    public int Generation;
    public int PopulationSize;
    public float ChampionScore;     // all-time best across the run
    public float TopScore;          // best jet in the population at save time
    public float AverageScore;      // mean fitness of the population at save time
    public string SavedAtUtc;

    // ── Engine brain payload (opaque, engine-specific JSON) ──
    public string EngineState;
}
