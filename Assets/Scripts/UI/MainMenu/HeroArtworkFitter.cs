using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Sizes an Image to <em>cover</em> its parent rect without distorting the sprite —
/// whatever overflows is cropped by the parent's RectMask2D.
///
/// Unity's own <c>Image.preserveAspect</c> does the opposite (fits the sprite inside
/// the rect), which leaves dead bands above and below a 16:9 screenshot in a squarer
/// panel. Stretching instead would fill the panel but squash the picture.
/// </summary>
[RequireComponent(typeof(Image))]
[DisallowMultipleComponent]
public class HeroArtworkFitter : MonoBehaviour
{
    private Image image;
    private RectTransform rect;
    private RectTransform area; // the masked parent we're filling

    private Vector2 fittedTo = NeverFitted;
    private Sprite fittedSprite;

    private static readonly Vector2 NeverFitted = new Vector2(float.NaN, float.NaN);

    private void Awake()
    {
        image = GetComponent<Image>();
        rect = (RectTransform)transform;
        area = transform.parent as RectTransform;
    }

    private void OnEnable() => fittedTo = NeverFitted;

    // Polled rather than driven by OnRectTransformDimensionsChange. This component writes
    // to its own rect, and Unity raises that callback synchronously on every write — with
    // offsetMin and offsetMax being two separate writes, the callback re-enters mid-update
    // and never settles, which hangs the editor outright. Comparing two floats a frame is
    // the cheaper problem.
    private void LateUpdate() => Refit();

    /// <summary>Re-fits if the parent's size or the sprite changed. Safe to call whenever;
    /// it's a no-op when nothing moved.</summary>
    public void Refit()
    {
        if (image == null || area == null) return; // called before Awake
        if (image.sprite == null) return;

        Vector2 available = area.rect.size;
        if (available.x <= 0f || available.y <= 0f) return; // layout hasn't run yet
        if (available == fittedTo && image.sprite == fittedSprite) return;

        fittedTo = available;
        fittedSprite = image.sprite;

        float spriteAspect = image.sprite.rect.width / image.sprite.rect.height;

        // Match the axis that would otherwise leave a gap; the mask eats the rest.
        Vector2 size = available.x / available.y < spriteAspect
            ? new Vector2(available.y * spriteAspect, available.y) // crop the sides
            : new Vector2(available.x, available.x / spriteAspect); // crop top and bottom

        // Stretch anchors with a symmetric overhang, so the rect keeps tracking the parent.
        Vector2 overflow = (size - available) * 0.5f;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = -overflow;
        rect.offsetMax = overflow;
    }
}
