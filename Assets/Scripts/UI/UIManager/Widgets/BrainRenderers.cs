using System.Collections.Generic;
using UnityEngine;
using SharpNeat.Genomes.Neat;
using SharpNeat.Network;

// ─────────────────────────────────────────────────────────────────────────────
// Shared graph model + per-AI renderers for BrainVisualizerWidget.
//
// The widget owns the texture and the drawing primitives; it knows nothing about
// any particular AI. Each BrainRenderer translates one brain type into a
// paradigm-agnostic NetworkGraph (normalized node positions + activations, and
// edges with optional weights), which the widget then rasterizes.
//
// Adding a new AI = adding a new BrainRenderer and listing it in the widget.
// ─────────────────────────────────────────────────────────────────────────────

public enum NetNodeRole { Input, Hidden, Output, Bias }

/// Reference type on purpose: renderers mutate Activation/Weight in place each
/// frame after the topology has been built once.
public class NetNode
{
    public float X;             // normalized 0..1 (0 = input column, 1 = output column)
    public float Y;             // normalized 0..1
    public float Activation;    // -1..1, used for colour
    public bool HasActivation;  // false => draw neutral (e.g. a NEAT hidden node we can't read)
    public NetNodeRole Role;
}

public class NetEdge
{
    public int From;            // index into NetworkGraph.Nodes
    public int To;
    public float Weight;        // signed connection weight (used for colour)
    public bool HasWeight;      // false => colour by source signal / faint (RL has no readable weights)
    public float SourceSignal;  // source node activation, optional flow hint
}

public class NetworkGraph
{
    public readonly List<NetNode> Nodes = new List<NetNode>();
    public readonly List<NetEdge> Edges = new List<NetEdge>();
    public readonly List<NetLayerAnnotation> LayerAnnotations = new List<NetLayerAnnotation>();

    public void Clear()
    {
        Nodes.Clear();
        Edges.Clear();
        LayerAnnotations.Clear();
    }
}

/// Display-only metadata for a network column. BrainVisualizerWidget renders these
/// as UI labels over the texture, so a compact layer can still report its real size.
public class NetLayerAnnotation
{
    public float X;
    public string Text;
}

public abstract class BrainRenderer
{
    /// True when this renderer knows how to draw the given agent's brain.
    public abstract bool Matches(JetAgent agent);

    /// A value that changes whenever the network's STRUCTURE changes, so the widget
    /// knows when to rebuild the (relatively expensive) layout. Reference- or
    /// value-comparable via object.Equals.
    public abstract object TopologyToken(JetAgent agent);

    /// Build nodes (with normalized positions) and edges. Called only when the
    /// topology token changes.
    public abstract void BuildTopology(JetAgent agent, NetworkGraph graph);

    /// Refresh per-frame data (node activations, and for fixed-topology nets the
    /// live weights) on the already-built graph.
    public abstract void SampleActivations(JetAgent agent, NetworkGraph graph);

    /// Maps an unbounded value into (-1,1) for stable colouring (ReLU activations
    /// and raw weights can be large; tanh/sigmoid outputs already sit in range).
    protected static float Squash(float x) => x / (1f + Mathf.Abs(x));
}

// ─────────────────────────────────────────────────────────────────────────────
// FixedNeuroEvo — fully-connected MLP, fixed shape. We can read real per-layer
// activations and real weights, so this is the richest view.
// ─────────────────────────────────────────────────────────────────────────────
public class NeuroEvoBrainRenderer : BrainRenderer
{
    public override bool Matches(JetAgent agent) => agent.Brain is NeuroEvoBrain;

    public override object TopologyToken(JetAgent agent)
    {
        // Shape is fixed for the whole run, so the layout is built once. (Weights
        // mutate every generation but those are refreshed in SampleActivations,
        // because ClassicNeuroEvoEngine reuses the same brain instances in place.)
        int[] shape = ((NeuroEvoBrain)agent.Brain).GetShape();
        return string.Join(",", shape);
    }

