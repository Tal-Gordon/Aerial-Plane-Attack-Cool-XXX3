using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Standalone editor for the active model's hidden-layer shape, backed by
/// <see cref="ParameterTuners.NetworkShape"/>. Renders one <see cref="NetworkLayerRow"/>
/// per hidden layer with Add / Remove, plus a summary line and Save / Reset. One control
/// covers every AI type:
/// <list type="bullet">
///   <item><b>NeuroEvo</b> — arbitrary per-layer widths.</item>
///   <item><b>PPO / SAC</b> — uniform: editing any layer sets them all; add/remove changes
///   depth only.</item>
///   <item><b>NEAT</b> — disabled, with a one-line explanation (the topology evolves).</item>
/// </list>
///
/// Save is destructive — a shape change can't keep trained weights, so it rebuilds the
/// run from scratch. To avoid an accidental wipe it uses a two-click confirm (the button
/// arms for a few seconds) rather than committing on the first press.
/// </summary>
public class NetworkShapeWidget : UIWidget
{
    [Header("Layer list")]
    [SerializeField] private Transform layerRowContainer;
    [SerializeField] private GameObject layerRowPrefab;   // must carry a NetworkLayerRow
    [SerializeField] private Button addLayerButton;

    [Header("Actions")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button resetButton;

    [Header("Disabled state")]
    [SerializeField] private GameObject disabledOverlay;  // shown for NEAT / no active run
    [SerializeField] private TextMeshProUGUI disabledLabel;

    // Built in code (kept out of the prefab): a small note shown only in RL uniform
    // mode, explaining why editing one width edits them all. The full shape itself is
    // visible in the list (locked In/Out bookends + a row per hidden layer).
    private TextMeshProUGUI uniformNoteLabel;
    private TextMeshProUGUI saveButtonLabel;
    private string saveButtonDefaultText = "Save (rebuild)";

    private readonly List<NetworkLayerRow> rows = new();
    // Everything RebuildRows spawns (rows, bookends, hairlines) for wholesale teardown.
    private readonly List<GameObject> generated = new();
    private readonly List<int> staged = new();   // working copy of the hidden widths
    private string builtSignature;               // committed shape the rows were built from

    private bool confirmingSave;
    private float confirmDeadline;
    private const float ConfirmWindowSeconds = 4f;

    // Tracks the last editable state so a flip (e.g. AI type change) can trigger a
    // layout rebuild, since showing/hiding the list changes the widget's height.
    private bool? lastEditable;

    private static NetworkShapeController Shape => ParameterTuners.NetworkShape;

    protected override void OnInitialize()
    {
        if (addLayerButton) addLayerButton.onClick.AddListener(OnAddLayer);
        if (resetButton) resetButton.onClick.AddListener(OnResetClicked);
        if (saveButton)
        {
            saveButton.onClick.AddListener(OnSaveClicked);
            saveButtonLabel = saveButton.GetComponentInChildren<TextMeshProUGUI>();
            if (saveButtonLabel) saveButtonDefaultText = saveButtonLabel.text;
        }

        EnsureLayout();
        uniformNoteLabel = BuildUniformNote();
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        NetworkShapeController shape = Shape;
        bool editable = shape != null && shape.IsEditable;

        SetEditableChrome(editable, shape);

        // A change in what's shown (list vs disabled note) changes our height — rebuild
        // so the section resizes instead of waiting for a fold toggle.
        if (lastEditable != editable)
        {
            lastEditable = editable;
            ForceRebuild();
        }

        if (!editable) return;

        // Rebuild rows whenever the committed shape changes underneath us (run start, AI
        // type change, or our own reload landing).
        string signature = CommittedSignature(shape);
        if (signature != builtSignature)
            RebuildFromCommitted(shape);

        // Expire a pending Save confirmation if the user hesitates.
        if (confirmingSave && Time.unscaledTime > confirmDeadline)
            ResetSaveButton();

        UpdateUniformNote(shape);

        bool dirty = IsDirty(shape);
        if (saveButton) saveButton.interactable = dirty;
        if (resetButton) resetButton.interactable = dirty;
        if (addLayerButton) addLayerButton.interactable = staged.Count < shape.MaxHiddenLayers;
    }

    // ── Disabled / enabled chrome ────────────────────────────────────

    private void SetEditableChrome(bool editable, NetworkShapeController shape)
    {
        if (disabledOverlay) disabledOverlay.SetActive(!editable);
        if (!editable && disabledLabel)
            disabledLabel.text = shape?.DisabledReason ?? "Shape isn't configurable.";

        if (layerRowContainer) layerRowContainer.gameObject.SetActive(editable);
        if (addLayerButton) addLayerButton.gameObject.SetActive(editable);
        if (saveButton) saveButton.gameObject.SetActive(editable);
        if (resetButton) resetButton.gameObject.SetActive(editable);
        // When editable, UpdateUniformNote decides whether the note shows (RL only).
        if (!editable && uniformNoteLabel) uniformNoteLabel.gameObject.SetActive(false);
    }

    // ── Row building ─────────────────────────────────────────────────

    // A compact signature of the committed shape; a change forces a row rebuild.
    private string CommittedSignature(NetworkShapeController shape)
    {
        var sb = new StringBuilder();
        sb.Append(shape.IsUniform ? 'u' : 'v').Append(':');
        sb.Append(shape.InputSize).Append('>');
        foreach (int h in shape.GetHiddenLayers()) sb.Append(h).Append(',');
        sb.Append('>').Append(shape.OutputSize);
        return sb.ToString();
    }

    private void RebuildFromCommitted(NetworkShapeController shape)
    {
        staged.Clear();
        staged.AddRange(shape.GetHiddenLayers());
        builtSignature = CommittedSignature(shape);
        ResetSaveButton();
        RebuildRows();
    }

    private void RebuildRows()
    {
        foreach (GameObject go in generated)
            if (go)
            {
                // Destroy is deferred to end-of-frame, so the object would still count
                // toward the container's size during the rebuild below (leaving stale
                // empty space until the next layout pass). Deactivate it now so the
                // layout ignores it immediately.
                go.SetActive(false);
                Destroy(go);
            }
        generated.Clear();
        rows.Clear();

        if (layerRowPrefab == null || layerRowContainer == null) return;
        NetworkShapeController shape = Shape;
        if (shape == null) return;

        // List order: In | L1..Ln | + Add layer | Out — hairlines between entries. The
        // locked bookends make the fixed ends part of the picture without being editable.
        SpawnLockedRow("Input (sensor readings)", shape.InputSize);

        bool removable = staged.Count > NetworkShapeController.MinHiddenLayers;
        for (int i = 0; i < staged.Count; i++)
        {
            SpawnHairline();
            NetworkLayerRow row = SpawnRow();
            if (row == null) break;
            row.Setup(i + 1, staged[i], removable, OnRowWidthChanged, OnRowRemoved);
            rows.Add(row);
        }

        SpawnHairline();
        if (addLayerButton != null) addLayerButton.transform.SetAsLastSibling();
        SpawnHairline();
        SpawnLockedRow("Output (flight controls)", shape.OutputSize);

        ForceRebuild();
    }

    private NetworkLayerRow SpawnRow()
    {
        GameObject obj = Instantiate(layerRowPrefab, layerRowContainer);
        UITheme.Skin(obj); // rows spawn at runtime — theme each clone
        generated.Add(obj);

        NetworkLayerRow row = obj.GetComponent<NetworkLayerRow>();
        if (row == null)
        {
            Debug.LogError($"{nameof(NetworkShapeWidget)}: layerRowPrefab is missing a {nameof(NetworkLayerRow)} component.");
            Destroy(obj);
            generated.Remove(obj);
        }
        return row;
    }

    private void SpawnLockedRow(string tag, int value)
    {
        NetworkLayerRow row = SpawnRow();
        if (row != null) row.SetupLocked(tag, value);
    }

    private void SpawnHairline()
    {
        var go = new GameObject("Hairline", typeof(RectTransform));
        go.transform.SetParent(layerRowContainer, worldPositionStays: false);
        go.AddComponent<Image>().color = UITheme.Hairline;
        LeafHeight(go, 1f);
        generated.Add(go);
    }

    // ── Layout (owned in code so the prefab never has to be sized by hand) ──

    // Matches the pattern the other dynamic-list widgets use (HyperparameterEditorWidget):
    // each container is a vertical group that does NOT control child height, plus a
    // ContentSizeFitter that grows the container to the sum of its children. Every leaf
    // states its own height (LayoutElement + explicit sizeDelta), so the chain reports a
    // correct preferred height all the way up to the section. Idempotent.
    private void EnsureLayout()
    {
        ConfigureContainer(gameObject, spacing: 6f, padding: new RectOffset(6, 6, 4, 6));
        if (layerRowContainer != null)
        {
            ConfigureContainer(layerRowContainer.gameObject, spacing: 0f, padding: new RectOffset(0, 0, 0, 0));
            // Inset card behind the list so the rows read as one grouped control.
            Ensure<Image>(layerRowContainer.gameObject).color = new Color(0.10f, 0.11f, 0.135f, 1f);
        }

        StyleAddLayerAsGhostRow();
        StyleSaveAsPrimary();
        StyleResetAsLink();
        if (disabledOverlay != null) LeafHeight(disabledOverlay, 44f);
    }

    // "+ Add layer" lives inside the list as a quiet ghost row (invisible at rest,
    // subtle box on hover) rather than competing with Save in the button stack.
    private void StyleAddLayerAsGhostRow()
    {
        if (addLayerButton == null) return;

        if (layerRowContainer != null && addLayerButton.transform.parent != layerRowContainer)
            addLayerButton.transform.SetParent(layerRowContainer, worldPositionStays: false);
        LeafHeight(addLayerButton, 26f);

        ColorBlock colors = addLayerButton.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = new Color(UITheme.Field.r, UITheme.Field.g, UITheme.Field.b, 0.9f);
        colors.pressedColor = UITheme.FieldPressed;
        colors.selectedColor = colors.normalColor;
        colors.disabledColor = new Color(1f, 1f, 1f, 0f);
        addLayerButton.colors = colors;

        var label = addLayerButton.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        if (label != null)
        {
            label.text = "+ Add layer";
            label.fontSize = 13f;
            label.color = UITheme.Accent;
            label.alignment = TextAlignmentOptions.Center;
        }
    }

    private void StyleSaveAsPrimary()
    {
        if (saveButton == null) return;
        LeafHeight(saveButton, 32f);
        UITheme.StylePrimary(saveButton); // the accent-filled call to action
    }

    // Reset is the least important action — a dimmed text link, not a boxed button.
    private void StyleResetAsLink()
    {
        if (resetButton == null) return;
        LeafHeight(resetButton, 20f);

        if (resetButton.image != null) resetButton.image.enabled = false;

        var label = resetButton.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
        if (label != null)
        {
            label.fontSize = 12f;
            label.color = UITheme.TextDimmed;
            label.alignment = TextAlignmentOptions.Center;
            resetButton.targetGraphic = label;

            // Label-as-button: white multipliers so the dimmed text darkens on press.
            ColorBlock colors = resetButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.75f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.5f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
            resetButton.colors = colors;
        }
    }

    // A vertical stack that sizes itself to its content (childControlHeight = false so it
    // reads each child's own height; ContentSizeFitter grows it to their sum).
    private static void ConfigureContainer(GameObject go, float spacing, RectOffset padding)
    {
        var vlg = Ensure<VerticalLayoutGroup>(go);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = spacing;
        vlg.padding = padding;

        var fitter = Ensure<ContentSizeFitter>(go);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.enabled = true;
    }

    // Rebuilds the layout from the row list up through every parent (section, scroll
    // content) so added/removed rows resize the section immediately. Without this the
    // list only corrects itself when the section is folded and unfolded.
    private void ForceRebuild()
    {
        RectTransform rt = (layerRowContainer != null ? layerRowContainer : transform) as RectTransform;
        while (rt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            rt = rt.parent as RectTransform;
        }
    }

    // Since the containers don't control child height, a leaf must state its own — set on
    // both the LayoutElement (read by any height-controlling parent) and the RectTransform
    // (read by a non-controlling parent). Covers both cases so it can't collapse.
    private static void LeafHeight(Component target, float height)
    {
        if (target != null) LeafHeight(target.gameObject, height);
    }

    private static void LeafHeight(GameObject target, float height)
    {
        if (target == null) return;
        var le = Ensure<LayoutElement>(target);
        le.minHeight = height;
        le.preferredHeight = height;
        le.flexibleHeight = 0f;

        if (target.transform is RectTransform rt)
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    // ── Edits ────────────────────────────────────────────────────────

    private void OnRowWidthChanged(NetworkLayerRow row, int width)
    {
        int idx = rows.IndexOf(row);
        if (idx < 0) return;

        if (Shape != null && Shape.IsUniform)
        {
            // RL: one width for every layer. Update the model and mirror it onto the
            // other rows without firing their callbacks back.
            for (int i = 0; i < staged.Count; i++) staged[i] = width;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != row) rows[i].SetWidthSilent(width);
        }
        else
        {
            staged[idx] = width;
        }
        ResetSaveButton();
    }

    private void OnRowRemoved(NetworkLayerRow row)
    {
        if (staged.Count <= NetworkShapeController.MinHiddenLayers) return;
        int idx = rows.IndexOf(row);
        if (idx < 0) return;

        staged.RemoveAt(idx);
        RebuildRows();       // relabels indices and refreshes the removable floor
        ResetSaveButton();
    }

    private void OnAddLayer()
    {
        if (Shape == null || staged.Count >= Shape.MaxHiddenLayers) return;

        // Seed the new layer from the current shape: uniform mode copies the shared
        // width, otherwise it repeats the last layer (or a sane default).
        int seed = staged.Count > 0
            ? (Shape != null && Shape.IsUniform ? staged[0] : staged[staged.Count - 1])
            : 16;

        staged.Add(seed);
        RebuildRows();
        ResetSaveButton();
    }

    private void OnResetClicked()
    {
        NetworkShapeController shape = Shape;
        if (shape != null) RebuildFromCommitted(shape);
    }

    // ── Save (two-click confirm) ─────────────────────────────────────

    private void OnSaveClicked()
    {
        NetworkShapeController shape = Shape;
        if (shape == null || !IsDirty(shape)) return;

        if (!confirmingSave)
        {
            // First press arms the confirmation — Save discards trained progress.
            confirmingSave = true;
            confirmDeadline = Time.unscaledTime + ConfirmWindowSeconds;
            if (saveButtonLabel) saveButtonLabel.text = "Confirm — discards progress";
            UITheme.StyleDestructive(saveButton);
            return;
        }

        ResetSaveButton();
        shape.Commit(staged);   // writes the new shape into settings, then reloads the run
    }

    private void ResetSaveButton()
    {
        confirmingSave = false;
        if (saveButtonLabel) saveButtonLabel.text = saveButtonDefaultText;
        if (saveButton) UITheme.StylePrimary(saveButton); // back to the accent call-to-action
    }

    // ── Summary + dirty check ────────────────────────────────────────

    private bool IsDirty(NetworkShapeController shape)
    {
        List<int> committed = shape.GetHiddenLayers();
        List<int> normalized = shape.Normalize(staged);
        if (normalized.Count != committed.Count) return true;
        for (int i = 0; i < committed.Count; i++)
            if (normalized[i] != committed[i]) return true;
        return false;
    }

    private void UpdateUniformNote(NetworkShapeController shape)
    {
        if (uniformNoteLabel == null) return;

        bool show = shape.IsUniform;
        if (uniformNoteLabel.gameObject.activeSelf != show)
            uniformNoteLabel.gameObject.SetActive(show);
    }

    // Builds the dimmed uniform-mode note and slots it at the top of the widget column.
    private TextMeshProUGUI BuildUniformNote()
    {
        var go = new GameObject("UniformNote", typeof(RectTransform));
        go.transform.SetParent(transform, worldPositionStays: false);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = "Uniform width — one value drives every hidden layer";
        label.fontSize = 12f;
        label.color = UITheme.TextDimmed;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;

        LeafHeight(go, 18f); // its container doesn't control height — state our own

        go.transform.SetSiblingIndex(0);
        go.SetActive(false); // UpdateUniformNote shows it for RL runs
        return label;
    }

    private void OnDestroy()
    {
        if (addLayerButton) addLayerButton.onClick.RemoveListener(OnAddLayer);
        if (resetButton) resetButton.onClick.RemoveListener(OnResetClicked);
        if (saveButton) saveButton.onClick.RemoveListener(OnSaveClicked);
    }
}
