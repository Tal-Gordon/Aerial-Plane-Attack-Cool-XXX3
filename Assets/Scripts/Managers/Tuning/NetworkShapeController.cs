using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bespoke tuner for a model's hidden-layer shape — the one architecture knob that
/// doesn't fit the flat-float <see cref="ITunableParameters"/> contract because it's a
/// variable-length int[] (see the deferral note in <see cref="ModelHyperparameters"/>).
///
/// Unifies the two ways "network shape" is stored across AI types behind a single
/// editable list of hidden-layer widths:
/// <list type="bullet">
///   <item><b>FixedNeuroEvo</b> — writes <c>NeuroEvoSettings.NetworkShape</c> directly;
///   arbitrary per-layer widths.</item>
///   <item><b>PPO / SAC</b> — ML-Agents only supports uniform layers, so the list is kept
///   uniform (<see cref="IsUniform"/>): every width shares one value and the list maps to
///   <c>HiddenUnits</c> (width) + <c>NumLayers</c> (depth).</item>
///   <item><b>NEAT</b> — evolves its own topology, so there is nothing to edit
///   (<see cref="IsEditable"/> is false; the widget shows a disabled note).</item>
/// </list>
///
/// The input and output layers are fixed (sensor count / action count) and never
/// edited — only the hidden layers between them.
///
/// Changing shape is always a COLD change: saved weights become structurally
/// incompatible, so <see cref="Commit"/> mutates the live settings and then persists +
/// reloads through the supplied handler, intentionally discarding trained progress.
/// </summary>
public class NetworkShapeController
{
    // Shared authoring limits. The input/output layers are fixed and not counted here.
    public const int MinLayerWidth = 1;
    public const int MaxLayerWidth = 1024;

    /// <summary>Fixed NeuroEvo supports a direct input-to-output network. ML-Agents'
    /// LinearEncoder always creates at least one hidden layer, even when num_layers is
    /// zero, so its truthful minimum remains one.</summary>
    public int MinHiddenLayers => IsUniform ? 1 : 0;

    /// <summary>Depth cap, per AI: NeuroEvo has no backend limit (the practical ceiling
    /// is training compute) so it gets a generous 32; ML-Agents keeps 5, matching the
    /// existing "Hidden Layers" hyperparameter dial and ML-Agents guidance.</summary>
    public int MaxHiddenLayers => IsUniform ? 5 : 32;

    private readonly Func<SimulationSettings> settingsProvider;
    private readonly Action onCommitted;

    /// <param name="settingsProvider">Reads the live settings (load swaps the object,
    /// so this is a closure rather than a captured reference — mirrors
    /// <see cref="ModelHyperparameters"/>).</param>
    /// <param name="onCommitted">Persists the mutated settings and reloads the run.</param>
    public NetworkShapeController(Func<SimulationSettings> settingsProvider, Action onCommitted)
    {
        this.settingsProvider = settingsProvider;
        this.onCommitted = onCommitted;
    }

    private SimulationSettings S => settingsProvider?.Invoke();

    // ── Capability / metadata ────────────────────────────────────────

    public bool HasActiveRun => S != null;

    /// <summary>False for NEAT (topology evolves) and when no run is active.</summary>
    public bool IsEditable
    {
        get { var s = S; return s != null && s.AIType != AIType.NEAT; }
    }

    /// <summary>RL can only express layers of equal width, so the list is kept uniform:
    /// a width edit applies to every layer, and only the depth varies.</summary>
    public bool IsUniform
    {
        get { var s = S; return s != null && (s.AIType == AIType.PPO_MLAgents || s.AIType == AIType.SAC_MLAgents); }
    }

    /// <summary>One-line reason the editor is disabled, or null when it's editable.</summary>
    public string DisabledReason
    {
        get
        {
            var s = S;
            if (s == null) return "No active run.";
            if (s.AIType == AIType.NEAT) return "NEAT evolves its own topology — shape isn't configurable.";
            return null;
        }
    }

    /// <summary>Fixed input-layer width (sensor count). Read-only, shown for context.</summary>
    public int InputSize
    {
        get
        {
            var s = S;
            if (s == null) return 0;
            return s.AIType switch
            {
                AIType.FixedNeuroEvo => FirstOr(s.NeuroEvoSettings?.NetworkShape, 0),
                AIType.PPO_MLAgents or AIType.SAC_MLAgents => s.RLSettings?.InputSize ?? 0,
                _ => 0,
            };
        }
    }

