using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One widget holding a Save and a Load button for the current run's full training
/// state. Both route through the UIManager to the SimulationManager, which
/// saves/loads per the mode + AI type currently loaded.
/// </summary>
public class SaveLoadWidget : UIWidget
{
    [SerializeField] private Button saveButton;
    [SerializeField] private Button loadButton;

    protected override void OnInitialize()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSave);
        else
            Debug.LogWarning($"{nameof(SaveLoadWidget)} on {gameObject.name} is missing a Save Button reference!");

        if (loadButton != null)
            loadButton.onClick.AddListener(OnLoad);
        else
            Debug.LogWarning($"{nameof(SaveLoadWidget)} on {gameObject.name} is missing a Load Button reference!");
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        // Plain action buttons — nothing to poll from the snapshot.
    }

    // Save is light and frequent — a non-blocking corner toast, not a screen takeover.
    private void OnSave()
    {
        if (LoadingOverlay.Instance != null)
            LoadingOverlay.Instance.RunToast(() => Manager.SaveState(), "Saving…", "Saved ✓");
        else
            Manager.SaveState();
    }

    // Load tears down and rebuilds the whole run — dim-modal so input is blocked
    // and the overlay is painted before the (synchronous) rebuild hitches the frame.
    private void OnLoad()
    {
        if (LoadingOverlay.Instance != null)
            LoadingOverlay.Instance.RunModal(() => Manager.LoadState(), "Loading run…");
        else
            Manager.LoadState();
    }

    private void OnDestroy()
    {
        if (saveButton != null) saveButton.onClick.RemoveListener(OnSave);
        if (loadButton != null) loadButton.onClick.RemoveListener(OnLoad);
    }
}
