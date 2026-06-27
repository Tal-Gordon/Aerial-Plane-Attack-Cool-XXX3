using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Adapts the active <see cref="SimulationSettings"/> to <see cref="ITunableParameters"/>
/// so the model's hyperparameters become a second tuner instance alongside the
/// reward params (see <see cref="ParameterTuners"/>). Unlike the objectives —
/// which implement the contract directly — settings are plain data, and the set
/// of relevant knobs depends on the active <see cref="AIType"/>, so this sits in
/// front of them.
///
/// The source settings object is fetched through a provider because the manager
/// swaps it on load (LoadState reassigns its settings field); reading through the
/// provider keeps this adapter valid across that swap without re-binding.
///
/// Each descriptor carries <see cref="ParameterDescriptor.RequiresReset"/>:
///   • false ("hot")  — adopted via a save→load round-trip that keeps trained
///                       state (e.g. learning rate, mutation probabilities).
///   • true  ("cold") — makes saved weights structurally incompatible, so the
///                       run must be rebuilt from scratch (population size,
///                       network architecture). The commit owner routes on this.
///
/// To add a knob: add the field to the relevant *Settings class, add a descriptor
/// to the matching array below, and read/write it in GetParameters/SetParameters.
/// </summary>
public class ModelHyperparameters : ITunableParameters
{
    private readonly System.Func<SimulationSettings> settingsProvider;

    public ModelHyperparameters(System.Func<SimulationSettings> settingsProvider)
    {
        this.settingsProvider = settingsProvider;
    }

    private SimulationSettings S => settingsProvider?.Invoke();

    // ── Descriptors (immutable per AI type — built once and shared) ──────

    // Universal across every AI type. Population size is cold everywhere: the
    // saved population/agent layout can't be restored at a different size.
    private static readonly ParameterDescriptor PopulationSizeDesc =
        new("populationSize", "Population Size", 1f, 5000f, 1000f, requiresReset: true);

    private static readonly ParameterDescriptor[] FixedNeuroEvoDescriptors =
    {
        PopulationSizeDesc,
        new("mutationRate", "Mutation Rate", 0f, 1f, 0.1f),
        // NOTE: NetworkShape (hidden layer count + sizes) is also a cold knob but
        // is a variable-length int[] that doesn't fit this flat float contract.
        // It's intentionally deferred until its bespoke control is designed — see
        // CLAUDE.md "Runtime parameter tuning".
    };

    private static readonly ParameterDescriptor[] NeatDescriptors =
    {
        PopulationSizeDesc,
        // All hot: BuildScaffolding re-reads these on the load round-trip.
        new("specieCount", "Species Count", 1f, 50f, 10f),
        new("elitismProportion", "Elitism Proportion", 0f, 1f, 0.2f),
        new("selectionProportion", "Selection Proportion", 0f, 1f, 0.4f),
        new("addNodeMutationProbability", "Add-Node Mutation", 0f, 1f, 0.02f),
        new("addConnectionMutationProbability", "Add-Connection Mutation", 0f, 1f, 0.05f),
        new("deleteConnectionMutationProbability", "Delete-Connection Mutation", 0f, 1f, 0.02f),
        new("connectionWeightMutationProbability", "Weight Mutation", 0f, 1f, 0.96f),
    };

    private static readonly ParameterDescriptor[] PpoDescriptors =
    {
        PopulationSizeDesc,
        // Cold: architecture — the .pt checkpoint can't be resumed against a
        // different network, so changing these forces a fresh run.
        new("hiddenUnits", "Hidden Units", 8f, 1024f, 256f, requiresReset: true),
        new("numLayers", "Hidden Layers", 1f, 5f, 2f, requiresReset: true),
        new("normalize", "Normalize Inputs", 0f, 1f, 1f, requiresReset: true, isToggle: true),
        // Hot: regenerated into the YAML every StartTrainer and adopted on --resume.
        new("learningRate", "Learning Rate", 1e-5f, 1e-2f, 3e-4f),
        new("batchSize", "Batch Size", 32f, 16384f, 4096f),
        new("bufferSize", "Buffer Size", 256f, 1048576f, 20480f),
        new("beta", "Entropy (beta)", 0f, 0.1f, 5e-3f),
        new("epsilon", "Clip (epsilon)", 0.05f, 0.5f, 0.2f),
        new("lambd", "GAE (lambda)", 0.5f, 1f, 0.95f),
        new("numEpoch", "Epochs", 1f, 10f, 2f),
        new("gamma", "Discount (gamma)", 0.8f, 0.999f, 0.99f),
        new("timeHorizon", "Time Horizon", 16f, 2048f, 128f),
        new("maxSteps", "Max Steps", 10000f, 50000000f, 5000000f),
        new("decisionPeriod", "Decision Period", 1f, 20f, 5f),
        // NOTE: trainingTimeScale is intentionally NOT exposed as a dial — it's a
        // headless-training speed knob, not a model hyperparameter. It still
        // round-trips in saves via Read/WriteRL below.
    };

