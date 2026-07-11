using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TimeScaleWidget : UIWidget
{
    [SerializeField] private TextMeshProUGUI informativeLabel; // just a label saying "Time Scale"
    [SerializeField] private Slider timeScaleSlider;
    [SerializeField] private TextMeshProUGUI valueLabel;

    private float lastDisplayedScale = -1f;
    private float preThrottleScale = -1f;
    private int goodFramesSinceThrottle = 0;
    private const int RecoveryFrames = 3;

    protected override void OnInitialize()
    {
        informativeLabel.text = "Time Scale";
        timeScaleSlider.minValue = 0.02f;
        timeScaleSlider.maxValue = 10f;
        timeScaleSlider.value = 1f;
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

            // Save the scale we were at before throttling (skip startup — stale slider value)
            if (preThrottleScale < 0f && Time.unscaledTime > 1f)
                preThrottleScale = Time.timeScale;

            goodFramesSinceThrottle = 0;

            // Auto-correct: Throttle time scale to recover frame time
            float newScale = Mathf.Max(1f, Time.timeScale - 10f * Time.unscaledDeltaTime);
            timeScaleSlider.value = newScale; // Triggers SetTimeScale automatically
        }
        // Warning threshold: hardware bottleneck
        else if (realFPS < 20f)
        {
            preThrottleScale = -1f;
            goodFramesSinceThrottle = 0;
            if (informativeLabel)
            {
                informativeLabel.color = Color.yellow;
                informativeLabel.text = "Time Scale (Bottleneck)";
            }
        }
        else
        {
            // Restore timescale after a transient spike (e.g. ML-Agents training update)
            if (preThrottleScale > 0f)
            {
                goodFramesSinceThrottle++;
                if (goodFramesSinceThrottle >= RecoveryFrames)
                {
                    timeScaleSlider.value = preThrottleScale;
                    preThrottleScale = -1f;
                    goodFramesSinceThrottle = 0;
                }
            }

            if (informativeLabel)
            {
                informativeLabel.color = UITheme.TextColor;
                informativeLabel.text = "Time Scale";
            }
        }
    }

    public void SetTimeScale(float value)
    {
        Time.timeScale = value;

        if (value > 0f)
        {
            // Keep the physics step fixed at 0.02s at every time scale. Slowing time
            // down is purely to ease CPU load (fewer physics steps per real second);
            // it must NOT shrink the integration step, or the same brain would fly a
            // different trajectory at different speeds — breaking deterministic
            // re-evaluation and, with it, elitism.
            Time.fixedDeltaTime = 0.02f;
        }
    }
}
