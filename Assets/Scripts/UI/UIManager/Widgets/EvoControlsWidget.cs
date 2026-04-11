using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Controls widget for evolutionary paradigm parameters (mutation rate, lambda).
/// Reads current values from SimulationSnapshot.EvoData.
/// Writes changes via static events — EvolutionaryParadigm subscribes to these.
/// </summary>
public class EvoControlsWidget : UIWidget
{
    // ── Static events (paradigm subscribes, widget fires blindly) ────
    public static event Action<float> OnMutationRateChanged;
    public static event Action<float> OnLambdaChanged;

    [Header("Wiring")]
    [SerializeField] private TextMeshProUGUI mutationRateText;
    [SerializeField] private TextMeshProUGUI lambdaText;
    [SerializeField] private TMP_InputField mutationRateInput;
    [SerializeField] private TMP_InputField lambdaInput;

    protected override void OnInitialize()
    {
        if (mutationRateText) mutationRateText.text = "Mutation Rate";
        if (lambdaText) lambdaText.text = "Lambda";

        // Set input validation to allow only decimal numbers
        if (mutationRateInput) mutationRateInput.contentType = TMP_InputField.ContentType.DecimalNumber;
        if (lambdaInput)      lambdaInput.contentType      = TMP_InputField.ContentType.DecimalNumber;

        if (mutationRateInput) mutationRateInput.onEndEdit.AddListener(OnMutationRateEndEdit);
        if (lambdaInput) lambdaInput.onEndEdit.AddListener(OnLambdaEndEdit);
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        // This widget is only relevant when running an evolutionary paradigm
        if (snapshot.EvoData == null) return;

        // Only update the input field if the user is not currently interacting with it
        if (mutationRateInput && !mutationRateInput.isFocused)
        {
            string valueString = snapshot.EvoData.MutationRate.ToString();
            if (mutationRateInput.text != valueString)
            {
                mutationRateInput.text = valueString;
            }
        }

        if (lambdaInput && !lambdaInput.isFocused)
        {
            string valueString = snapshot.EvoData.Lambda.ToString();
            if (lambdaInput.text != valueString)
            {
                lambdaInput.text = valueString;
            }
        }
    }

    private void OnMutationRateEndEdit(string value)
    {
        if (float.TryParse(value, out float result))
        {
            // Clamp to strictly positive (minimum 0.0001)
            result = Mathf.Max(result, 0.0001f);

            // Fire the event, whoever is subscribed handles it
            OnMutationRateChanged?.Invoke(result);

            // Sync UI to show clamped value
            mutationRateInput.text = result.ToString();
        }
        else
        {
            // Reset to last known good value from snapshot
            if (Manager?.Snapshot?.EvoData != null)
                mutationRateInput.text = Manager.Snapshot.EvoData.MutationRate.ToString();
        }
    }

    private void OnLambdaEndEdit(string value)
    {
        if (float.TryParse(value, out float result))
        {
            // Clamp to [0, 1] range
            result = Mathf.Clamp01(result);

            // Fire the event, whoever is subscribed handles it
            OnLambdaChanged?.Invoke(result);

            // Sync UI to show clamped value
            lambdaInput.text = result.ToString();
        }
        else
        {
            // Reset to last known good value from snapshot
            if (Manager?.Snapshot?.EvoData != null)
                lambdaInput.text = Manager.Snapshot.EvoData.Lambda.ToString();
        }
    }
}
