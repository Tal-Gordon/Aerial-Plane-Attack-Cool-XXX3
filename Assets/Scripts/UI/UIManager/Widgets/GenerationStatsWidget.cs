using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Headline run stats. Each Build*() method only describes WHAT to show by
/// producing an ordered list of caption-over-value stats (+ optional fill bar);
/// a single <see cref="Render"/> applies it, cloning <see cref="StatCell"/> tiles
/// from one prefab as needed and hiding the leftovers. Nothing outside this widget
/// ever writes these labels; the per-paradigm differences below are the whole story.
///
///   Evolution : GEN / BEST / AVG (last gen) / ALIVE a-of-total + fill bar / TIME.
///               Objectives without gradual attrition (MaxAltitude) swap ALIVE
///               for a static POP and drop the bar (TracksAttrition == false).
///   RL (PPO/SAC) : EPISODES / BEST / AVG / AGENTS / TIME — BEST is the all-time
///                  record (like evo's champion); the comparable current MAX vs AVG
///                  lives in the history graph. No alive-bar (episodes reset
///                  individually).
///   Inference : just the run mode + BEST (it's a replay, nothing progresses).
/// </summary>
public class GenerationStatsWidget : UIWidget
{
    [Header("Cell cloning")]
    [SerializeField] private StatCell cellPrefab;     // the tile cloned per stat
    [SerializeField] private RectTransform cellRow;   // parent for the cloned tiles (give it a layout group)

    [Header("Population")]
    [SerializeField] private Image populationFillBar; // toggled on only for attrition runs

    // Pool of cloned tiles, grown on demand and reused frame to frame.
    private readonly List<StatCell> cells = new List<StatCell>();

    protected override void OnInitialize()
    {
        // The serialized bar colours (green fill on a grey track) predate the theme
        // and survive every generic skin pass — pin them to the palette here.
        if (populationFillBar != null)
        {
            populationFillBar.color = UITheme.Accent;
            Image track = populationFillBar.transform.parent != null
                ? populationFillBar.transform.parent.GetComponent<Image>()
                : null;
            if (track != null) track.color = UITheme.FieldPressed;
        }
    }

    // One stat's content. Empty Value = not shown.
    private struct Stat
    {
        public string Caption;
        public string Value;
        public bool Shown => !string.IsNullOrEmpty(Value);
        public Stat(string caption, string value) { Caption = caption; Value = value; }
    }

    // A paradigm-agnostic description of one frame. Built by Build*, applied by Render.
    private struct StatsView
    {
        public Stat Iteration;
        public Stat Best;
        public Stat Avg;
        public Stat Population;
        public Stat Time;
        public bool ShowBar;
        public float BarFill;
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        if (snapshot == null) return;

        bool isInference = snapshot.ParadigmName != null &&
                           snapshot.ParadigmName.StartsWith("Inference");

        StatsView view;
        if (isInference)                  view = BuildInference(snapshot);
        else if (snapshot.RLData != null) view = BuildRL(snapshot);
        else                              view = BuildEvolution(snapshot);

        Render(view);
    }

    // ── Evolutionary: generations of a population that thins out as jets crash ──
    private StatsView BuildEvolution(SimulationSnapshot snapshot)
    {
        int total = snapshot.Population != null ? snapshot.Population.Count : 0;

        // Stable mean of the last completed generation (captured by the paradigm),
        // not a frame-by-frame live average that would just climb from zero.
        float avgFitness = snapshot.EvoData != null ? snapshot.EvoData.LastGenerationAverage : 0f;

        var view = new StatsView
        {
            Iteration = new Stat("Gen", $"{snapshot.IterationNumber}"),
            Best      = new Stat("Best", FormatFitness(snapshot.ChampionScore)),
            Avg       = new Stat("Avg", FormatFitness(avgFitness)),
            Time      = new Stat("Time", FormatTime(snapshot.ElapsedTime)),
        };

        if (snapshot.TracksAttrition)
        {
            // Jets crash at different times → a meaningful, live alive-fraction.
            view.Population = new Stat("Alive", $"{snapshot.AgentsAlive} / {total}");
            view.ShowBar = true;
            view.BarFill = total > 0 ? (float)snapshot.AgentsAlive / total : 0f;
        }
        else
        {
            // No gradual attrition (e.g. MaxAltitude: all jets end on one shared
            // time limit), so "alive" carries no info — show the static pop size
            // and drop the fill bar entirely.
            view.Population = new Stat("Pop", $"{total}");
            view.ShowBar = false;
        }

        return view;
    }

    // ── Reinforcement learning: episodes, not generations; no population die-off ──
    private StatsView BuildRL(SimulationSnapshot snapshot)
    {
        RLSnapshot rl = snapshot.RLData;
        int agents = snapshot.Population != null ? snapshot.Population.Count : 0;

        // BEST = all-time record (same ChampionScore field evo's BEST uses). The
        // comparable current MAX vs AVG lives in the history graph instead.
        return new StatsView
        {
            Iteration  = new Stat("Episodes", $"{rl.TotalEpisodes}"),
            Best       = new Stat("Best", FormatFitness(snapshot.ChampionScore)),
            Avg        = new Stat("Avg", FormatFitness(rl.CurrentAvg)),
            Population = new Stat("Agents", $"{agents}"),
            Time       = new Stat("Time", FormatTime(snapshot.ElapsedTime)),
            // RL agents don't progressively die within a "generation", so an
            // alive-fraction bar would sit pinned at full — hide it.
            ShowBar = false,
        };
    }

    // ── Inference replay: nothing is learning or progressing, keep it minimal ──
    private StatsView BuildInference(SimulationSnapshot snapshot)
    {
        return new StatsView
        {
            Iteration = new Stat("Mode", snapshot.ParadigmName),      // e.g. "Inference (PPO)"
            Best      = new Stat("Best", FormatFitness(snapshot.ChampionScore)),
            // Everything else left default → hidden.
        };
    }

    // ── The single place that touches the UI ─────────────────────────────────────

    private void Render(StatsView view)
    {
        // Fixed display order; hidden stats are simply skipped so cells stay packed.
        Stat[] stats = { view.Iteration, view.Best, view.Avg, view.Population, view.Time };

        int used = 0;
        foreach (Stat stat in stats)
        {
            if (!stat.Shown) continue;
            GetCell(used++).Set(stat.Caption, stat.Value);
        }

        // Park any tiles beyond what this frame needs.
        for (int i = used; i < cells.Count; i++)
            SetActive(cells[i].gameObject, false);

        SetBar(view.ShowBar, view.BarFill);
    }

    // Returns tile `index`, cloning a new one from the prefab if the pool is short.
    private StatCell GetCell(int index)
    {
        while (cells.Count <= index)
        {
            StatCell newCell = Instantiate(cellPrefab, cellRow);
            UITheme.Skin(newCell.gameObject); // pool grows at runtime — theme each clone
            cells.Add(newCell);
        }

        StatCell cell = cells[index];
        SetActive(cell.gameObject, true);
        return cell;
    }

    private void SetBar(bool show, float fill)
    {
        if (!populationFillBar) return;
        SetActive(populationFillBar.gameObject, show);
        if (show) populationFillBar.fillAmount = Mathf.Clamp01(fill);
    }

    private void SetActive(GameObject go, bool active)
    {
        if (go.activeSelf != active) go.SetActive(active);
    }

    // ── Formatting ───────────────────────────────────────────────────────────────

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
