using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeScaleWidget : UIWidget
{
    [SerializeField] private Slider timeScaleSlider;
    [SerializeField] private TextMeshProUGUI valueLabel;

    protected override void OnInitialize()
    {
        timeScaleSlider.minValue = 0f;
        timeScaleSlider.maxValue = 10f;
        timeScaleSlider.onValueChanged.AddListener(SetTimeScale);
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        // Sync slider to actual timescale without triggering the listener
        timeScaleSlider.SetValueWithoutNotify(snapshot.TimeScale);
        if (valueLabel) valueLabel.text = $"{snapshot.TimeScale:F1}x";
    }

    public void SetTimeScale(float value)
    {
        Time.timeScale = value;
        
        // Optional: Adjust fixedDeltaTime to keep physics smooth
        // This prevents "stuttering" during slow-mo
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }
}