using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One widget holding a Save and a Load button for the current run's full training
/// state (the same operations as the S / L hotkeys). Both route through the
/// UIManager to the SimulationManager, which saves/loads per the mode + AI type
/// currently loaded.
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

    private void OnSave() => Manager.SaveState();

    private void OnLoad() => Manager.LoadState();

    private void OnDestroy()
    {
        if (saveButton != null) saveButton.onClick.RemoveListener(OnSave);
        if (loadButton != null) loadButton.onClick.RemoveListener(OnLoad);
    }
}
