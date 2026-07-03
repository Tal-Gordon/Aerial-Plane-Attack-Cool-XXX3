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
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
}
