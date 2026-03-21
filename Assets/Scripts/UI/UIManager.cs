using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private Slider timeSlider;

    void Start()
    {
        // Optional: Initialize slider value to current timescale
        if (timeSlider != null)
        {
            timeSlider.value = Time.timeScale;
            timeSlider.onValueChanged.AddListener(SetTimeScale);
        }
    }

    public void SetTimeScale(float value)
    {
        Time.timeScale = value;
        
        // Optional: Adjust fixedDeltaTime to keep physics smooth
        // This prevents "stuttering" during slow-mo
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
    }
}
