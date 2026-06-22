using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Headline run stats. The same five label slots are reused across every
/// paradigm — what they show (and whether they show at all) adapts to whatever
/// the active paradigm actually fills into the snapshot, so nothing displays a
/// stat that is meaningless or stale for the current AI type.
///
///   Evolution : GEN / BEST / AVG (last gen) / ALIVE a‑of‑total + fill bar.
///               Objectives without gradual attrition (MaxAltitude) swap ALIVE
///               for a static POP and drop the bar (TracksAttrition == false).
///   RL (PPO/SAC) : EPISODES / BEST / AVG / AGENTS / TIME — BEST is the all-time
///                  record (like evo's champion); the comparable current MAX vs AVG
///                  lives in the history graph. No alive‑bar (episodes reset
///                  individually).
///   Inference : just the run label + BEST (it's a replay, nothing progresses)
/// </summary>
public class GenerationStatsWidget : UIWidget
{
    [Header("Generation")]
    [SerializeField] private TextMeshProUGUI generationLabel;

    [Header("Fitness")]
    [SerializeField] private TextMeshProUGUI avgFitnessLabel;
    [SerializeField] private TextMeshProUGUI topFitnessLabel;

    [Header("Population")]
    [SerializeField] private TextMeshProUGUI aliveLabel;
    [SerializeField] private TextMeshProUGUI deadLabel; // repurposed: TIME for RL, hidden for evo
    [SerializeField] private Image populationFillBar;

    public override void Tick(SimulationSnapshot snapshot)
    {
        if (snapshot == null) return;

        bool isInference = snapshot.ParadigmName != null &&
                           snapshot.ParadigmName.StartsWith("Inference");

        if (isInference)      TickInference(snapshot);
        else if (snapshot.RLData != null) TickRL(snapshot);
        else                  TickEvolution(snapshot);
    }

    // ── Evolutionary: generations of a population that thins out as jets crash ──
    private void TickEvolution(SimulationSnapshot snapshot)
    {
        int total = snapshot.Population != null ? snapshot.Population.Count : 0;

        Show(generationLabel, $"GEN: {snapshot.IterationNumber}");
        Show(topFitnessLabel, $"BEST: {FormatFitness(snapshot.ChampionScore)}");

        // Stable mean of the last completed generation (captured by the paradigm),
        // not a frame-by-frame live average that would just climb from zero.
        float avg = snapshot.EvoData != null ? snapshot.EvoData.LastGenerationAverage : 0f;
        Show(avgFitnessLabel, $"AVG: {FormatFitness(avg)}");

        // DEAD is just (total − alive) — folded into the ALIVE line, so it's hidden.
        Hide(deadLabel);

        if (snapshot.TracksAttrition)
        {
            // Jets crash at different times → a meaningful, live alive-fraction.
            Show(aliveLabel, $"ALIVE: {snapshot.AgentsAlive} / {total}");
            if (populationFillBar)
            {
                SetActive(populationFillBar.gameObject, true);
                populationFillBar.fillAmount = total > 0 ? (float)snapshot.AgentsAlive / total : 0f;
            }
        }
        else
        {
            // No gradual attrition (e.g. MaxAltitude: all jets end on one shared
            // time limit), so "alive" carries no info — show the static pop size
            // and drop the fill bar entirely.
            Show(aliveLabel, $"POP: {total}");
            if (populationFillBar) SetActive(populationFillBar.gameObject, false);
        }
    }

    // ── Reinforcement learning: episodes, not generations; no population die‑off ──
    private void TickRL(SimulationSnapshot snapshot)
    {
        RLSnapshot rl = snapshot.RLData;
        int agents = snapshot.Population != null ? snapshot.Population.Count : 0;

        Show(generationLabel, $"EPISODES: {rl.TotalEpisodes}");
        // BEST = all-time record (same ChampionScore field evo's BEST uses). The
        // comparable current MAX vs AVG lives in the history graph instead.
        Show(topFitnessLabel, $"BEST: {FormatFitness(snapshot.ChampionScore)}");
        Show(avgFitnessLabel, $"AVG: {FormatFitness(rl.CurrentAvg)}");
        Show(aliveLabel, $"AGENTS: {agents}");
        Show(deadLabel, $"TIME: {FormatTime(rl.TrainingTime)}");

        // RL agents don't progressively die within a "generation", so an
        // alive‑fraction bar would sit pinned at full — hide it.
        if (populationFillBar) SetActive(populationFillBar.gameObject, false);
    }

    // ── Inference replay: nothing is learning or progressing, keep it minimal ──
    private void TickInference(SimulationSnapshot snapshot)
    {
        Show(generationLabel, snapshot.ParadigmName);           // e.g. "Inference (PPO)"
        Show(topFitnessLabel, $"BEST: {FormatFitness(snapshot.ChampionScore)}");

        Hide(avgFitnessLabel);
        Hide(aliveLabel);
        Hide(deadLabel);
        if (populationFillBar) SetActive(populationFillBar.gameObject, false);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void Show(TextMeshProUGUI label, string text)
    {
        if (!label) return;
        SetActive(label.gameObject, true);
        label.text = text;
    }

    private void Hide(TextMeshProUGUI label)
    {
        if (label) SetActive(label.gameObject, false);
    }

    private void SetActive(GameObject go, bool active)
    {
        if (go.activeSelf != active) go.SetActive(active);
    }

    // 1200 -> "1.2k", 1500000 -> "1.5M"; keeps negatives readable too.
    private string FormatFitness(float value)
    {
        float mag = Mathf.Abs(value);
        if (mag >= 1_000_000f) return $"{value / 1_000_000f:F1}M";
        if (mag >= 1_000f)     return $"{value / 1_000f:F1}k";
        return $"{value:F0}";
    }

    private string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }
}
