using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Continue training?" modal — shown when the chosen (track, AI type) already has a
/// saved run, so Play doesn't silently discard it. Built in code in the SettingsMenu
/// style: full-screen dim (click to cancel), centred panel with an accent top stroke.
///
/// Create it with <see cref="Attach"/> and drive it with <see cref="Show"/>; the caller
/// supplies what happens on each choice, so this knows nothing about scene loading.
/// </summary>
public class ContinueTrainingDialog : MonoBehaviour
{
    private TextMeshProUGUI body;
    private Action onContinue;
    private Action onStartFresh;

    /// <summary>Builds the dialog under <paramref name="canvasRoot"/>, hidden. Returns null
    /// if there is no canvas to host it — callers should fall back to loading fresh.</summary>
    public static ContinueTrainingDialog Attach(Transform canvasRoot)
    {
        if (canvasRoot == null) return null;

        var root = new GameObject("ContinueDialog", typeof(RectTransform));
        root.transform.SetParent(canvasRoot, worldPositionStays: false);
        MenuUI.Stretch((RectTransform)root.transform);
        var dialog = root.AddComponent<ContinueTrainingDialog>();

        Image dim = MenuUI.Panel(root.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
        MenuUI.Stretch(dim.rectTransform);
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.targetGraphic = dim;
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(dialog.Hide);

        Image panel = MenuUI.Rounded(MenuUI.Panel(root.transform, "Panel", UITheme.Panel), 12);
        MenuUI.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(660f, 300f), Vector2.zero);

        Image accent = MenuUI.Panel(panel.transform, "AccentLine", UITheme.Accent);
        MenuUI.Band(accent.rectTransform, 1f, 2f);

        TextMeshProUGUI title = MenuUI.Label(panel.transform, "Title", "Continue training?", 30f,
            UITheme.Accent, TextAlignmentOptions.Center, FontStyles.Bold);
        MenuUI.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(600f, 44f), new Vector2(0f, -30f));

        dialog.body = MenuUI.Label(panel.transform, "Body", string.Empty, 20f,
            UITheme.TextColor, TextAlignmentOptions.Top);
        MenuUI.Place(dialog.body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(560f, 110f), new Vector2(0f, -90f));

        Button continueButton = MenuUI.TextButton(panel.transform, "ContinueButton", "Continue save", 20f);
        MenuUI.Place(continueButton.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(270f, 52f), new Vector2(-150f, 36f));
        UITheme.StylePrimary(continueButton); // resuming progress is the safe default
        continueButton.onClick.AddListener(() => dialog.Choose(dialog.onContinue));

        Button freshButton = MenuUI.TextButton(panel.transform, "FreshButton", "Start from default", 20f);
        MenuUI.Place(freshButton.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(270f, 52f), new Vector2(150f, 36f));
        freshButton.onClick.AddListener(() => dialog.Choose(dialog.onStartFresh));

        root.SetActive(false);
        return dialog;
    }

    public void Show(string message, Action continueSave, Action startFresh)
    {
        onContinue = continueSave;
        onStartFresh = startFresh;
        body.text = message;

        transform.SetAsLastSibling(); // above the menu, whatever else was added later
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    private void Choose(Action action)
    {
        Hide();
        action?.Invoke();
    }
}
