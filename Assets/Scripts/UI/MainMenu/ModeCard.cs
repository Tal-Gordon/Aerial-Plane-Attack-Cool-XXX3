using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One mode tile in <see cref="ModeGridMenu"/>: the track's artwork, its name, a saved-run
/// badge, and the hover/selection state. Built by the menu (see <c>BuildCard</c>) — this
/// owns the wiring and the animation.
///
/// A resting tile sits dimmed and at rest scale. Hovering lifts it; selecting locks it
/// lifted, so the chosen track stays the brightest thing in the grid. The button's own
/// colour transition is switched off in favour of this, since Unity's ColorBlock has no
/// state that means "selected" and its disabled state is doing duty as one.
/// </summary>
[RequireComponent(typeof(Button))]
public class ModeCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const float LiftScale = 1.03f;
    private const float LiftSpeed = 7f; // full travel in about 1/7 s

    private GameModeData data;
    private Button button;
    private GameObject selectionFrame;
    private GameObject savedBadge;
    private Image scrim;
    private Image nameStrip;
    private Action<GameModeData> onSelected;

    private bool selected;
    private bool hovered;
    private float lift; // 0 = resting and dimmed, 1 = lifted and clear

    private static Color RestingScrim => new Color(UITheme.Background.r, UITheme.Background.g, UITheme.Background.b, 0.58f);
    private static Color LiftedScrim => new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.10f);
    private static Color RestingStrip => new Color(UITheme.Background.r, UITheme.Background.g, UITheme.Background.b, 0.86f);
    private static Color LiftedStrip => new Color(UITheme.Panel.r, UITheme.Panel.g, UITheme.Panel.b, 0.94f);

    public GameModeData Data => data;

    public void Init(GameModeData mode, GameObject frame, GameObject badge, Image scrimImage, Image strip,
        Action<GameModeData> selectedCallback)
    {
        data = mode;
        selectionFrame = frame;
        savedBadge = badge;
        scrim = scrimImage;
        nameStrip = strip;
        onSelected = selectedCallback;

        button = GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onSelected?.Invoke(data));

        SetSelected(false);
        SetHasSave(false);
        ApplyLift(); // start resting rather than popping in on the first frame
    }

    /// <summary>Marks this card as the chosen mode. Non-interactable so a re-click is
    /// swallowed, and the lift stays locked in whether or not the pointer is over it.</summary>
    public void SetSelected(bool value)
    {
        selected = value;
        button.interactable = !value;
        if (selectionFrame != null) selectionFrame.SetActive(value);
    }

    /// <summary>Shows the badge marking a track that already has a training save for the
    /// currently selected AI type.</summary>
    public void SetHasSave(bool value)
    {
        if (savedBadge != null) savedBadge.SetActive(value);
    }

    // Pointer events still arrive on a non-interactable button, so the selected card keeps
    // reporting hover — it just has nowhere further to lift.
    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    private void OnDisable() => hovered = false; // no stale hover if the menu is hidden mid-move

    private void Update()
    {
        float target = (selected || hovered) ? 1f : 0f;
        if (Mathf.Approximately(lift, target)) return;

        lift = Mathf.MoveTowards(lift, target, Time.unscaledDeltaTime * LiftSpeed);
        ApplyLift();
    }

    private void ApplyLift()
    {
        float eased = lift * lift * (3f - 2f * lift); // smoothstep — no linear ramp-in/out
        transform.localScale = Vector3.one * Mathf.Lerp(1f, LiftScale, eased);
        if (scrim != null) scrim.color = Color.Lerp(RestingScrim, LiftedScrim, eased);
        if (nameStrip != null) nameStrip.color = Color.Lerp(RestingStrip, LiftedStrip, eased);
    }
}
