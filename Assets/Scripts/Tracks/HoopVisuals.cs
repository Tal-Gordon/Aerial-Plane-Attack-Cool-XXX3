using UnityEngine;

/// <summary>
/// Colours one hoop. <see cref="TrackHoopStyler"/> assigns the sequence colour once at
/// startup; the highlight is toggled per frame as the selected jet advances.
///
/// Colour is driven through emission as well as albedo. A hoop is a thin torus seen at
/// distance against whatever the scene's skybox happens to be — an unlit fill washes out
/// against a bright sky and disappears entirely against a dark one, while emission holds
/// the same apparent colour on any background.
/// </summary>
// No RequireComponent: a hoop's renderer usually sits on a child mesh (ProBuilder keeps
// the geometry under the placed object), so the component goes on the hoop root and
// collects whatever renderers live beneath it.
public class HoopVisuals : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color"); // built-in / older shaders
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private const float FadeSpeed = 6f;

    private Renderer[] hoopRenderers;
    private MaterialPropertyBlock block;

    private Color sequenceColor = Color.white;
    private float emissionStrength = 1f;
    private float dimTarget = 1f;   // 1 = full colour, lower = pushed back
    private float dim = 1f;

    private void Awake() => Init();

    private void Init()
    {
        if (block != null) return; // SetSequenceColor can land before Awake

        block = new MaterialPropertyBlock();
        hoopRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);

        if (hoopRenderers.Length == 0)
            Debug.LogWarning($"[HoopVisuals] '{name}' has no Renderer on it or under it; it can't be coloured.", this);
    }

    /// <summary>Sets this hoop's place-in-the-track colour. Applied immediately.</summary>
    public void SetSequenceColor(Color color, float emission)
    {
        Init(); // the styler colours hoops the same frame it adds them, before Awake runs
        sequenceColor = color;
        emissionStrength = emission;
        Apply();
    }

    /// <summary>Pushes the hoop back (or brings it forward) over the next few frames.
    /// <paramref name="amount"/> is 1 for full colour, lower to recede.</summary>
    public void SetDimTarget(float amount) => dimTarget = amount;

    private void Update()
    {
        if (Mathf.Approximately(dim, dimTarget)) return;
        dim = Mathf.MoveTowards(dim, dimTarget, Time.unscaledDeltaTime * FadeSpeed);
        Apply();
    }

    private void Apply()
    {
        if (hoopRenderers == null) return;

        Color albedo = sequenceColor * dim;
        albedo.a = sequenceColor.a;
        Color emissive = sequenceColor * emissionStrength * dim * dim;

        // A property block rather than renderer.material: the latter instantiates a
        // material per hoop, which on a 20-hoop track means 20 extra materials and 20
        // more draw calls. Property blocks keep the batch intact.
        foreach (Renderer hoopRenderer in hoopRenderers)
        {
            if (hoopRenderer == null) continue;
            hoopRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, albedo);
            block.SetColor(ColorId, albedo);
            block.SetColor(EmissionColorId, emissive);
            hoopRenderer.SetPropertyBlock(block);
        }
    }
}
