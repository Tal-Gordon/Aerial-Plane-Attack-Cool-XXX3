using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeScaleWidget : UIWidget
{
    [SerializeField] private TextMeshProUGUI informativeLabel; // just a label saying "Time Scale"
    [SerializeField] private Slider timeScaleSlider;
    [SerializeField] private TextMeshProUGUI valueLabel;

    private float lastDisplayedScale = -1f;

    protected override void OnInitialize()
    {
        informativeLabel.text = "Time Scale";
        timeScaleSlider.minValue = 0.02f;
        timeScaleSlider.maxValue = 10f;
        timeScaleSlider.onValueChanged.AddListener(SetTimeScale);
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        // Sync slider to actual timescale without triggering the listener
        timeScaleSlider.SetValueWithoutNotify(snapshot.TimeScale);
        
        if (snapshot.TimeScale != lastDisplayedScale)
        {
            lastDisplayedScale = snapshot.TimeScale;
            if (valueLabel) valueLabel.text = $"{snapshot.TimeScale:F1}x";
        }
    }

    public void SetTimeScale(float value)
    {
        Time.timeScale = value;
        
        // Optional: Adjust fixedDeltaTime to keep physics smooth
        // This prevents "stuttering" during slow-mo
        if (value > 0f)
        {
            Time.fixedDeltaTime = 0.02f * value;
        }
    }
}