using System.Collections.Generic;
using UnityEngine;

public interface ITrainingParadigm
{
    /// <summary>
    /// Called once by SimulationManager after instantiating the population.
    /// The paradigm stores the list and owns agent lifecycle from here on.
    /// </summary>
    public void Initialize(List<JetAgent> population, SimulationSettings settings, IObjective objective);

    /// <summary>
    /// Called every FixedUpdate by SimulationManager.
    /// Evo: loops agents, checks terminal states, evolves when all dead.
    /// RL:  loops agents, checks terminal states, resets individuals immediately.
    /// </summary>
    public void Tick();

    /// <summary>
    /// Returns a SimulationSnapshot with all paradigm-owned fields filled.
    /// SimulationManager stamps on TimeScale and SelectedAgent afterward.
    /// </summary>
    public SimulationSnapshot GetSnapshot();

    /// <summary>
    /// Unsubscribes from static events and cleans up.
    /// Called by SimulationManager.OnDestroy() or on paradigm swap.
    /// </summary>
    public void Dispose();

    /// <summary>
    /// Returns the current all-time best brain across any generation/episode.
    /// Used for saving to disk or injecting into opponent jets.
    /// </summary>
    public IBrain GetChampionBrain();

    /// <summary>
    /// Returns the current all-time best score across any generation/episode.
    /// </summary>
    public float GetChampionScore();

    /// <summary>
    /// Saves the champion brain to the given directory.
    /// Delegates to the underlying engine or framework.
    /// </summary>
    public void SaveChampion(string directoryPath);

    /// <summary>
    /// Captures the full training state (every brain, AI/objective parameters,
    /// and stats) into a TrainingSaveData and persists it via DataManager,
    /// keyed by the current mode + AI type. Overwrites any previous save.
    /// </summary>
    public void SaveState();

    /// <summary>
    /// Restores the state previously written by <see cref="SaveState"/> into the
    /// already-initialized population, then respawns the jets so training
    /// continues from the saved run (evo: overwrites the fresh brains; RL: starts
    /// the trainer in resume mode from the saved checkpoint). Settings and
    /// objective parameters are applied by SimulationManager before the
    /// population is rebuilt.
    /// </summary>
    public void LoadState();

    /// <summary>
    /// Loads the champion from the saved training run for the current mode + AI
    /// type and returns it as a standalone, ready-to-run brain (no learning, no
    /// mutation). Used by inference mode to replay the best individual. Does not
    /// touch the live population. Returns null if there is no restorable champion
    /// or the paradigm does not support inference yet.
    /// </summary>
    public IBrain LoadChampionBrain();

    // ── Inference replay ─────────────────────────────────────────────
    // Inference reduces the run to a single jet that replays the saved champion /
    // policy on a loop with NO learning and NO saving. SimulationManager tears
    // down the training run, spawns and sensor-wires exactly one jet, re-creates
    // the paradigm, and calls Initialize → StartInference. From there the paradigm
    // owns the replay; the manager only pumps TickInference each FixedUpdate.

    /// <summary>
    /// True when this paradigm can replay its saved run without learning.
    /// </summary>
    public bool CanRunInference { get; }

    /// <summary>
    /// Switches the (already Initialize-d, single-jet) population into inference
    /// replay: loads the saved champion/policy and starts driving the lone jet.
    /// Returns false if it could not set up (e.g. no usable saved model), so the
    /// manager can abort back to training. No learning, no saving occurs.
    /// </summary>
    public bool StartInference();

    /// <summary>
    /// Per-FixedUpdate step while inference replay is active. The evolutionary
    /// paradigm pumps the objective and respawns the jet to loop the course; the
    /// RL paradigm is driven by ML-Agents/Academy so this is a no-op.
    /// </summary>
    public void TickInference();
}
