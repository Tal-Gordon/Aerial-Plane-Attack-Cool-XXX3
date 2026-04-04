using UnityEngine;
using UnityEngine.UI;

public class BrainVisualizerWidget : UIWidget
{
    [Header("Wiring")]
    [SerializeField] private RawImage rawImage;
    [SerializeField] private GameObject noSelectionOverlay; // optional "no agent selected" label

    [Header("Labels (optional)")]
    [SerializeField] private string[] inputLabels  = { "SALT", "SSPEED", "SHDG" };
    [SerializeField] private string[] outputLabels = { "APITCH", "AROLL", "AYAW", "ATHROTTLE" };

    [Header("Texture Settings")]
    [SerializeField] private int texWidth  = 340;
    [SerializeField] private int texHeight = 200;

    [Header("Visual Config")]
    [SerializeField] private int   nodeRadius       = 10;
    [SerializeField] private int   updateEveryNFrames = 2; // skip frames (lower = better perf)

    // Colours
    [SerializeField] private Color bgColor          = new Color(0.05f, 0.07f, 0.12f, 1f);
    [SerializeField] private Color nodePositive     = new Color(0.2f, 0.9f, 1.0f,  1f); // high activation
    [SerializeField] private Color nodeNeutral      = new Color(0.15f, 0.2f, 0.3f, 1f); // ~zero
    [SerializeField] private Color nodeNegative     = new Color(0.9f, 0.2f, 0.3f,  1f); // negative
    [SerializeField] private Color connectionPos    = new Color(0.2f, 0.8f, 0.4f, 0.5f);
    [SerializeField] private Color connectionNeg    = new Color(0.9f, 0.3f, 0.2f, 0.5f);
    [SerializeField] private Color connectionWeak   = new Color(0.3f, 0.3f, 0.35f, 0.2f);

    // ---- Internal ----
    private Texture2D     tex;
    private Color[]       clearPixels;   // cached blank frame
    private int           frameCounter;

    // Cached node positions [layer][node] = pixel position
    private Vector2Int[][] nodePositions;
    private int[]          cachedShape;

    // Last computed per-layer activations (pulled from brain each frame)
    // We feed the sensor data through manually to get activations.
    // If your NeuroEvoBrain exposes GetActivations(), use that instead.
    private float[][]  activations;

    protected override void OnInitialize()
    {
        tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, mipChain: false)
        {
            filterMode = FilterMode.Bilinear
        };

        clearPixels = new Color[texWidth * texHeight];
        for (int i = 0; i < clearPixels.Length; i++) clearPixels[i] = bgColor;

