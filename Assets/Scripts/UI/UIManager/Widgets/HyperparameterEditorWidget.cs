using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor for the active run's tunable parameters. Renders one <see cref="ParameterRow"/>
/// per descriptor across two tuners and stages edits into whichever one owns the row:
/// <list type="bullet">
///   <item>the model's hyperparameters (<see cref="ParameterTuners.Hyperparameters"/>),</item>
///   <item>the active objective's reward parameters (<see cref="ParameterTuners.Reward"/>),
///   which live on a different GameObject (the objective) but are edited from the
///   same panel.</item>
/// </list>
/// offers Save / Reset / Reset-to-Default.
///
/// <para>Save routing mirrors the backend's hot/cold split
/// (<see cref="ParameterDescriptor.RequiresReset"/>):</para>
/// <list type="bullet">
///   <item>only hot edits → each dirty tuner is committed in place via its own
///   round-trip (SimulationManager.ApplyHotCommit); trained state is kept. Reward
///   params are always hot.</item>
///   <item>any cold hyperparameter edit → a confirmation dialog lists the offending
///   parameter(s) and the user picks: cancel; save params + progress then reload; or
///   save params only then reload (progress discarded). Pending reward edits are
///   committed first so they aren't lost to the reload.</item>
/// </list>
/// </summary>
public class HyperparameterEditorWidget : UIWidget
{
    [Header("Rows")]
    // Separate parents so a delimiter object can sit between the two groups in the
    // layout. Hyperparameter rows go under the first, reward-parameter rows under
    // the second; both are built from the same rowPrefab.
    [SerializeField] private Transform hyperparameterRowContainer;
    [SerializeField] private Transform rewardRowContainer;
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
    // Maps each row's key back to the tuner that owns it, so edits stage on the
    // right buffer and Tick reads each row's value from its own source.
    private readonly Dictionary<string, ParameterTuner> rowTuner = new();
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

    // Tuners rendered by this widget, in display order. Hyperparameters first,
    // then the objective's reward params. Nulls (no active run) are tolerated by
    // the call sites.
    private IEnumerable<ParameterTuner> Tuners()
    {
        yield return ParameterTuners.Hyperparameters;
        yield return ParameterTuners.Reward;
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        // Rebuild rows when the combined descriptor set changes (the tuners appear
        // after SimulationManager.Start, or the loaded AI type changes which knobs
        // exist).
        string signature = DescriptorSignature();
        if (signature != builtSignature)
            RebuildRows();

        // Keep displayed values in sync with effective (staged-or-live) values,
        // reading each row from its owning tuner.
        foreach (KeyValuePair<string, ParameterRow> entry in rows)
            if (rowTuner.TryGetValue(entry.Key, out ParameterTuner tuner) && tuner != null)
                entry.Value.SetDisplayedValue(tuner.GetEffectiveValue(entry.Key));

        if (saveButton) saveButton.interactable = HasAnyPendingChanges();
    }

    private bool HasAnyPendingChanges()
    {
        foreach (ParameterTuner tuner in Tuners())
            if (tuner != null && tuner.HasPendingChanges) return true;
        return false;
    }

    // ── Row building ─────────────────────────────────────────────────

    private string DescriptorSignature()
    {
        var sb = new StringBuilder();
        int index = 0;
        foreach (ParameterTuner tuner in Tuners())
        {
            // The leading index keeps tuners distinct in the signature even if two
            // share a key, so a tuner appearing/disappearing always rebuilds.
            sb.Append(index++).Append(':');
            if (tuner == null) { sb.Append("<none>;"); continue; }
            foreach (ParameterDescriptor d in tuner.Descriptors)
                sb.Append(d.Key).Append(';');
        }
        return sb.ToString();
    }

    private void RebuildRows()
    {
        foreach (ParameterRow row in rows.Values)
            if (row) Destroy(row.gameObject);
        rows.Clear();
        rowTuner.Clear();

        builtSignature = DescriptorSignature();

        if (rowPrefab == null) return;

        // Each tuner's rows go under its own parent so a delimiter can separate them.
        BuildRowsFor(ParameterTuners.Hyperparameters, hyperparameterRowContainer);
        BuildRowsFor(ParameterTuners.Reward, rewardRowContainer);
    }

