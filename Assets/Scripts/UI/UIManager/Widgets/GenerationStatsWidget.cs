using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GenerationStatsWidget : UIWidget
{
    [Header("Generation")]
    [SerializeField] private TextMeshProUGUI generationLabel;

    [Header("Fitness")]
    [SerializeField] private TextMeshProUGUI avgFitnessLabel;
    [SerializeField] private TextMeshProUGUI topFitnessLabel;

    [Header("Population")]
    [SerializeField] private TextMeshProUGUI aliveLabel;
    [SerializeField] private TextMeshProUGUI deadLabel;
    [SerializeField] private Image populationFillBar;


    public override void Tick(SimulationSnapshot snapshot)
    {
        if (generationLabel)
            generationLabel.text = $"GEN: {snapshot.CurrentGeneration}";

        if (aliveLabel)
            aliveLabel.text = $"ALIVE: {snapshot.AliveCount}";

        if (snapshot.Population != null)
        {
            int total = snapshot.Population.Count;
            int dead  = total - snapshot.AliveCount;

            if (deadLabel)
                deadLabel.text = $"DEAD: {dead}";

            if (populationFillBar != null)
            {
                float alivePercentage = (float)snapshot.AliveCount / total;
                populationFillBar.fillAmount = alivePercentage;
            }

            if (avgFitnessLabel && total > 0 && snapshot.AliveCount == 0)
            {
                float sum = 0f;

                foreach (var agent in snapshot.Population)
                {
                    sum += agent.CurrentFitness;
                }
                float avg = sum / total;

                avgFitnessLabel.text = $"AVG FITNESS: {FormatFitness(avg)}";
            }

            if (topFitnessLabel && snapshot.TopAgent != null) {
                topFitnessLabel.text = $"TOP: {FormatFitness(snapshot.TopAgent.CurrentFitness)}";
            }
        }
    }

    // Formats large numbers readably: 1200 -> "1.2k", 1500000 -> "1.5M"
    private string FormatFitness(float value)
    {
        if (value >= 1_000_000f) return $"{value / 1_000_000f:F1}M";
        if (value >= 1_000f)     return $"{value / 1_000f:F1}k";
        return $"{value:F0}";
    }
}