        if (rawImage) rawImage.texture = tex;
    }

    public override void Tick(SimulationSnapshot snapshot)
    {
        frameCounter++;
        if (frameCounter % updateEveryNFrames != 0) return;

        var agent = snapshot.SelectedAgent;

        bool hasBrain = agent != null && agent.Brain is IEvolvableBrain;
        if (noSelectionOverlay) noSelectionOverlay.SetActive(!hasBrain);
        if (rawImage)           rawImage.gameObject.SetActive(hasBrain);

        if (!hasBrain) return;

        var brain = (IEvolvableBrain)agent.Brain;
        int[] shape = brain.GetShape();

        // Rebuild node positions only when topology changes (e.g. new agent selected)
        if (!ShapeEquals(shape, cachedShape))
        {
            cachedShape   = shape;
            nodePositions = BuildNodePositions(shape);
        }

        // Get current sensor data and run a forward pass to collect activations
        // This assumes JetAgent exposes its ISensor via a getter.
        // If not, we fall back to drawing without live activations.
        float[] inputs = TryGetSensorData(agent);
        activations   = ComputeActivations(brain, shape, inputs);

        DrawFrame(shape);
    }

    private Vector2Int[][] BuildNodePositions(int[] shape)
    {
        int layers = shape.Length;
        var positions = new Vector2Int[layers][];

        int padX = 30;
        int padY = 20;

        for (int l = 0; l < layers; l++)
        {
            positions[l] = new Vector2Int[shape[l]];
            float xFraction = layers == 1 ? 0.5f : (float)l / (layers - 1);
            int   x = padX + Mathf.RoundToInt(xFraction * (texWidth - padX * 2));

            for (int n = 0; n < shape[l]; n++)
            {
                float yFraction = shape[l] == 1 ? 0.5f : (float)n / (shape[l] - 1);
                int   y = padY + Mathf.RoundToInt(yFraction * (texHeight - padY * 2));
                positions[l][n] = new Vector2Int(x, y);
            }
        }

        return positions;
    }

    private void DrawFrame(int[] shape)
    {
        // Clear
        tex.SetPixels(clearPixels);

        // Connections (draw before nodes so nodes render on top)
        for (int l = 0; l < shape.Length - 1; l++)
        {
            for (int n = 0; n < shape[l]; n++)
            {
                for (int m = 0; m < shape[l + 1]; m++)
                {
                    // We don't have easy per-weight access via the interface,
                    // so colour connections by the source node's activation.
                    float activation = activations != null ? activations[l][n] : 0f;
                    Color lineColor  = activation > 0.1f  ? connectionPos
                                    : activation < -0.1f ? connectionNeg
                                    : connectionWeak;

                    DrawLine(nodePositions[l][n], nodePositions[l + 1][m], lineColor);
                }
            }
        }

        // Nodes
        for (int l = 0; l < shape.Length; l++)
        {
            for (int n = 0; n < shape[l]; n++)
            {
                float activation = activations != null ? activations[l][n] : 0f;

                // Lerp: negative → neutral → positive
                Color nodeColor = activation >= 0f
                    ? Color.Lerp(nodeNeutral, nodePositive, activation)
                    : Color.Lerp(nodeNeutral, nodeNegative, -activation);

                DrawCircle(nodePositions[l][n], nodeRadius, nodeColor);
            }
        }

        tex.Apply();
    }

    private void DrawCircle(Vector2Int center, int radius, Color color)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx * dx + dy * dy > radius * radius) continue;
            int px = center.x + dx;
            int py = center.y + dy;
            if (px < 0 || px >= texWidth || py < 0 || py >= texHeight) continue;
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
            if (px < 0 || px >= texWidth || py < 0 || py >= texHeight) continue;
            tex.SetPixel(px, py, color);
        }
    }

    // Runs the sensor data through the network layer by layer,
    // capturing the activation at each node.
    // Requires NeuroEvoBrain (or any IEvolvableBrain) to support GetControlOutputs.
    // We reconstruct activations using the same forward pass logic.
    private float[][] ComputeActivations(IEvolvableBrain brain, int[] shape, float[] inputs)
    {
        if (inputs == null || inputs.Length == 0)
            return null;

        // We call GetControlOutputs to get the final outputs,
        // but for intermediate activations we need to re-implement
        // the forward pass here OR expose GetLayerActivations() on NeuroEvoBrain.
        //
        // Simplest approach: store input layer activations; use output for last layer;
        // interpolate middles. For a proper implementation, add this to NeuroEvoBrain:
        //   public float[][] GetLayerActivations(float[] inputs) { ... }
        // and call it here instead.

        var activations = new float[shape.Length][];

        // Input layer — clamp to [-1, 1] range for colour purposes
        activations[0] = new float[shape[0]];
        for (int i = 0; i < Mathf.Min(inputs.Length, shape[0]); i++)
            activations[0][i] = Mathf.Clamp(inputs[i], -1f, 1f);

        // Intermediate layers — zeroed until you expose GetLayerActivations
        for (int l = 1; l < shape.Length - 1; l++)
            activations[l] = new float[shape[l]]; // all zero until exposed

        // Output layer — run the actual forward pass
        float[] outputs = brain.GetControlOutputs(inputs);
        activations[shape.Length - 1] = new float[shape[shape.Length - 1]];
        for (int i = 0; i < Mathf.Min(outputs.Length, shape[shape.Length - 1]); i++)
            activations[shape.Length - 1][i] = Mathf.Clamp(outputs[i], -1f, 1f);

        return activations;
    }

    // TODO: Replace ComputeActivations with this once NeuroEvoBrain exposes GetLayerActivations()
    // private float[][] ComputeActivations(IEvolvableBrain brain, int[] shape, float[] inputs)
    // {
    //     if (inputs == null || inputs.Length == 0) return null;

    //     // Cast the interface to our specific brain script
    //     var myBrain = brain as NeuroEvoBrain; 
        
    //     if (myBrain != null)
    //     {
    //         // Now we are getting the real data for every single node!
    //         return myBrain.GetLayerActivations(inputs); 
    //     }

    //     return null;
    // }

    // Try to pull sensor data from the agent.
    private float[] TryGetSensorData(JetAgent agent)
    {
        return agent.Sensor?.GetObservationData();

        // fallback:
        // var sensor = agent.GetComponent<ISensor>() as MonoBehaviour;
        // return sensor != null
        //     ? (sensor as ISensor)?.GetObservationData()
        //     : null;
        //
        // Not recommended, slow
        // But will work in case of null sensor errors and such
    }

    private bool ShapeEquals(int[] a, int[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private void OnDestroy()
    {
        if (tex) Destroy(tex);
    }
}