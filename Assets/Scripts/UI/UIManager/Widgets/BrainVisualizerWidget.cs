using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Live, per-AI brain visualizer. The widget owns the texture and the rasterizing
// primitives only; the actual "what does this network look like" logic lives in a
// set of BrainRenderer strategies (one per AI type). Each frame it picks the
// renderer that matches the selected jet's brain, builds the topology once (or
// rebuilds it when the structure changes — NEAT grows over generations), then
// samples live activations and draws.
//
// The render texture sizes itself to the RawImage's on-screen rect, and node radius
// adapts to how crowded the layout is, so complex architectures stay legible instead
// of collapsing into noise.
//
// To trigger it, a jet must be selected (see JetSelectionController).
public class BrainVisualizerWidget : UIWidget
{
    [Header("Wiring")]
    [SerializeField] private RawImage rawImage;
    [SerializeField] private GameObject noSelectionOverlay; // optional "no agent selected" label

    [Header("Sizing")]
    [Tooltip("Size the render texture to the RawImage's rect so it fills the panel.")]
    [SerializeField] private bool dynamicSize = true;
    [Tooltip("Used only when dynamicSize is off, or before the rect has been laid out.")]
    [SerializeField] private int fallbackWidth = 340;
    [SerializeField] private int fallbackHeight = 400;
    [Tooltip("Upper bound on either texture dimension, to cap allocation.")]
    [SerializeField] private int maxTextureDimension = 2048;
    [SerializeField] private int padding = 24; // px border kept clear inside the texture

    [Header("Nodes")]
    [SerializeField] private int maxNodeRadius = 9;
    [SerializeField] private int minNodeRadius = 2;
    [Tooltip("Node radius as a fraction of the spacing to its nearest neighbour.")]
    [SerializeField] private float nodeSpacingFraction = 0.4f;

    [Header("Performance")]
    [SerializeField] private int updateEveryNFrames = 2; // skip frames (higher = cheaper)

    [Header("Colours")]
    [SerializeField] private Color paletteBackground = new Color(0.33f, 0.33f, 0.35f, 1f);
    [SerializeField] private Color paletteNodeInactive = new Color(0.6f, 0.60f, 0.60f, 1f); // ~zero / unknown
    [SerializeField] private Color paletteNodePositive = new Color(0.3f, 0.68f, 0.95f, 1f); // high activation (blue)
    [SerializeField] private Color paletteNodeNegative = new Color(0.96f, 0.55f, 0.22f, 1f); // negative (orange)
    [Tooltip("Connections are drawn uniform and opaque on purpose — quiet structure, not noise.")]
    [SerializeField] private Color paletteConnection = new Color(0.18f, 0.21f, 0.28f, 1f);

    // ---- Internal ----
    private Texture2D tex;
    private Color[]   clearPixels;
    private int       texW, texH;
    private int       frameCounter;
    private int       computedRadius;

    private readonly NetworkGraph graph = new NetworkGraph();
    private readonly Dictionary<int, int> columnCounts = new Dictionary<int, int>();
    private BrainRenderer[] renderers;
    private BrainRenderer   activeRenderer;
    private object          topologyToken;

    protected override void OnInitialize()
    {
        // The serialized background is a pre-theme mid-grey that clashes with the
        // dark window (prefab-default trap — code defaults can't reach it). Pin the
        // structural colours to the palette; activation colours already match it.
        paletteBackground = new Color(0.10f, 0.11f, 0.14f, 1f);
        if (noSelectionOverlay != null)
        {
            var overlayLabel = noSelectionOverlay.GetComponentInChildren<TMPro.TMP_Text>(includeInactive: true);
            if (overlayLabel != null) overlayLabel.color = UITheme.TextDimmed;
        }

        computedRadius = maxNodeRadius;

        // Renderers are tried in order; their Matches() checks are mutually
        // exclusive, so order only matters if a brain ever satisfies two of them.
        renderers = new BrainRenderer[]
        {
            new NeuroEvoBrainRenderer(),
            new NeatBrainRenderer(),
            new RLBrainRenderer(),
        };
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        frameCounter++;
        if (frameCounter % Mathf.Max(1, updateEveryNFrames) != 0) return;

        JetAgent agent = snapshot.SelectedAgent;
        BrainRenderer renderer = agent != null ? PickRenderer(agent) : null;
        bool canDraw = renderer != null;

        if (noSelectionOverlay) noSelectionOverlay.SetActive(!canDraw);
        if (rawImage)           rawImage.gameObject.SetActive(canDraw);

        if (!canDraw)
        {
            activeRenderer = null;
            topologyToken  = null;
            return;
        }

        bool sizeChanged = EnsureTexture();

        // Rebuild the layout only when the renderer changes (a different AI selected)
        // or the topology token changes (NEAT grew a node/connection).
        object token = renderer.TopologyToken(agent);
        bool rebuilt = false;
        if (renderer != activeRenderer || !Equals(token, topologyToken))
        {
            activeRenderer = renderer;
            topologyToken  = token;
            graph.Clear();
            renderer.BuildTopology(agent, graph);
            rebuilt = true;
        }

        // Node positions (in pixels) only move when the layout or texture size
        // changes, so the density-based radius is recomputed only then.
        if (rebuilt || sizeChanged) RecomputeNodeRadius();

        renderer.SampleActivations(agent, graph);
        DrawFrame();
    }

