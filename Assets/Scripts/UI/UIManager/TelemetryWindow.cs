using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class TelemetryWindow : FoldablePanel
{
    [Header("UI Wiring")]
    [SerializeField] private RectTransform dragHandle; // the title bar

    private RectTransform rect;
    private RectTransform parentRect; // Cache the parent for boundary calculations
    private bool isDragging;
    private Vector2 dragOffset;
    private Vector2 initialPosition;

    protected override void Awake()
    {
        base.Awake();
        rect = GetComponent<RectTransform>();
        parentRect = rect.parent as RectTransform; // Canvas
        initialPosition = rect.localPosition;
    }

    // Event trigger wrappers (for the Inspector)
    
    public void HandleBeginDrag(BaseEventData data)
    {
        OnBeginDrag((PointerEventData)data);
    }

    public void HandleDrag(BaseEventData data)
    {
        OnDrag((PointerEventData)data);
    }

    public void HandleEndDrag(BaseEventData data)
    {
        OnEndDrag((PointerEventData)data);
    }

    public void HandleClick(BaseEventData data)
    {
        PointerEventData pointerData = (PointerEventData)data;
        
        // Check if it's a double click
        if (pointerData.clickCount == 2)
        {
            rect.localPosition = initialPosition;
        }
    }

    public void OnBeginDrag(PointerEventData data)
    {
        isDragging = true;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            data.position,
            data.pressEventCamera,
            out Vector2 localPoint
        );
        dragOffset = (Vector2)rect.localPosition - localPoint;
    }

    public void OnDrag(PointerEventData data)
    {
        if (!isDragging) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            data.position,
            data.pressEventCamera,
            out Vector2 localPoint
        );
        Vector2 target = localPoint + dragOffset;

        // Calculate boundaries based on parent size and pivot
        float minX = parentRect.rect.xMin + (rect.rect.width * rect.pivot.x);
        float maxX = parentRect.rect.xMax - (rect.rect.width * (1f - rect.pivot.x));

        float minY = parentRect.rect.yMin + (rect.rect.height * rect.pivot.y);
        float maxY = parentRect.rect.yMax - (rect.rect.height * (1f - rect.pivot.y));

        // Clamp the target position
        target.x = Mathf.Clamp(target.x, minX, maxX);
        target.y = Mathf.Clamp(target.y, minY, maxY);

        // Apply clamped position
        rect.localPosition = target;
    }

    public void OnEndDrag(PointerEventData data)
    {
        isDragging = false;
    }
}