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

    private void Update()
    {
        if (PauseMenuController.IsGamePaused || Time.timeScale <= 0f) return;

        float realFPS = 1f / Time.unscaledDeltaTime;

        // Critical threshold: AI physics might break
        if (Time.unscaledDeltaTime >= (Time.maximumDeltaTime * 0.8f))
        {
            if (informativeLabel)
            {
                informativeLabel.color = Color.red;
                informativeLabel.text = "Time Scale (Critical)";
            }
            
            // Auto-correct: Throttle time scale to recover frame time
            float newScale = Mathf.Max(1f, Time.timeScale - 10f * Time.unscaledDeltaTime);
            timeScaleSlider.value = newScale; // Triggers SetTimeScale automatically
        }
        // Warning threshold: hardware bottleneck
        else if (realFPS < 20f)
        {
            if (informativeLabel)
            {
                informativeLabel.color = Color.yellow;
                informativeLabel.text = "Time Scale (Bottleneck)";
            }
        }
        else
        {
            if (informativeLabel)
            {
                informativeLabel.color = new Color32(31, 31, 31, 255);
                informativeLabel.text = "Time Scale";
            }
        }
    }

    public void SetTimeScale(float value)
    {
        Time.timeScale = value;
        
        if (value > 0f)
        {
            // For slow-mo (< 1x), scale down fixedDeltaTime to keep it smooth.
            // For fast-forward (> 1x), cap fixedDeltaTime at 0.02f to ensure accurate physics!
            Time.fixedDeltaTime = Mathf.Min(0.02f, 0.02f * value);
        }
    }
}