    private static readonly ParameterDescriptor[] SacDescriptors =
    {
        PopulationSizeDesc,
        new("hiddenUnits", "Hidden Units", 8f, 1024f, 256f, requiresReset: true),
        new("numLayers", "Hidden Layers", 1f, 5f, 2f, requiresReset: true),
        new("normalize", "Normalize Inputs", 0f, 1f, 1f, requiresReset: true, isToggle: true),
        new("learningRate", "Learning Rate", 1e-5f, 1e-2f, 3e-4f),
        new("batchSize", "Batch Size", 32f, 16384f, 4096f),
        new("bufferSize", "Buffer Size", 256f, 1048576f, 20480f),
        new("tau", "Target Smoothing (tau)", 1e-4f, 0.05f, 5e-3f),
        new("stepsPerUpdate", "Steps Per Update", 1f, 100f, 10f),
        new("initEntCoef", "Initial Entropy Coef", 0f, 2f, 1f),
        new("bufferInitSteps", "Buffer Init Steps", 0f, 100000f, 0f),
        new("gamma", "Discount (gamma)", 0.8f, 0.999f, 0.99f),
        new("timeHorizon", "Time Horizon", 16f, 2048f, 128f),
        new("maxSteps", "Max Steps", 10000f, 50000000f, 5000000f),
        new("decisionPeriod", "Decision Period", 1f, 20f, 5f),
        // trainingTimeScale intentionally not exposed — see PpoDescriptors note.
    };

    private static readonly ParameterDescriptor[] Empty = new ParameterDescriptor[0];

    private static ParameterDescriptor[] DescriptorsFor(AIType type) => type switch
    {
        AIType.FixedNeuroEvo => FixedNeuroEvoDescriptors,
        AIType.NEAT          => NeatDescriptors,
        AIType.PPO_MLAgents  => PpoDescriptors,
        AIType.SAC_MLAgents  => SacDescriptors,
        _                    => Empty,
    };

    public IReadOnlyList<ParameterDescriptor> GetParameterDescriptors() =>
        S != null ? DescriptorsFor(S.AIType) : Empty;

    // ── Read ─────────────────────────────────────────────────────────

    public Dictionary<string, float> GetParameters()
    {
        var values = new Dictionary<string, float>();
        var s = S;
        if (s == null) return values;

        values["populationSize"] = s.PopulationSize;

        switch (s.AIType)
        {
            case AIType.FixedNeuroEvo:
                if (s.NeuroEvoSettings != null)
                    values["mutationRate"] = s.NeuroEvoSettings.MutationRate;
                break;

            case AIType.NEAT:
                var n = s.NeatSettings;
                if (n != null)
                {
                    values["specieCount"] = n.SpecieCount;
                    values["elitismProportion"] = n.ElitismProportion;
                    values["selectionProportion"] = n.SelectionProportion;
                    values["addNodeMutationProbability"] = n.AddNodeMutationProbability;
                    values["addConnectionMutationProbability"] = n.AddConnectionMutationProbability;
                    values["deleteConnectionMutationProbability"] = n.DeleteConnectionMutationProbability;
                    values["connectionWeightMutationProbability"] = n.ConnectionWeightMutationProbability;
                }
                break;

            case AIType.PPO_MLAgents:
            case AIType.SAC_MLAgents:
                ReadRL(s.RLSettings, s.AIType, values);
                break;
        }

        return values;
    }

    private static void ReadRL(RLSettings rl, AIType type, Dictionary<string, float> values)
    {
        if (rl == null) return;

        values["hiddenUnits"] = rl.HiddenUnits;
        values["numLayers"] = rl.NumLayers;
        values["normalize"] = rl.Normalize ? 1f : 0f;
        values["learningRate"] = rl.LearningRate;
        values["batchSize"] = rl.BatchSize;
        values["bufferSize"] = rl.BufferSize;
        values["gamma"] = rl.Gamma;
        values["timeHorizon"] = rl.TimeHorizon;
        values["maxSteps"] = rl.MaxSteps;
        values["decisionPeriod"] = rl.DecisionPeriod;
        values["trainingTimeScale"] = rl.TrainingTimeScale;

        if (type == AIType.SAC_MLAgents)
        {
            values["tau"] = rl.Tau;
            values["stepsPerUpdate"] = rl.StepsPerUpdate;
            values["initEntCoef"] = rl.InitEntCoef;
            values["bufferInitSteps"] = rl.BufferInitSteps;
        }
        else // PPO
        {
            values["beta"] = rl.Beta;
            values["epsilon"] = rl.Epsilon;
            values["lambd"] = rl.Lambd;
            values["numEpoch"] = rl.NumEpoch;
        }
    }

