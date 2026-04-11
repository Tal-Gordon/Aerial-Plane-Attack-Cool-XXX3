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
}
