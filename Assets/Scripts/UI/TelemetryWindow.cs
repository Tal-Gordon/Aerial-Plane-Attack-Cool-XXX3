using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class TelemetryWindow : FoldablePanel, IDragHandler, IBeginDragHandler
{
    [Header("UI Wiring")]
    [SerializeField] private RectTransform dragHandle; // the title bar

    [Header("Config")]
    [SerializeField] private float minX = 0f; // optional screen clamping
    [SerializeField] private float minY = 0f;

    private RectTransform rect;
    private Canvas canvas;
    private bool isDragging;
    private Vector2 dragOffset;

    protected override void Awake()
    {
        base.Awake();
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    // Hook these up to EventTrigger components on dragHandle,
    // or call them from a standalone DragHandler MonoBehaviour on the handle.
    public void OnBeginDrag(UnityEngine.EventSystems.PointerEventData data)
    {
        isDragging = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            data.position,
            data.pressEventCamera,
            out Vector2 localPoint
        );
        dragOffset = rect.anchoredPosition - localPoint;
    }

    public void OnDrag(UnityEngine.EventSystems.PointerEventData data)
    {
        if (!isDragging) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect.parent as RectTransform,
            data.position,
            data.pressEventCamera,
            out Vector2 localPoint
        );
        Vector2 target = localPoint + dragOffset;
        // Optional: clamp to screen
        // target.x = Mathf.Max(target.x, minX);
        // target.y = Mathf.Max(target.y, minY);
        rect.anchoredPosition = target;
    }

    public void OnEndDrag(UnityEngine.EventSystems.PointerEventData data)
    {
        isDragging = false;
    }
}