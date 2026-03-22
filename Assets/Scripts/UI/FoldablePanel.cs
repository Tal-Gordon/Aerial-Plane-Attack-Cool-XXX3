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

    protected virtual void Awake()
    {
        if (foldButton)
        {
            foldButton.onClick.AddListener(ToggleFold);
        }
        SetFolded(startFolded, animate: false);
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
    }
}
