using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;

public class FoldablePanel : MonoBehaviour
{
    [Header("Folding UI Wiring")]
    [SerializeField] protected Button foldButton;
    [SerializeField] protected TextMeshProUGUI foldButtonLabel;
    [SerializeField] protected GameObject foldContent;

    [Header("Folding Config")]
    [SerializeField] protected bool startFolded = false;

    public bool IsFolded { get; private set; }
    public GameObject FoldContent => foldContent;

    private RectTransform rectTransform;
    private int pendingRebuilds; // LateUpdate passes still owed after a fold change

    protected virtual void Awake()
    {
        rectTransform = GetComponent<RectTransform>();

        if (foldButton)
        {
            foldButton.onClick.AddListener(ToggleFold);
        }
        SetFolded(startFolded, animate: false);
    }

    /// <summary>
    /// Runtime wiring for procedurally built panels (TelemetryWindowBuilder), where
    /// there is no Inspector to drag references into. Safe to call after Awake —
    /// replaces any previous wiring and applies the requested fold state.
    /// </summary>
    public void SetFoldWiring(Button button, TextMeshProUGUI buttonLabel, GameObject content, bool foldedAtStart)
    {
        if (foldButton) foldButton.onClick.RemoveListener(ToggleFold);

        foldButton = button;
        foldButtonLabel = buttonLabel;
        foldContent = content;
        startFolded = foldedAtStart;

        if (foldButton) foldButton.onClick.AddListener(ToggleFold);
        SetFolded(foldedAtStart, animate: false);
    }

    public void ToggleFold() => SetFolded(!IsFolded);

    public virtual void SetFolded(bool foldedValue, bool animate = true)
    {
        IsFolded = foldedValue;

        if (foldContent)
        {
            foldContent.SetActive(!IsFolded);
        }

        if (foldButtonLabel)
        {
            foldButtonLabel.text = IsFolded ? "→" : "↓";
        }

        if (!rectTransform) rectTransform = GetComponent<RectTransform>();
        RebuildLayoutChain();
        pendingRebuilds = 2;
    }

    /// <summary>
    /// Rebuilds this panel <em>and every ancestor</em>. Rebuilding only our own rect
    /// resizes the panel without re-running the parent's layout group, so a panel that
    /// grows on unfold simply extends over the panel below it. (Same reason
    /// <see cref="NetworkShapeWidget"/> walks the chain when its row list changes.)
    /// </summary>
    private void RebuildLayoutChain()
    {
        RectTransform rt = rectTransform;
        while (rt != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            rt = rt.parent as RectTransform;
        }
    }

    /// <summary>
    /// Content revealed by an unfold does not reach its final height within the frame
    /// that revealed it: widgets only Tick once their section is unfolded (see
    /// <see cref="UISection.TickWidgets"/>), and some finish sizing themselves on that
    /// first Tick — BrainVisualizerWidget activates its RawImage / "no selection"
    /// overlay and sizes its texture from the freshly laid-out rect. Those changes land
    /// after SetFolded's rebuild and dirty nothing above the widget, which left the
    /// sections below sitting at last frame's positions until the next fold toggle.
    ///
    /// LateUpdate runs after every Update (so after UIManager has ticked the widgets),
    /// which makes this deterministic where a coroutine racing Update would not be. Two
    /// passes: one for the settling content, one for the nested ContentSizeFitter chain
    /// (widget → section → scroll content → window), which needs more than a single pass
    /// to report settled bounds.
    /// </summary>
    private void LateUpdate()
    {
        if (pendingRebuilds <= 0) return;
        pendingRebuilds--;

        if (!rectTransform) rectTransform = GetComponent<RectTransform>();
        Canvas.ForceUpdateCanvases();
        RebuildLayoutChain();
    }
}
