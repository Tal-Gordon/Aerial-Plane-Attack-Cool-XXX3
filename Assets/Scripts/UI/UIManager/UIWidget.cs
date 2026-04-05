using UnityEngine;

public abstract class UIWidget : MonoBehaviour
{
    protected UIManager Manager { get; private set; }

    public void Initialize(UIManager manager)
    {
        Manager = manager;
        OnInitialize();
    }

    // Pulls data from Manager.Snapshot and refreshes the UI
    // Called every Update() by UIManager (via GetComponentsInChildren).
    public abstract void Tick(SimulationSnapshot snapshot);

    // Optional lifecycle hooks
    protected virtual void OnInitialize() { }
    public virtual void OnSelected(JetAgent agent) { }
    public virtual void OnDeselected() { }
}