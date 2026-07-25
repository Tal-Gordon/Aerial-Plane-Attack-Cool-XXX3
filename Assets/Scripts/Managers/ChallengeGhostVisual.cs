using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gives the saved-policy jet a translucent cyan ghost treatment so it can share
/// the player's exact spawn without becoming visually indistinguishable.
/// Source material assets are never modified.
/// </summary>
public sealed class ChallengeGhostVisual : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    private static readonly int ModeId = Shader.PropertyToID("_Mode");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    private readonly List<Material> instances = new();

    public static ChallengeGhostVisual Apply(GameObject jet, float alpha = 0.38f)
    {
        if (jet == null) return null;
        ChallengeGhostVisual ghost = jet.GetComponent<ChallengeGhostVisual>();
        if (ghost == null) ghost = jet.AddComponent<ChallengeGhostVisual>();
        ghost.Configure(Mathf.Clamp(alpha, 0.1f, 0.8f));
        return ghost;
    }

    private void Configure(float alpha)
    {
        ClearMaterials();

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            Material[] source = renderer.sharedMaterials;
            Material[] ghostMaterials = new Material[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                Material original = source[i];
                if (original == null) continue;

                Material material = new Material(original)
                {
                    name = original.name + " (Challenge Ghost)",
                    renderQueue = (int)RenderQueue.Transparent,
                };
                ConfigureTransparentMaterial(material, alpha);
                instances.Add(material);
                ghostMaterials[i] = material;
            }

            renderer.sharedMaterials = ghostMaterials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static void ConfigureTransparentMaterial(Material material, float alpha)
    {
        if (material.HasProperty(BaseColorId))
        {
            Color color = material.GetColor(BaseColorId);
            color = Color.Lerp(color, new Color(0.35f, 0.78f, 1f, alpha), 0.35f);
            color.a = alpha;
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            Color color = material.GetColor(ColorId);
            color = Color.Lerp(color, new Color(0.35f, 0.78f, 1f, alpha), 0.35f);
            color.a = alpha;
            material.SetColor(ColorId, color);
        }

        // URP Lit.
        if (material.HasProperty(SurfaceId)) material.SetFloat(SurfaceId, 1f);

        // Built-in Standard fallback.
        if (material.HasProperty(ModeId)) material.SetFloat(ModeId, 3f);

        if (material.HasProperty(SrcBlendId))
            material.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
        if (material.HasProperty(DstBlendId))
            material.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty(ZWriteId)) material.SetFloat(ZWriteId, 0f);

        material.SetOverrideTag("RenderType", "Transparent");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private void OnDestroy() => ClearMaterials();

    private void ClearMaterials()
    {
        foreach (Material material in instances)
            if (material != null) Destroy(material);
        instances.Clear();
    }
}
