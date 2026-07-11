using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Forwards pointer events from a title bar to its TelemetryWindow — the code
/// equivalent of the EventTrigger wiring used on scene-authored windows, so
/// procedurally built windows (TelemetryWindowBuilder) drag and double-click-reset
/// the same way. Requires a raycastable Graphic on the same GameObject.
/// </summary>
public class WindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] private TelemetryWindow window;

    public void Bind(TelemetryWindow target) => window = target;

    public void OnBeginDrag(PointerEventData eventData) { if (window) window.OnBeginDrag(eventData); }
    public void OnDrag(PointerEventData eventData)      { if (window) window.OnDrag(eventData); }
    public void OnEndDrag(PointerEventData eventData)   { if (window) window.OnEndDrag(eventData); }
    public void OnPointerClick(PointerEventData eventData) { if (window) window.HandleClick(eventData); }
}