    public override void BuildTopology(JetAgent agent, NetworkGraph graph)
    {
        var brain = (NeuroEvoBrain)agent.Brain;
        int[] shape = brain.GetShape();
        int layers = shape.Length;

        // First node index of each layer, so edges can address nodes globally.
        int[] offset = new int[layers];
        int running = 0;
        for (int l = 0; l < layers; l++) { offset[l] = running; running += shape[l]; }

        for (int l = 0; l < layers; l++)
        {
            float x = layers == 1 ? 0.5f : (float)l / (layers - 1);
            NetNodeRole role = l == 0 ? NetNodeRole.Input
                             : l == layers - 1 ? NetNodeRole.Output
                             : NetNodeRole.Hidden;

            for (int n = 0; n < shape[l]; n++)
            {
                float y = shape[l] == 1 ? 0.5f : (float)n / (shape[l] - 1);
                graph.Nodes.Add(new NetNode { X = x, Y = y, Role = role });
            }
        }

        for (int l = 0; l < layers - 1; l++)
            for (int from = 0; from < shape[l]; from++)
                for (int to = 0; to < shape[l + 1]; to++)
                    graph.Edges.Add(new NetEdge
                    {
                        From = offset[l] + from,
                        To = offset[l + 1] + to,
                        HasWeight = true,
                    });
    }

