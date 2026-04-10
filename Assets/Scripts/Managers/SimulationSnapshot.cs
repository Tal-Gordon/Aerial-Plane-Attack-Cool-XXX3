using System.Collections.Generic;
using UnityEngine;

public class SimulationSnapshot
{
    // ── Generic (filled by every paradigm) ───────────────────────────
    public int IterationNumber;          // generation for evo, episode for RL
    public int AgentsAlive;
    public float AllTimeBestScore;
    public string ParadigmName;
    public List<JetAgent> Population;

    // ── Filled by SimulationManager, not paradigm ────────────────────
    public float TimeScale;
    public JetAgent SelectedAgent;

    // ── Paradigm-specific sub-snapshots (null if irrelevant) ─────────
    public EvoSnapshot EvoData;
    public RLSnapshot RLData;
}

public class EvoSnapshot
{
    public JetAgent TopAgent;
    public float MutationRate;
    public float Lambda;
    public int CurrentGeneration;
}

public class RLSnapshot
{
    public float AverageEpisodeReward;
    public int EpisodeCount;
}
