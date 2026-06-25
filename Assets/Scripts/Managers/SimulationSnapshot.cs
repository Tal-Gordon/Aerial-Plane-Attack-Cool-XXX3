using System.Collections.Generic;
using UnityEngine;

public class SimulationSnapshot
{
    // ── Generic (filled by every paradigm) ───────────────────────────
    public int IterationNumber;          // generation for evo, episode for RL
    public int AgentsAlive;
    public float ChampionScore;
    public string ParadigmName;
    public List<JetAgent> Population;

    // ── Filled by SimulationManager, not paradigm ────────────────────
    public float TimeScale;
    public JetAgent SelectedAgent;

    // True while the run is replaying a saved champion/policy (no learning, no
    // saving) instead of training. Lets UI reflect the current mode and stay in
    // sync with the inference hotkeys. Stamped from SimulationManager.
    public bool InInferenceMode;

    // Whether the population thins out gradually within an iteration (jets
    // crashing at different times). True for FlightSchool, false for objectives
    // like MaxAltitude where every jet ends on the same shared time limit, so an
    // "alive" count carries no information. Stamped from the active IObjective.
    public bool TracksAttrition;

    // ── Paradigm-specific sub-snapshots (null if irrelevant) ─────────
    public EvoSnapshot EvoData;
    public RLSnapshot RLData;
}

public class EvoSnapshot
{
    public IEvolvableBrain ChampionBrain;
    public float MutationRate;
    public float Lambda;

    // Mean final fitness of the LAST completed generation. Captured once when the
    // generation ends, so it holds steady (and stays comparable to the champion
    // score) instead of climbing from zero as a new generation's jets accumulate.
    public float LastGenerationAverage;

    // Best final fitness within that same last generation (the generation's top
    // performer, not the all-time champion). Pairs with LastGenerationAverage to
    // feed the history graph's MAX/AVG lines.
    public float LastGenerationMax;
}

public class RLSnapshot
{
    public int TotalEpisodes;
    public float TrainingTime;

    // Live MAX and AVG of jets' last-life scores (the reward each jet earned from
    // birth to death), over jets that have finished at least one life. Both come
    // from the same quantity, so MAX is just the top of what AVG averages — directly
    // comparable, and shared by the numbers widget and the history graph.
    // (Earlier these accumulated every episode and grew without bound — up for PPO,
    // deep negative for SAC; and the headline "best" was an all-time record that
    // dwarfed the average. Both are fixed.)
    public float CurrentMax;
    public float CurrentAvg;
}