    public override void SampleActivations(JetAgent agent, NetworkGraph graph)
    {
        var brain = (NeuroEvoBrain)agent.Brain;
        int[] shape = brain.GetShape();

        // Live weights (they change each generation; the instance is reused).
        float[][][] weights = brain.GetWeights();
        int e = 0;
        for (int l = 0; l < shape.Length - 1 && e < graph.Edges.Count; l++)
            for (int from = 0; from < shape[l]; from++)
                for (int to = 0; to < shape[l + 1] && e < graph.Edges.Count; to++)
                    graph.Edges[e++].Weight = weights[l][from][to];

        // Live activations from a read-only forward pass.
        float[] inputs = agent.Sensor?.GetObservationData();
        if (inputs == null) return;

        float[][] acts = brain.GetLayerActivations(inputs);
        int idx = 0;
        for (int l = 0; l < shape.Length; l++)
            for (int n = 0; n < shape[l]; n++)
            {
                if (idx >= graph.Nodes.Count) return;
                float v = (l < acts.Length && n < acts[l].Length) ? acts[l][n] : 0f;
                NetNode node = graph.Nodes[idx++];
                node.Activation = Squash(v);
                node.HasActivation = true;
            }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// NEAT — arbitrary directed graph that GROWS over generations (add-node /
// add-connection mutations). Topology and the number of "layers" change, so the
// layout is recomputed from the genome whenever it changes. We have real
// connection weights; hidden-node activations aren't exposed by the black box, so
// only input/output/bias nodes are coloured.
// ─────────────────────────────────────────────────────────────────────────────
public class NeatBrainRenderer : BrainRenderer
{
    public override bool Matches(JetAgent agent) => agent.Brain is NeatBrain;

    public override object TopologyToken(JetAgent agent)
    {
        // Offspring are fresh NeatGenome objects; elites are carried over by
        // reference. Reference identity therefore tracks structural change exactly,
        // and rebuilds only when this jet actually gets a new/mutated genome.
        return ((NeatBrain)agent.Brain).Genome;
    }

    public override void BuildTopology(JetAgent agent, NetworkGraph graph)
    {
        NeatGenome genome = ((NeatBrain)agent.Brain).Genome;
        NeuronGeneList neurons = genome.NeuronGeneList;
        ConnectionGeneList conns = genome.ConnectionGeneList;
        int n = neurons.Count;

        var idToIndex = new Dictionary<uint, int>(n);
        var roles = new NetNodeRole[n];
        for (int i = 0; i < n; i++)
        {
            idToIndex[neurons[i].Id] = i;
            roles[i] = MapRole(neurons[i].NodeType);
        }

        // Assign each node a column via longest-path depth from the inputs. The
        // network can be cyclic (CyclicFixedTimestepsScheme), so we cap the
        // relaxation at n passes — that bounds any cycle's contribution instead of
        // looping forever, which is fine for layout purposes.
        int[] depth = new int[n];
        for (int pass = 0; pass < n; pass++)
        {
            bool changed = false;
            for (int c = 0; c < conns.Count; c++)
            {
                if (!idToIndex.TryGetValue(conns[c].SourceNodeId, out int si)) continue;
                if (!idToIndex.TryGetValue(conns[c].TargetNodeId, out int ti)) continue;
                if (roles[ti] == NetNodeRole.Input || roles[ti] == NetNodeRole.Bias) continue;
                if (depth[ti] < depth[si] + 1) { depth[ti] = depth[si] + 1; changed = true; }
            }
            if (!changed) break;
        }

        int maxHidden = 0;
        for (int i = 0; i < n; i++)
            if (roles[i] == NetNodeRole.Hidden && depth[i] > maxHidden) maxHidden = depth[i];
        int outputCol = maxHidden + 1; // outputs always sit one column past the deepest hidden

        int[] col = new int[n];
        for (int i = 0; i < n; i++)
        {
            switch (roles[i])
            {
                case NetNodeRole.Input:
                case NetNodeRole.Bias:
                    col[i] = 0;
                    break;
                case NetNodeRole.Output:
                    col[i] = outputCol;
                    break;
                default: // Hidden — keep it strictly between inputs and outputs
                    col[i] = Mathf.Clamp(depth[i], 1, Mathf.Max(1, maxHidden));
                    break;
            }
        }

        // Spread nodes that share a column evenly along Y, in stable genome order.
        var perColTotal = new Dictionary<int, int>();
        for (int i = 0; i < n; i++)
        {
            perColTotal.TryGetValue(col[i], out int t);
            perColTotal[col[i]] = t + 1;
        }
        var perColPlaced = new Dictionary<int, int>();
        for (int i = 0; i < n; i++)
        {
            int total = perColTotal[col[i]];
            perColPlaced.TryGetValue(col[i], out int k);
            perColPlaced[col[i]] = k + 1;

            float x = outputCol == 0 ? 0.5f : (float)col[i] / outputCol;
            float y = total == 1 ? 0.5f : (float)k / (total - 1);
            graph.Nodes.Add(new NetNode { X = x, Y = y, Role = roles[i] });
        }

        for (int c = 0; c < conns.Count; c++)
        {
            if (!idToIndex.TryGetValue(conns[c].SourceNodeId, out int si)) continue;
            if (!idToIndex.TryGetValue(conns[c].TargetNodeId, out int ti)) continue;
            graph.Edges.Add(new NetEdge
            {
                From = si,
                To = ti,
                Weight = (float)conns[c].Weight,
                HasWeight = true,
            });
        }
    }

    public override void SampleActivations(JetAgent agent, NetworkGraph graph)
    {
        var brain = (NeatBrain)agent.Brain;
        NeuronGeneList neurons = brain.Genome.NeuronGeneList;

        float[] inputs = agent.Sensor?.GetObservationData();
        float[] outputs = brain.LastOutputs; // already remapped to (-1,1), no re-activation

        // NeuronGeneList order is [bias, inputs..., outputs..., hidden...]; the i-th
        // Input node lines up with InputSignalArray[i] (= observation i) and the
        // i-th Output node with LastOutputs[i].
        int inIdx = 0, outIdx = 0;
        int count = Mathf.Min(neurons.Count, graph.Nodes.Count);
        for (int i = 0; i < count; i++)
        {
            NetNode node = graph.Nodes[i];
            switch (node.Role)
            {
                case NetNodeRole.Input:
                    if (inputs != null && inIdx < inputs.Length)
                    {
                        node.Activation = Squash(inputs[inIdx]);
                        node.HasActivation = true;
                    }
                    inIdx++;
                    break;
                case NetNodeRole.Output:
                    if (outputs != null && outIdx < outputs.Length)
                    {
                        node.Activation = Mathf.Clamp(outputs[outIdx], -1f, 1f);
                        node.HasActivation = true;
                    }
                    outIdx++;
                    break;
                case NetNodeRole.Bias:
                    node.Activation = 1f;
                    node.HasActivation = true;
                    break;
                // Hidden nodes stay neutral — the black box doesn't expose them.
            }
        }
    }

    private static NetNodeRole MapRole(NodeType type)
    {
        switch (type)
        {
            case NodeType.Input: return NetNodeRole.Input;
            case NodeType.Output: return NetNodeRole.Output;
            case NodeType.Bias: return NetNodeRole.Bias;
            default: return NetNodeRole.Hidden;
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// PPO / SAC (ML-Agents) — the policy network lives in the external Python trainer
// and is unreachable in-process. Its configured architecture is still available,
// so hidden layers are shown as compact representative columns with exact size
// labels. Only input/output activations are live; weights remain unavailable.
// ─────────────────────────────────────────────────────────────────────────────
public class RLBrainRenderer : BrainRenderer
{
    private const int MaxRepresentativeNodesPerHiddenLayer = 16;

    public override bool Matches(JetAgent agent) => agent.GetComponent<JetMLAgent>() != null;

    public override object TopologyToken(JetAgent agent)
    {
        int inN = agent.Sensor?.GetObservationData()?.Length ?? 0;
        int outN = agent.GetComponent<JetMLAgent>().LastActions?.Length ?? 0;
        List<int> hidden = GetConfiguredHiddenLayers();
        return $"rl:{inN}:{string.Join(",", hidden)}:{outN}";
    }

    public override void BuildTopology(JetAgent agent, NetworkGraph graph)
    {
        int inN = agent.Sensor?.GetObservationData()?.Length ?? 0;
        int outN = agent.GetComponent<JetMLAgent>().LastActions?.Length ?? 0;
        List<int> hidden = GetConfiguredHiddenLayers();
        int layerCount = hidden.Count + 2;

        var starts = new int[layerCount];
        var counts = new int[layerCount];

        starts[0] = graph.Nodes.Count;
        counts[0] = inN;
        for (int i = 0; i < inN; i++)
        {
            float y = inN == 1 ? 0.5f : (float)i / (inN - 1);
            graph.Nodes.Add(new NetNode { X = 0f, Y = y, Role = NetNodeRole.Input });
        }

        for (int layer = 0; layer < hidden.Count; layer++)
        {
            int configuredUnits = Mathf.Max(1, hidden[layer]);
            int shownUnits = Mathf.Min(configuredUnits, MaxRepresentativeNodesPerHiddenLayer);
            float x = (float)(layer + 1) / (layerCount - 1);

            starts[layer + 1] = graph.Nodes.Count;
            counts[layer + 1] = shownUnits;
            for (int n = 0; n < shownUnits; n++)
            {
                float y = shownUnits == 1 ? 0.5f : (float)n / (shownUnits - 1);
                graph.Nodes.Add(new NetNode
                {
                    X = x,
                    Y = y,
                    Role = NetNodeRole.Hidden,
                    HasActivation = false,
                });
            }

            graph.LayerAnnotations.Add(new NetLayerAnnotation
            {
                X = x,
                Text = $"{configuredUnits} units",
            });
        }

        int outputLayer = layerCount - 1;
        starts[outputLayer] = graph.Nodes.Count;
        counts[outputLayer] = outN;
        for (int o = 0; o < outN; o++)
        {
            float y = outN == 1 ? 0.5f : (float)o / (outN - 1);
            graph.Nodes.Add(new NetNode { X = 1f, Y = y, Role = NetNodeRole.Output });
        }

        // Sparse structural links show layer order without claiming to be weights.
        for (int layer = 0; layer < layerCount - 1; layer++)
            ConnectRepresentativeColumns(graph,
                starts[layer], counts[layer], starts[layer + 1], counts[layer + 1]);
    }

    public override void SampleActivations(JetAgent agent, NetworkGraph graph)
    {
        float[] inputs = agent.Sensor?.GetObservationData();
        float[] outputs = agent.GetComponent<JetMLAgent>().LastActions;

        int inIdx = 0, outIdx = 0;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            NetNode node = graph.Nodes[i];
            if (node.Role == NetNodeRole.Input)
            {
                if (inputs != null && inIdx < inputs.Length)
                {
                    node.Activation = Squash(inputs[inIdx]);
                    node.HasActivation = true;
                }
                inIdx++;
            }
            else if (node.Role == NetNodeRole.Output)
            {
                if (outputs != null && outIdx < outputs.Length)
                {
                    node.Activation = Mathf.Clamp(outputs[outIdx], -1f, 1f);
                    node.HasActivation = true;
                }
                outIdx++;
            }
        }
    }

    private static List<int> GetConfiguredHiddenLayers()
    {
        NetworkShapeController shape = ParameterTuners.NetworkShape;
        return shape != null ? shape.GetHiddenLayers() : new List<int>();
    }

    // These sparse edges show data flow between configured layers. They are not
    // presented as policy weights, which remain inside the external trainer.
    private static void ConnectRepresentativeColumns(NetworkGraph graph,
        int fromStart, int fromCount, int toStart, int toCount)
    {
        if (fromCount <= 0 || toCount <= 0) return;

        for (int i = 0; i < fromCount; i++)
        {
            int mapped = MapIndex(i, fromCount, toCount);
            graph.Edges.Add(new NetEdge
            {
                From = fromStart + i,
                To = toStart + mapped,
                HasWeight = false,
            });
        }

        // Cover the target side as well when the next column is wider.
        for (int i = 0; i < toCount; i++)
        {
            int mapped = MapIndex(i, toCount, fromCount);
            bool duplicate = MapIndex(mapped, fromCount, toCount) == i;
            if (!duplicate)
                graph.Edges.Add(new NetEdge
                {
                    From = fromStart + mapped,
                    To = toStart + i,
                    HasWeight = false,
                });
        }
    }

    private static int MapIndex(int index, int sourceCount, int targetCount)
    {
        if (sourceCount <= 1 || targetCount <= 1) return 0;
        return Mathf.RoundToInt((float)index / (sourceCount - 1) * (targetCount - 1));
    }
}
