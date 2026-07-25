using System;
using System.Collections.Generic;

/// <summary>
/// A complete, restorable snapshot of a training run for one
/// (Track, AIType) slot. Serialized to JSON by <see cref="DataManager"/>.
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

    // The track (scene) this save belongs to. Save slots are keyed by (track, AI
    // type), so two tracks sharing a Mode (e.g. several FlightSchool tracks) each
    // own an independent save. Informational here — the on-disk path is the
    // authority — but it lets a save self-describe which slot produced it.
    public string Track;

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
    public float TrainingElapsedSeconds; // accumulated scaled simulation time across resumes
    public string SavedAtUtc;

    // Optional visual-analysis data. Null on saves created before the stats-dashboard
    // upgrade, so old save files remain fully loadable.
    public TrainingRunHistory RunHistory;
    public ScoreDistributionData ScoreDistribution;

    // ── Engine brain payload (opaque, engine-specific JSON) ──
    public string EngineState;
}

/// <summary>
/// Decimated MAX/AVG samples spanning the full run. Kept in the save rather than in
/// a particular UI widget so history survives scene reloads and training resumes.
/// </summary>
[Serializable]
public class TrainingRunHistory
{
    public List<int> Iterations = new List<int>();
    public List<float> MaxScores = new List<float>();
    public List<float> AverageScores = new List<float>();

    public int Count => Math.Min(
        Iterations?.Count ?? 0,
        Math.Min(MaxScores?.Count ?? 0, AverageScores?.Count ?? 0));

    public void Append(int iteration, float max, float average, int maxSamples = 512)
    {
        if (float.IsNaN(max) || float.IsInfinity(max)
            || float.IsNaN(average) || float.IsInfinity(average))
            return;

        Iterations ??= new List<int>();
        MaxScores ??= new List<float>();
        AverageScores ??= new List<float>();

        int count = Count;
        if (count > 0 && Iterations[count - 1] == iteration)
        {
            MaxScores[count - 1] = max;
            AverageScores[count - 1] = average;
            return;
        }

        Iterations.Add(iteration);
        MaxScores.Add(max);
        AverageScores.Add(average);

        if (Count > Math.Max(8, maxSamples))
            Decimate();
    }

    public TrainingRunHistory Clone()
    {
        int count = Count;
        return new TrainingRunHistory
        {
            Iterations = count > 0 ? Iterations.GetRange(0, count) : new List<int>(),
            MaxScores = count > 0 ? MaxScores.GetRange(0, count) : new List<float>(),
            AverageScores = count > 0 ? AverageScores.GetRange(0, count) : new List<float>(),
        };
    }

    private void Decimate()
    {
        int count = Count;
        int write = 0;
        for (int read = 0; read < count; read += 2, write++)
        {
            Iterations[write] = Iterations[read];
            MaxScores[write] = MaxScores[read];
            AverageScores[write] = AverageScores[read];
        }

        Iterations.RemoveRange(write, Iterations.Count - write);
        MaxScores.RemoveRange(write, MaxScores.Count - write);
        AverageScores.RemoveRange(write, AverageScores.Count - write);
    }
}

/// <summary>
/// Compact population-score histogram captured at save time. Storing bins instead of
/// every individual score keeps large populations from bloating the JSON save.
/// </summary>
[Serializable]
public class ScoreDistributionData
{
    public float Minimum;
    public float Maximum;
    public int SampleCount;
    public int[] Bins;

    public static ScoreDistributionData FromScores(
        IEnumerable<float> scores, int binCount = 12)
    {
        if (scores == null) return null;

        var values = new List<float>();
        foreach (float score in scores)
        {
            if (!float.IsNaN(score) && !float.IsInfinity(score))
                values.Add(score);
        }

        if (values.Count == 0) return null;

        float minimum = values[0];
        float maximum = values[0];
        for (int i = 1; i < values.Count; i++)
        {
            minimum = Math.Min(minimum, values[i]);
            maximum = Math.Max(maximum, values[i]);
        }

        int[] bins = new int[Math.Max(3, binCount)];
        float range = maximum - minimum;
        if (range < 0.000001f)
        {
            bins[bins.Length / 2] = values.Count;
        }
        else
        {
            foreach (float value in values)
            {
                int index = (int)((value - minimum) / range * bins.Length);
                index = Math.Max(0, Math.Min(bins.Length - 1, index));
                bins[index]++;
            }
        }

        return new ScoreDistributionData
        {
            Minimum = minimum,
            Maximum = maximum,
            SampleCount = values.Count,
            Bins = bins,
        };
    }

    public ScoreDistributionData Clone() =>
        new ScoreDistributionData
        {
            Minimum = Minimum,
            Maximum = Maximum,
            SampleCount = SampleCount,
            Bins = Bins != null ? (int[])Bins.Clone() : null,
        };
}