    private BrainRenderer PickRenderer(JetAgent agent)
    {
        foreach (BrainRenderer r in renderers)
            if (r.Matches(agent)) return r;
        return null;
    }

    // Creates/resizes the texture to match the RawImage rect (when dynamicSize is on).
    // Returns true if the texture was (re)created this call.
    private bool EnsureTexture()
    {
        int w = fallbackWidth;
        int h = fallbackHeight;

        if (dynamicSize && rawImage != null)
        {
            Rect rect = rawImage.rectTransform.rect;
            float scale = rawImage.canvas != null ? rawImage.canvas.scaleFactor : 1f;
            int rw = Mathf.RoundToInt(rect.width * scale);
            int rh = Mathf.RoundToInt(rect.height * scale);
            if (rw > 8 && rh > 8) { w = rw; h = rh; } // rect may be unset before first layout
        }

        w = Mathf.Clamp(w, 16, maxTextureDimension);
        h = Mathf.Clamp(h, 16, maxTextureDimension);

        if (tex != null && w == texW && h == texH) return false;

        texW = w;
        texH = h;

        if (tex != null) Destroy(tex);
        tex = new Texture2D(texW, texH, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear
        };

        clearPixels = new Color[texW * texH];
        for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = paletteBackground;

        if (rawImage) rawImage.texture = tex;
        return true;
    }

    // Shrinks nodes when a column gets crowded so dense graphs stay readable.
    private void RecomputeNodeRadius()
    {
        if (graph.Nodes.Count == 0) { computedRadius = maxNodeRadius; return; }

        columnCounts.Clear();
        foreach (NetNode node in graph.Nodes)
        {
            int x = ToPixel(node).x; // nodes in the same column share an X, so this buckets them
            columnCounts.TryGetValue(x, out int c);
            columnCounts[x] = c + 1;
        }

        int maxPerColumn = 1;
        foreach (int c in columnCounts.Values)
            if (c > maxPerColumn) maxPerColumn = c;

        int columns = columnCounts.Count;
        int drawW = texW - 2 * padding;
        int drawH = texH - 2 * padding;

        float vSpacing = maxPerColumn > 1 ? (float)drawH / (maxPerColumn - 1) : drawH;
        float hSpacing = columns > 1 ? (float)drawW / (columns - 1) : drawW;

        int radius = Mathf.RoundToInt(nodeSpacingFraction * Mathf.Min(vSpacing, hSpacing));
        computedRadius = Mathf.Clamp(radius, minNodeRadius, maxNodeRadius);
    }

    private void DrawFrame()
    {
        tex.SetPixels(clearPixels);

        // Connections first (uniform + opaque, so they read as quiet structure and
        // never let the panel behind show through), then nodes on top carry colour.
        foreach (NetEdge edge in graph.Edges)
        {
            NetNode a = graph.Nodes[edge.From];
            NetNode b = graph.Nodes[edge.To];
            DrawLine(ToPixel(a), ToPixel(b), paletteConnection);
        }

        foreach (NetNode node in graph.Nodes)
            DrawCircle(ToPixel(node), computedRadius, NodeColor(node));

        tex.Apply();
    }

    private Vector2Int ToPixel(NetNode n)
    {
        int x = padding + Mathf.RoundToInt(n.X * (texW - 2 * padding));
        int y = padding + Mathf.RoundToInt(n.Y * (texH - 2 * padding));
        return new Vector2Int(x, y);
    }

    private Color NodeColor(NetNode n)
    {
        if (!n.HasActivation) return paletteNodeInactive;
        float a = Mathf.Clamp(n.Activation, -1f, 1f);
        return a >= 0f
            ? Color.Lerp(paletteNodeInactive, paletteNodePositive, a)
            : Color.Lerp(paletteNodeInactive, paletteNodeNegative, -a);
    }

    private void DrawCircle(Vector2Int center, int radius, Color color)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            int px = center.x + dx;
            int py = center.y + dy;
            if (px < 0 || px >= texW || py < 0 || py >= texH) continue;
            tex.SetPixel(px, py, color);
        }
    }

    private void DrawLine(Vector2Int a, Vector2Int b, Color color)
    {
        int steps = Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
        if (steps == 0) return;

        for (int i = 0; i <= steps; i++)
        {
            float t  = (float)i / steps;
            int   px = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
            int   py = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
            if (px < 0 || px >= texW || py < 0 || py >= texH) continue;
            tex.SetPixel(px, py, color);
        }
    }

    private void OnDestroy()
    {
        if (tex) Destroy(tex);
    }
}
