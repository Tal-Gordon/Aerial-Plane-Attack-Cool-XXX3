using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Toggle that switches inference mode (replay the saved champion/policy, no
/// learning, no saving) on and off for the currently loaded AI. Works the same way
/// for every paradigm — it routes through <see cref="UIManager.SetInferenceMode"/>,
/// which the SimulationManager handles per the active AI.
///
/// <para>The toggle reflects the live state from the snapshot every Tick, so it
/// reverts itself if a transition is refused (e.g. entering inference with no
/// saved run yet).</para>
/// </summary>
public class InferenceToggleWidget : UIWidget
{
    [SerializeField] private Toggle toggle;

    protected override void OnInitialize()
    {
        if (toggle != null)
            toggle.onValueChanged.AddListener(OnToggleChanged);
        else
            Debug.LogWarning($"{nameof(InferenceToggleWidget)} on {gameObject.name} is missing a Toggle reference!");
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        if (toggle == null) return;

        // Sync to the actual mode without firing OnToggleChanged, so refused
        // transitions are reflected here.
        toggle.SetIsOnWithoutNotify(snapshot.InInferenceMode);
    }

    private void OnToggleChanged(bool on)
    {
        // SimulationManager decides whether the change is allowed; the next Tick
        // re-syncs the toggle from the snapshot if it wasn't.
        // Entering/leaving inference rebuilds the run (and for RL relaunches the
        // Python trainer), so run it behind a dim-modal overlay.
        if (LoadingOverlay.Instance != null)
            LoadingOverlay.Instance.RunModal(
                () => Manager.SetInferenceMode(on),
                on ? "Starting inference…" : "Resuming training…");
        else
            Manager.SetInferenceMode(on);
    }

    private void OnDestroy()
    {
        if (toggle != null)
            toggle.onValueChanged.RemoveListener(OnToggleChanged);
    }
}