    private void BuildRowsFor(ParameterTuner tuner, Transform container)
    {
        if (tuner == null || container == null) return;

        foreach (ParameterDescriptor d in tuner.Descriptors)
        {
            if (rows.ContainsKey(d.Key))
            {
                Debug.LogWarning($"{nameof(HyperparameterEditorWidget)}: duplicate parameter key '{d.Key}' across tuners; the later one is ignored.");
                continue;
            }

            GameObject obj = Instantiate(rowPrefab, container);
            ParameterRow row = obj.GetComponent<ParameterRow>();
            if (row == null)
            {
                Debug.LogError($"{nameof(HyperparameterEditorWidget)}: rowPrefab is missing a {nameof(ParameterRow)} component.");
                Destroy(obj);
                continue;
            }

            row.Setup(d, tuner.GetEffectiveValue(d.Key), OnRowEdited);
            rows[d.Key] = row;
            rowTuner[d.Key] = tuner;
        }
    }

    private void OnRowEdited(string key, float value)
    {
        if (rowTuner.TryGetValue(key, out ParameterTuner tuner))
            tuner?.Stage(key, value);
    }

    // ── Global buttons ───────────────────────────────────────────────

    private void OnSaveClicked()
    {
        if (!HasAnyPendingChanges()) return;

        // Cold edits can only come from the hyperparameter tuner (reward params are
        // always hot), so confirm those before doing anything destructive.
        List<string> coldNames = StagedColdHyperparameterNames();
        if (coldNames.Count == 0)
        {
            CommitAllHot();
        }
        else
        {
            ShowDialog(coldNames);
        }
    }

    // Commits every dirty tuner in place. Each Commit is its own protected
    // Save→Load round-trip that keeps trained state.
    private void CommitAllHot()
    {
        foreach (ParameterTuner tuner in Tuners())
            if (tuner != null && tuner.HasPendingChanges)
                tuner.Commit();
    }

    private void OnResetClicked()
    {
        // Drop unsaved edits across both tuners; effective values revert to the last
        // saved/applied state.
        foreach (ParameterTuner tuner in Tuners())
            tuner?.Discard();
    }

    private void OnResetToDefaultClicked()
    {
        // Stage the baked defaults (from DataManager) onto both tuners. Only keys
        // that have a descriptor — i.e. the editable dials — are staged; non-dial
        // params keep their current value.
        StageDefaults(ParameterTuners.Hyperparameters, Manager.GetDefaultHyperparameters());
        StageDefaults(ParameterTuners.Reward, Manager.GetDefaultRewardParameters());
    }

    private static void StageDefaults(ParameterTuner tuner, Dictionary<string, float> defaults)
    {
        if (tuner == null || defaults == null) return;
        foreach (ParameterDescriptor d in tuner.Descriptors)
            if (defaults.TryGetValue(d.Key, out float v))
                tuner.Stage(d.Key, v);
    }

    // ── Cold-change confirmation dialog ──────────────────────────────

    private List<string> StagedColdHyperparameterNames()
    {
        var names = new List<string>();
        ParameterTuner tuner = ParameterTuners.Hyperparameters;
        if (tuner == null) return names;

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
        HideDialog();

        // Reward edits are hot — commit them in place first so they aren't lost when
        // the scene reloads for the cold hyperparameter change.
        ParameterTuner reward = ParameterTuners.Reward;
        if (reward != null && reward.HasPendingChanges) reward.Commit();

        ParameterTuner hyper = ParameterTuners.Hyperparameters;
        Dictionary<string, float> staged = hyper != null
            ? new Dictionary<string, float>(hyper.Pending)
            : new Dictionary<string, float>();

        // Applies staged hyperparameters to the live settings, persists them (the
        // "cold save"), optionally saves training progress to file, then reloads.
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
