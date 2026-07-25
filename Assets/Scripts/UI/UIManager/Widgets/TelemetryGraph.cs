using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Minimal, dependency-free line-graph renderer. A MaskableGraphic that draws
/// two polylines (plus an L-shaped axis) straight into the UI mesh — no charting
/// package, no per-point GameObjects. It is deliberately "dumb": it knows nothing
/// about generations or episodes, it just plots whatever NORMALISED points
/// (x and y both in 0..1, origin bottom-left) RunGraphWidget hands it and stretches
/// them across its RectTransform. All scaling / axis labelling lives in the widget.
/// </summary>
public class TelemetryGraph : MaskableGraphic
{
    [Header("Background")]
    // A dark panel behind the plot so the lines/axis read clearly regardless of the
    // (light) telemetry window behind it. Set alpha to 0 for a transparent graph.
    [SerializeField] private Color backgroundColor = new Color(0.11f, 0.12f, 0.15f, 0.92f);

    [Header("Lines")]
    [SerializeField] private Color maxColor = new Color(0.35f, 0.90f, 0.45f); // green
    [SerializeField] private Color avgColor = new Color(0.35f, 0.70f, 1.00f); // blue
    [SerializeField] private Color referenceColor = new Color(1f, 0.72f, 0.24f, 0.72f);
    [SerializeField] private float lineThickness = 2.5f;

    [Header("Axis")]
    [SerializeField] private Color axisColor = new Color(1f, 1f, 1f, 0.55f); // light, on the dark panel
    [SerializeField] private float axisThickness = 1.5f;
    [SerializeField, Range(0f, 0.2f)] private float padding = 0.04f; // inset so lines don't touch the edge

    public Color MaxColor => maxColor;
    public Color AvgColor => avgColor;
    public Color ReferenceColor => referenceColor;

    public void SetBackgroundColor(Color color)
    {
        backgroundColor = color;
        SetVerticesDirty();
    }

    // Normalised (0..1) points supplied by the widget. Null/empty = nothing to draw.
    private IReadOnlyList<Vector2> avgPoints;
    private IReadOnlyList<Vector2> maxPoints;
    private bool hasReference;
    private float referenceY;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false; // purely visual — never intercept drags/clicks
    }

    /// <summary>Hand the renderer fresh normalised series and trigger a redraw.</summary>
    public void SetData(IReadOnlyList<Vector2> avg, IReadOnlyList<Vector2> max)
    {
        avgPoints = avg;
        maxPoints = max;
        SetVerticesDirty();
    }

    /// <summary>Shows a dashed horizontal reference line at a normalised Y value.</summary>
    public void SetReference(float normalisedY)
    {
        hasReference = true;
        referenceY = Mathf.Clamp01(normalisedY);
        SetVerticesDirty();
    }

    public void ClearReference()
    {
        hasReference = false;
        SetVerticesDirty();
    }

    public void Clear()
    {
        avgPoints = null;
        maxPoints = null;
        hasReference = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect r = GetPixelAdjustedRect();

        // Background panel fills the whole rect (drawn first, behind everything).
        if (backgroundColor.a > 0f)
            AddQuad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMax), backgroundColor);

        // Inner plot rect after padding, so lines/points never sit on the border.
        float padX = r.width * padding;
        float padY = r.height * padding;
        Rect plot = new Rect(r.xMin + padX, r.yMin + padY, r.width - 2f * padX, r.height - 2f * padY);

        DrawAxis(vh, plot);
        if (hasReference) DrawDashedReference(vh, plot);
        DrawPolyline(vh, avgPoints, plot, avgColor);
        DrawPolyline(vh, maxPoints, plot, maxColor); // max on top so it wins overlaps
    }

    private void DrawDashedReference(VertexHelper vh, Rect plot)
    {
        float y = plot.yMin + referenceY * plot.height;
        const int dashCount = 18;
        float cell = plot.width / dashCount;
        for (int i = 0; i < dashCount; i += 2)
        {
            Vector2 start = new Vector2(plot.xMin + i * cell, y);
            Vector2 end = new Vector2(
                plot.xMin + Mathf.Min((i + 1) * cell, plot.width), y);
            AddSegment(vh, start, end, axisThickness, referenceColor);
        }
    }

    // L-shaped axis: left (Y) and bottom (X) edges of the plot rect.
    private void DrawAxis(VertexHelper vh, Rect plot)
    {
        Vector2 bl = new Vector2(plot.xMin, plot.yMin);
        Vector2 br = new Vector2(plot.xMax, plot.yMin);
        Vector2 tl = new Vector2(plot.xMin, plot.yMax);

        AddSegment(vh, bl, br, axisThickness, axisColor); // X axis
        AddSegment(vh, bl, tl, axisThickness, axisColor); // Y axis
    }

    private void DrawPolyline(VertexHelper vh, IReadOnlyList<Vector2> pts, Rect plot, Color color)
    {
        if (pts == null || pts.Count == 0) return;

        // A single sample has no segment to draw — show a small marker instead so
        // the very first generation/episode isn't an empty graph.
        if (pts.Count == 1)
        {
            Vector2 p = ToPlot(pts[0], plot);
            AddSegment(vh, p + Vector2.left * 3f, p + Vector2.right * 3f, lineThickness, color);
            return;
        }

        for (int i = 0; i < pts.Count - 1; i++)
            AddSegment(vh, ToPlot(pts[i], plot), ToPlot(pts[i + 1], plot), lineThickness, color);
    }

    private static Vector2 ToPlot(Vector2 normalised, Rect plot)
    {
        return new Vector2(
            plot.xMin + Mathf.Clamp01(normalised.x) * plot.width,
            plot.yMin + Mathf.Clamp01(normalised.y) * plot.height);
    }

    // Emits one thickness-wide quad between a and b with the given solid color.
    private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 dir = (b - a);
        float len = dir.magnitude;
        if (len < 0.0001f) return;
        dir /= len;

        Vector2 normal = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

        int idx = vh.currentVertCount;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        vert.position = a - normal; vh.AddVert(vert);
        vert.position = a + normal; vh.AddVert(vert);
        vert.position = b + normal; vh.AddVert(vert);
        vert.position = b - normal; vh.AddVert(vert);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }

    // Solid axis-aligned rectangle from bottom-left bl to top-right tr.
    private void AddQuad(VertexHelper vh, Vector2 bl, Vector2 tr, Color color)
    {
        int idx = vh.currentVertCount;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        vert.position = new Vector2(bl.x, bl.y); vh.AddVert(vert);
        vert.position = new Vector2(bl.x, tr.y); vh.AddVert(vert);
        vert.position = new Vector2(tr.x, tr.y); vh.AddVert(vert);
        vert.position = new Vector2(tr.x, bl.y); vh.AddVert(vert);

        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx + 2, idx + 3, idx);
    }
}
