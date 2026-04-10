using UnityEngine;
using TMPro;

public class GeneticControlsWidget : UIWidget
{
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

        // TODO Opus Note #5: When this widget is refactored to use static events
        // (e.g. static event Action<float> OnMutationRateChanged), the subscribing
        // paradigm (a plain C# class, not a MonoBehaviour) MUST unsubscribe in its
        // Dispose() method. Otherwise scene reloads or paradigm swaps leave stale
        // subscribers that cause null refs. Add Dispose() to ITrainingParadigm.

        // Initial sync from GeneticManager
        SyncUIFromManager();

        if (mutationRateInput) mutationRateInput.onEndEdit.AddListener(OnMutationRateEndEdit);
        if (lambdaInput) lambdaInput.onEndEdit.AddListener(OnLambdaEndEdit);
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        // Only update the input field if the user is not currently interacting with it
        if (mutationRateInput && !mutationRateInput.isFocused)
        {
            string valueString = snapshot.MutationRate.ToString();
            if (mutationRateInput.text != valueString)
            {
                mutationRateInput.text = valueString;
            }
        }

        if (lambdaInput && !lambdaInput.isFocused)
        {
            string valueString = snapshot.Lambda.ToString();
            if (lambdaInput.text != valueString)
            {
                lambdaInput.text = valueString;
            }
        }
    }

    private void SyncUIFromManager()
    {
        if (Manager == null || Manager.GeneticManager == null) return;

        if (mutationRateInput)
            mutationRateInput.text = Manager.GeneticManager.GetMutationRate().ToString();
            
        if (lambdaInput)
            lambdaInput.text = Manager.GeneticManager.GetLambda().ToString();
    }

    private void OnMutationRateEndEdit(string value)
    {
        if (float.TryParse(value, out float result))
        {
            // Clamp to strictly positive (minimum 0.0001)
            result = Mathf.Max(result, 0.0001f);
            
            Manager.GeneticManager.SetMutationRate(result);
            
            // Sync UI to show clamped value
            mutationRateInput.text = result.ToString();
        }
        else
        {
            // Reset to current manager value if invalid input
            mutationRateInput.text = Manager.GeneticManager.GetMutationRate().ToString();
        }
    }

    private void OnLambdaEndEdit(string value)
    {
        if (float.TryParse(value, out float result))
        {
            // Clamp to [0, 1] range
            result = Mathf.Clamp01(result);

            Manager.GeneticManager.SetLambda(result);

            // Sync UI to show clamped value
            lambdaInput.text = result.ToString();
        }
        else
        {
            // Reset to current manager value if invalid input
            lambdaInput.text = Manager.GeneticManager.GetLambda().ToString();
        }
    }
}
