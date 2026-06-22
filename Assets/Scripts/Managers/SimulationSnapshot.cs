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
}

public class RLSnapshot
{
    public int TotalEpisodes;
    public float TrainingTime;

    // Per-EPISODE figures, not running totals across the whole run. BestEpisodeScore
    // is the best single-episode reward seen; LastEpisodeScores[i] is agent i's most
    // recent completed-episode reward. (These used to accumulate every episode, which
    // made them grow/shrink without bound — up for PPO, deep negative for SAC.)
    public float BestEpisodeScore;
    public float[] LastEpisodeScores;
}
