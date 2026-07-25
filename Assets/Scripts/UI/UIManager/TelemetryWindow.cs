using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    private bool layoutSettled; // false until Awake has finished and a frame has laid out

    protected override void Awake()
    {
        PromoteFoldContentToScrollView(); // before base.Awake applies the initial fold state
        rect = GetComponent<RectTransform>();
        base.Awake();
        parentRect = rect.parent as RectTransform; // Canvas
        initialPosition = rect.localPosition;
        layoutSettled = true;
    }

    public override void SetFolded(bool foldedValue, bool animate = true)
    {
        // Skipped during base.Awake: the window's height is still unmeasured that early
        // (a ContentSizeFitter hasn't run yet), and the first real fold is a click, by
        // which point the layout is settled and the height reads true.
        if (layoutSettled) PinPivotToTopEdge();
        base.SetFolded(foldedValue, animate);
    }

    /// <summary>
    /// Moves the window's pivot to its top edge, shifting the position to compensate so
    /// nothing appears to move.
    ///
    /// A window that sizes itself to its content grows and shrinks about its pivot. The
    /// scene-authored window pivots at its centre (0.5, 0.5), so collapsing it to the
    /// title bar keeps its <em>midpoint</em> fixed and strands the bar halfway down the
    /// screen. With the pivot on the top edge the window rolls up under a stationary
    /// title bar instead. One-time and idempotent — once moved, every later fold, and any
    /// deferred layout pass, keeps the top edge fixed for free.
    /// </summary>
    private void PinPivotToTopEdge()
    {
        if (rect.pivot.y >= 0.999f) return; // already top-pivoted (code-built windows are)

        float height = rect.rect.height;
        if (height <= 0f) return; // nothing meaningful to measure yet; retry on the next fold

        float shift = (1f - rect.pivot.y) * height;
        rect.pivot = new Vector2(rect.pivot.x, 1f);
        rect.anchoredPosition += new Vector2(0f, shift);

        // Double-click reset targets a stored localPosition — carry it through the same
        // shift so it still resolves to the window's authored spot.
        initialPosition += new Vector2(0f, shift);
    }

    /// <summary>
    /// Retargets a fold that points <em>inside</em> a scroll view at the scroll view itself.
    ///
    /// Scene-authored windows wire foldContent to the ScrollRect's Content object — the
    /// innermost node, inside the viewport. Deactivating only that leaves the scroll view's
    /// other children alive, and the vertical scrollbar keeps drawing as a stray strip
    /// beside the collapsed title bar. Folding the whole scroll view takes the scrollbar,
    /// viewport and content together.
    ///
    /// Windows built by <see cref="TelemetryWindowBuilder"/> already fold the scroll view,
    /// so this is a no-op for them. Fixed here rather than in the scene YAML so it holds
    /// for every scene, present and future (and because scene merges have corrupted files
    /// before — see CLAUDE.md).
    /// </summary>
    private void PromoteFoldContentToScrollView()
    {
        if (foldContent == null) return;

        ScrollRect scroll = foldContent.GetComponentInParent<ScrollRect>(includeInactive: true);
        if (scroll == null || scroll.gameObject == foldContent) return;

        // Never fold the window itself — that would take the title bar and the fold button
        // with it, leaving no way to unfold. Likewise ignore a scroll view we don't own.
        if (scroll.gameObject == gameObject || !scroll.transform.IsChildOf(transform)) return;

        foldContent = scroll.gameObject;
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