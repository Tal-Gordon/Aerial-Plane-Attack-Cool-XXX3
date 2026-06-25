using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor for the active model's hyperparameters (<see cref="ParameterTuners.Hyperparameters"/>).
/// Builds one <see cref="ParameterRow"/> per descriptor for the loaded AI type, stages
/// edits into the tuner, and offers Save / Reset / Reset-to-Default.
///
/// <para>Save routing mirrors the backend's hot/cold split
/// (<see cref="ParameterDescriptor.RequiresReset"/>):</para>
/// <list type="bullet">
///   <item>only hot edits → committed in place via the tuner's hot round-trip
///   (SimulationManager.ApplyHotCommit); trained state is kept.</item>
///   <item>any cold edit → a confirmation dialog lists the offending parameter(s) and
///   the user picks: cancel; save params + progress then reload; or save params only
///   then reload (progress discarded).</item>
/// </list>
/// </summary>
public class HyperparameterEditorWidget : UIWidget
{
    [Header("Rows")]
    [SerializeField] private Transform rowContainer;
    [SerializeField] private GameObject rowPrefab; // must carry a ParameterRow component

    [Header("Global buttons")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button resetButton;          // revert to last saved/applied
    [SerializeField] private Button resetToDefaultButton; // DataManager baked defaults

    [Header("Cold-change confirmation dialog")]
    [SerializeField] private GameObject confirmDialog;       // panel root (starts hidden)
    [SerializeField] private TextMeshProUGUI confirmMessage;
    [SerializeField] private Button confirmSaveButton;      // save params + progress, reload
    [SerializeField] private Button confirmNoSaveButton;    // save params only, reload
    [SerializeField] private Button declineButton;          // cancel, keep edits

    private readonly Dictionary<string, ParameterRow> rows = new();
    private string builtSignature;

    protected override void OnInitialize()
    {
        if (saveButton) saveButton.onClick.AddListener(OnSaveClicked);
        if (resetButton) resetButton.onClick.AddListener(OnResetClicked);
        if (resetToDefaultButton) resetToDefaultButton.onClick.AddListener(OnResetToDefaultClicked);

        if (confirmSaveButton) confirmSaveButton.onClick.AddListener(OnConfirmSave);
        if (confirmNoSaveButton) confirmNoSaveButton.onClick.AddListener(OnConfirmNoSave);
        if (declineButton) declineButton.onClick.AddListener(HideDialog);

        HideDialog();
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        ParameterTuner tuner = ParameterTuners.Hyperparameters;

        // Rebuild rows when the descriptor set changes (the tuner appears after
        // SimulationManager.Start, or the loaded AI type changes which knobs exist).
        string signature = DescriptorSignature(tuner);
        if (signature != builtSignature)
            RebuildRows(tuner);

        if (tuner != null)
        {
            // Keep displayed values in sync with effective (staged-or-live) values.
            foreach (KeyValuePair<string, ParameterRow> entry in rows)
                entry.Value.SetDisplayedValue(tuner.GetEffectiveValue(entry.Key));
        }

        if (saveButton) saveButton.interactable = tuner != null && tuner.HasPendingChanges;
    }

    // ── Row building ─────────────────────────────────────────────────

    private string DescriptorSignature(ParameterTuner tuner)
    {
        if (tuner == null) return "<none>";

        var sb = new StringBuilder();
        foreach (ParameterDescriptor d in tuner.Descriptors)
            sb.Append(d.Key).Append(';');
        return sb.ToString();
    }

    private void RebuildRows(ParameterTuner tuner)
    {
        foreach (ParameterRow row in rows.Values)
            if (row) Destroy(row.gameObject);
        rows.Clear();

        builtSignature = DescriptorSignature(tuner);

        if (tuner == null || rowPrefab == null || rowContainer == null) return;

        foreach (ParameterDescriptor d in tuner.Descriptors)
        {
            GameObject obj = Instantiate(rowPrefab, rowContainer);
            ParameterRow row = obj.GetComponent<ParameterRow>();
            if (row == null)
            {
                Debug.LogError($"{nameof(HyperparameterEditorWidget)}: rowPrefab is missing a {nameof(ParameterRow)} component.");
                Destroy(obj);
                continue;
            }

            row.Setup(d, tuner.GetEffectiveValue(d.Key), OnRowEdited);
            rows[d.Key] = row;
        }
    }

    private void OnRowEdited(string key, float value)
    {
        ParameterTuners.Hyperparameters?.Stage(key, value);
    }

    // ── Global buttons ───────────────────────────────────────────────

    private void OnSaveClicked()
    {
        ParameterTuner tuner = ParameterTuners.Hyperparameters;
        if (tuner == null || !tuner.HasPendingChanges) return;

        List<string> coldNames = StagedColdNames(tuner);
        if (coldNames.Count == 0)
        {
            // All hot — adopt in place, keeping trained state. Pending clears, so
            // the Save button goes uninteractable on the next Tick.
            tuner.Commit();
        }
        else
        {
            ShowDialog(coldNames);
        }
    }

    private void OnResetClicked()
    {
        // Drop unsaved edits; effective values revert to the last saved/applied state.
        ParameterTuners.Hyperparameters?.Discard();
    }

    private void OnResetToDefaultClicked()
    {
        ParameterTuner tuner = ParameterTuners.Hyperparameters;
        if (tuner == null) return;

        // Stage DataManager's baked defaults for this (mode, AI type). The user then
        // Saves to apply them (which may trip the cold dialog, e.g. population size).
        Dictionary<string, float> defaults = Manager.GetDefaultHyperparameters();
        foreach (ParameterDescriptor d in tuner.Descriptors)
            if (defaults.TryGetValue(d.Key, out float v))
                tuner.Stage(d.Key, v);
    }

    // ── Cold-change confirmation dialog ──────────────────────────────

    private List<string> StagedColdNames(ParameterTuner tuner)
    {
        var names = new List<string>();
        foreach (ParameterDescriptor d in tuner.Descriptors)
            if (d.RequiresReset && tuner.Pending.ContainsKey(d.Key))
                names.Add(d.DisplayName);
        return names;
    }

    private void ShowDialog(List<string> coldNames)
    {
        if (confirmDialog == null)
        {
            Debug.LogWarning($"{nameof(HyperparameterEditorWidget)} has cold changes but no confirmDialog wired; Save ignored.");
            return;
        }

        if (confirmMessage)
        {
            confirmMessage.text =
                "These changes can't be applied in place — the scene must reload:\n\n  • " +
                string.Join("\n  • ", coldNames) +
                "\n\nSave and reload?";
        }

        confirmDialog.SetActive(true);
    }

    private void OnConfirmSave() => ConfirmReload(saveProgress: true);
    private void OnConfirmNoSave() => ConfirmReload(saveProgress: false);

    private void ConfirmReload(bool saveProgress)
    {
        ParameterTuner tuner = ParameterTuners.Hyperparameters;
        Dictionary<string, float> staged = tuner != null
            ? new Dictionary<string, float>(tuner.Pending)
            : new Dictionary<string, float>();

        HideDialog();

        // Applies staged params to the live settings, persists them (the "cold save"),
        // optionally saves training progress to file, then reloads the scene.
        Manager.ApplyHyperparametersAndReload(staged, saveProgress);
    }

    private void HideDialog()
    {
        if (confirmDialog) confirmDialog.SetActive(false);
    }

    private void OnDestroy()
    {
        if (saveButton) saveButton.onClick.RemoveListener(OnSaveClicked);
        if (resetButton) resetButton.onClick.RemoveListener(OnResetClicked);
        if (resetToDefaultButton) resetToDefaultButton.onClick.RemoveListener(OnResetToDefaultClicked);
        if (confirmSaveButton) confirmSaveButton.onClick.RemoveListener(OnConfirmSave);
        if (confirmNoSaveButton) confirmNoSaveButton.onClick.RemoveListener(OnConfirmNoSave);
        if (declineButton) declineButton.onClick.RemoveListener(HideDialog);
    }
}
