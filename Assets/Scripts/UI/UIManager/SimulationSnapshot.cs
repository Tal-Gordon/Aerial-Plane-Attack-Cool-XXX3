using System.Collections.Generic;
using UnityEngine;

public class SimulationSnapshot
{
    // World
    public int CurrentGeneration;
    public int AliveCount;
    public float TimeScale;

    // Genetic Algorithm
    public float MutationRate;
    public float Lambda;

    // Population
    public List<JetAgent> Population; // sorted by fitness already
    public JetAgent TopAgent;
    public JetAgent SelectedAgent;
}