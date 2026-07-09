using UnityEngine;

/// <summary>
/// Bridges an <see cref="AITypeSelector"/>'s index-based selection to the gameplay
/// scene by recording the chosen <see cref="AIType"/> in <see cref="GameSession"/>.
/// SimulationManager reads that value on Start to override the mode's default AI type.
///
/// <para>The index → AIType mapping is data-driven via <see cref="optionAITypes"/>,
/// so the selector's buttons can be reordered or trimmed without touching code — just
/// keep this array in the same order as the buttons.</para>
///
/// <para>Attach this next to (or reference) an <see cref="AITypeSelector"/>; it
/// subscribes in code, so no Inspector UnityEvent wiring is required.</para>
/// </summary>
public class AITypeSelectionWriter : MonoBehaviour
{
    [Tooltip("The selector whose onOptionSelected drives this writer. " +
             "Defaults to an AITypeSelector on the same GameObject if left empty.")]
    [SerializeField] private AITypeSelector selector;

    [Tooltip("AIType for each selector button, in the same order as the buttons.")]
    public AIType[] optionAITypes =
    {
        AIType.FixedNeuroEvo,
        AIType.NEAT,
        AIType.PPO_MLAgents,
        AIType.SAC_MLAgents,
    };

    private void Awake()
    {
        if (selector == null)
            selector = GetComponent<AITypeSelector>();

        if (selector == null)
            Debug.LogError("[AITypeSelectionWriter] No AITypeSelector assigned or found on this GameObject; AI selection will not be recorded.");
    }

    private void OnEnable()
    {
        // Subscribe in OnEnable so we catch the selector's default selection, which
        // it broadcasts from its own Start (OnEnable runs before Start).
        if (selector == null) return;
        selector.onOptionSelected.AddListener(OnOptionSelected);

        // Sync to any selection made while we weren't listening. Without this the
        // default choice can be lost forever: if something drives the selector while
        // this GameObject is inactive (e.g. the mode list resetting it at scene load),
        // the selector's own Start broadcast then early-returns as "already selected"
        // and GameSession keeps its stale value — the AI choice silently falls back
        // to the track's persisted settings.
        if (selector.CurrentIndex >= 0)
            OnOptionSelected(selector.CurrentIndex);
    }

    private void OnDisable()
    {
        if (selector != null)
            selector.onOptionSelected.RemoveListener(OnOptionSelected);
    }

    /// <summary>
    /// Records the AIType for the selected button index. Public so it can also be
    /// wired through the Inspector's onOptionSelected event if preferred.
    /// </summary>
    public void OnOptionSelected(int index)
    {
        if (optionAITypes == null || optionAITypes.Length == 0)
        {
            Debug.LogWarning("[AITypeSelectionWriter] optionAITypes is empty; selection ignored.");
            return;
        }

        if (index < 0 || index >= optionAITypes.Length)
        {
            Debug.LogWarning($"[AITypeSelectionWriter] Selected index {index} is outside optionAITypes (length {optionAITypes.Length}); selection ignored.");
            return;
        }

        GameSession.SelectedAIType = optionAITypes[index];
    }
}