    // ── Write ────────────────────────────────────────────────────────

    public void SetParameters(Dictionary<string, float> parameters)
    {
        if (parameters == null) return;
        var s = S;
        if (s == null) return;

        if (parameters.TryGetValue("populationSize", out float pop))
            s.PopulationSize = Mathf.Max(1, Mathf.RoundToInt(pop));

        switch (s.AIType)
        {
            case AIType.FixedNeuroEvo:
                if (s.NeuroEvoSettings != null &&
                    parameters.TryGetValue("mutationRate", out float mr))
                    s.NeuroEvoSettings.MutationRate = mr;
                break;

            case AIType.NEAT:
                var n = s.NeatSettings;
                if (n != null)
                {
                    if (parameters.TryGetValue("specieCount", out float sc)) n.SpecieCount = Mathf.Max(1, Mathf.RoundToInt(sc));
                    if (parameters.TryGetValue("elitismProportion", out float ep)) n.ElitismProportion = ep;
                    if (parameters.TryGetValue("selectionProportion", out float sp)) n.SelectionProportion = sp;
                    if (parameters.TryGetValue("addNodeMutationProbability", out float an)) n.AddNodeMutationProbability = an;
                    if (parameters.TryGetValue("addConnectionMutationProbability", out float ac)) n.AddConnectionMutationProbability = ac;
                    if (parameters.TryGetValue("deleteConnectionMutationProbability", out float dc)) n.DeleteConnectionMutationProbability = dc;
                    if (parameters.TryGetValue("connectionWeightMutationProbability", out float cw)) n.ConnectionWeightMutationProbability = cw;
                }
                break;

            case AIType.PPO_MLAgents:
            case AIType.SAC_MLAgents:
                WriteRL(s.RLSettings, s.AIType, parameters);
                break;
        }
    }

    private static void WriteRL(RLSettings rl, AIType type, Dictionary<string, float> p)
    {
        if (rl == null) return;

        if (p.TryGetValue("hiddenUnits", out float hu)) rl.HiddenUnits = Mathf.Max(1, Mathf.RoundToInt(hu));
        if (p.TryGetValue("numLayers", out float nl)) rl.NumLayers = Mathf.Max(1, Mathf.RoundToInt(nl));
        if (p.TryGetValue("normalize", out float nm)) rl.Normalize = nm >= 0.5f;
        if (p.TryGetValue("learningRate", out float lr)) rl.LearningRate = lr;
        if (p.TryGetValue("batchSize", out float bs)) rl.BatchSize = Mathf.Max(1, Mathf.RoundToInt(bs));
        if (p.TryGetValue("bufferSize", out float bf)) rl.BufferSize = Mathf.Max(1, Mathf.RoundToInt(bf));
        if (p.TryGetValue("gamma", out float ga)) rl.Gamma = ga;
        if (p.TryGetValue("timeHorizon", out float th)) rl.TimeHorizon = Mathf.Max(1, Mathf.RoundToInt(th));
        if (p.TryGetValue("maxSteps", out float ms)) rl.MaxSteps = Mathf.Max(1, Mathf.RoundToInt(ms));
        if (p.TryGetValue("decisionPeriod", out float dp)) rl.DecisionPeriod = Mathf.Max(1, Mathf.RoundToInt(dp));
        if (p.TryGetValue("trainingTimeScale", out float ts)) rl.TrainingTimeScale = Mathf.Max(0.01f, ts);

        if (type == AIType.SAC_MLAgents)
        {
            if (p.TryGetValue("tau", out float tau)) rl.Tau = tau;
            if (p.TryGetValue("stepsPerUpdate", out float su)) rl.StepsPerUpdate = su;
            if (p.TryGetValue("initEntCoef", out float ie)) rl.InitEntCoef = ie;
            if (p.TryGetValue("bufferInitSteps", out float bi)) rl.BufferInitSteps = Mathf.Max(0, Mathf.RoundToInt(bi));
        }
        else // PPO
        {
            if (p.TryGetValue("beta", out float beta)) rl.Beta = beta;
            if (p.TryGetValue("epsilon", out float eps)) rl.Epsilon = eps;
            if (p.TryGetValue("lambd", out float lambd)) rl.Lambd = lambd;
            if (p.TryGetValue("numEpoch", out float ne)) rl.NumEpoch = Mathf.Max(1, Mathf.RoundToInt(ne));
        }
    }
}
