using System;
using System.Collections.Generic;

/// <summary>
/// Reusable, category-agnostic hub that sits on top of an
/// <see cref="ITunableParameters"/> source and adds:
///   • a staging buffer    — edits are collected, not applied on the fly,
///   • an observer event    — <see cref="OnStateChanged"/> fires on every change,
///   • a deferred commit    — staged edits are applied via a supplied delegate.
///
/// One instance per parameter group (reward params today, model hyperparameters
/// later) — each keeps its own staging buffer, event, and commit behaviour while
/// sharing this machinery. UI talks to the tuner; it never touches the source or
/// the commit pipeline directly.
/// </summary>
public class ParameterTuner
{
    private ITunableParameters source;
    private readonly Action<Dictionary<string, float>> commitHandler;
    private readonly Dictionary<string, float> pending = new Dictionary<string, float>();

    /// <summary>Fires whenever the staged set changes (stage / discard / commit / rebind).
    /// Subscribers (a future dials window) refresh their values and toggle a commit
    /// affordance off <see cref="HasPendingChanges"/>.</summary>
    public event Action OnStateChanged;

    /// <param name="source">The parameter group being tuned.</param>
    /// <param name="commitHandler">Applies the staged values when the user commits.
    /// Receives a private copy of the pending edits.</param>
    public ParameterTuner(ITunableParameters source, Action<Dictionary<string, float>> commitHandler)
    {
        this.source = source;
        this.commitHandler = commitHandler;
    }

    /// <summary>Swaps the underlying source (e.g. if the objective is replaced).
    /// Clears any pending edits so they can't apply to a different source.</summary>
    public void SetSource(ITunableParameters newSource)
    {
        source = newSource;
        pending.Clear();
        OnStateChanged?.Invoke();
    }

    // ── Read ─────────────────────────────────────────────────────────

    public IReadOnlyList<ParameterDescriptor> Descriptors =>
        source != null ? source.GetParameterDescriptors() : Array.Empty<ParameterDescriptor>();

    /// <summary>The source's current (committed) values.</summary>
    public Dictionary<string, float> GetLiveValues() =>
        source != null ? source.GetParameters() : new Dictionary<string, float>();

    public float GetLiveValue(string key) =>
        GetLiveValues().TryGetValue(key, out float v) ? v : 0f;

    /// <summary>The value the UI should display: the staged edit if one exists,
    /// otherwise the live value.</summary>
    public float GetEffectiveValue(string key) =>
        pending.TryGetValue(key, out float staged) ? staged : GetLiveValue(key);

    public bool HasPendingChanges => pending.Count > 0;

    public IReadOnlyDictionary<string, float> Pending => pending;

    // ── Staging ──────────────────────────────────────────────────────

    /// <summary>Records an edit without applying it. If the value matches the
    /// current live value the edit is dropped, so <see cref="HasPendingChanges"/>
    /// only reports genuine differences.</summary>
    public void Stage(string key, float value)
    {
        if (source == null) return;

        bool changed;
        if (Mathf_Approximately(value, GetLiveValue(key)))
            changed = pending.Remove(key);
        else
        {
            bool existed = pending.TryGetValue(key, out float prev);
            pending[key] = value;
            changed = !existed || prev != value;
        }

        if (changed) OnStateChanged?.Invoke();
    }

    /// <summary>Drops all staged edits; effective values revert to live.</summary>
    public void Discard()
    {
        if (pending.Count == 0) return;
        pending.Clear();
        OnStateChanged?.Invoke();
    }

    /// <summary>Applies the staged edits through the commit delegate, then clears
    /// the buffer. No-op when nothing is staged.</summary>
    public void Commit()
    {
        if (pending.Count == 0 || commitHandler == null) return;

        var toApply = new Dictionary<string, float>(pending);
        pending.Clear();
        commitHandler.Invoke(toApply);
        OnStateChanged?.Invoke();
    }

    // UnityEngine.Mathf.Approximately without forcing a UnityEngine using here;
    // a plain epsilon compare is enough for parameter equality.
    private static bool Mathf_Approximately(float a, float b) =>
        Math.Abs(a - b) < 1e-6f;
}