    /// <summary>Fixed output-layer width (action count). Read-only, shown for context.</summary>
    public int OutputSize
    {
        get
        {
            var s = S;
            if (s == null) return 0;
            return s.AIType switch
            {
                AIType.FixedNeuroEvo => LastOr(s.NeuroEvoSettings?.NetworkShape, 0),
                AIType.PPO_MLAgents or AIType.SAC_MLAgents => s.RLSettings?.OutputSize ?? 0,
                _ => 0,
            };
        }
    }

    /// <summary>The committed hidden-layer widths (input/output excluded).</summary>
    public List<int> GetHiddenLayers()
    {
        var s = S;
        var hidden = new List<int>();
        if (s == null) return hidden;

        switch (s.AIType)
        {
            case AIType.FixedNeuroEvo:
                int[] shape = s.NeuroEvoSettings?.NetworkShape;
                if (shape != null)
                    for (int i = 1; i < shape.Length - 1; i++) hidden.Add(shape[i]);
                break;

            case AIType.PPO_MLAgents:
            case AIType.SAC_MLAgents:
                var rl = s.RLSettings;
                if (rl != null)
                    for (int i = 0; i < rl.NumLayers; i++) hidden.Add(rl.HiddenUnits);
                break;
        }
        return hidden;
    }

    // ── Commit ───────────────────────────────────────────────────────

    /// <summary>Normalizes the requested hidden layers, writes them into the live
    /// settings, then persists + reloads via the handler. No-op when not editable.</summary>
    public void Commit(IReadOnlyList<int> hiddenLayers)
    {
        var s = S;
        if (s == null || !IsEditable || hiddenLayers == null) return;

        List<int> normalized = Normalize(hiddenLayers);
        if (normalized.Count < MinHiddenLayers) return;

        switch (s.AIType)
        {
            case AIType.FixedNeuroEvo:
                WriteNeuroEvo(s, normalized);
                break;
            case AIType.PPO_MLAgents:
            case AIType.SAC_MLAgents:
                WriteRL(s, normalized);
                break;
        }

        onCommitted?.Invoke();
    }

    /// <summary>Clamp widths and layer count to the shared limits; collapse to a single
    /// shared width when the active AI requires uniform layers. Used by the editor to
    /// compare a staged edit against the committed shape and to sanitize on commit.</summary>
    public List<int> Normalize(IReadOnlyList<int> hiddenLayers)
    {
        var result = new List<int>(hiddenLayers.Count);
        foreach (int w in hiddenLayers)
            result.Add(Mathf.Clamp(w, MinLayerWidth, MaxLayerWidth));

        if (result.Count > MaxHiddenLayers)
            result.RemoveRange(MaxHiddenLayers, result.Count - MaxHiddenLayers);

        // RL: every layer must share one width — snap them all to the first.
        if (IsUniform && result.Count > 0)
        {
            int w = result[0];
            for (int i = 0; i < result.Count; i++) result[i] = w;
        }
        return result;
    }

    private void WriteNeuroEvo(SimulationSettings s, List<int> hidden)
    {
        var evo = s.NeuroEvoSettings;
        if (evo == null) return;

        int input = FirstOr(evo.NetworkShape, InputSize);
        int output = LastOr(evo.NetworkShape, OutputSize);

        var shape = new int[hidden.Count + 2];
        shape[0] = input;
        for (int i = 0; i < hidden.Count; i++) shape[i + 1] = hidden[i];
        shape[^1] = output;
        evo.NetworkShape = shape;
    }

    private static void WriteRL(SimulationSettings s, List<int> hidden)
    {
        var rl = s.RLSettings;
        if (rl == null) return;

        // Normalize/Commit enforce ML-Agents' one-layer minimum, and the list is
        // uniform, so its first entry supplies the shared width.
        rl.NumLayers = hidden.Count;
        rl.HiddenUnits = hidden[0];
    }

    private static int FirstOr(int[] a, int fallback) => a != null && a.Length > 0 ? a[0] : fallback;
    private static int LastOr(int[] a, int fallback) => a != null && a.Length > 0 ? a[^1] : fallback;
}
