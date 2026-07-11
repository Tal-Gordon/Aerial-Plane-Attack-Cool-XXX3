using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public GameObject buttons;
    public GameObject selectionWindow;

    private GameModeSelectionController selectionController;

    private void Awake()
    {
        selectionController = GetComponent<GameModeSelectionController>();

        // Theme the whole menu canvas (title, buttons, selection window) in place —
        // the scene keeps its default visuals in the editor, UITheme restyles at runtime.
        Transform themeRoot = buttons != null ? buttons.transform.root : transform.root;
        UITheme.Skin(themeRoot.gameObject);
        BuildMenuDressing(themeRoot);
    }

    // Code-built dressing the bare scene lacks: a soft background gradient, a title
    // block, and the accent Play button. Additive only — nothing scene-authored is
    // moved or removed, so the scene stays editable as before.
    private void BuildMenuDressing(Transform themeRoot)
    {
        Canvas canvas = themeRoot.GetComponentInChildren<Canvas>();
        if (canvas == null) return;
        Transform parent = canvas.transform;

        // Background: reuse the scene's full-screen backdrop image if there is one
        // (skinned flat Panel otherwise), else add our own behind everything.
        Sprite gradient = UITheme.CreateVerticalGradientSprite(
            new Color(0.045f, 0.055f, 0.075f, 1f), new Color(0.10f, 0.12f, 0.16f, 1f));
        Image backdrop = FindFullScreenImage(parent);
        if (backdrop == null)
        {
            backdrop = new GameObject("BackgroundGradient", typeof(RectTransform)).AddComponent<Image>();
            backdrop.transform.SetParent(parent, worldPositionStays: false);
            backdrop.transform.SetSiblingIndex(0);
            var rt = backdrop.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        backdrop.sprite = gradient;
        backdrop.type = Image.Type.Simple;
        backdrop.color = Color.white;

        // Title block, inserted just above the backdrop in draw order so the
        // selection window (a later sibling) still covers it when open.
        var titleRoot = new GameObject("TitleBlock", typeof(RectTransform));
        titleRoot.transform.SetParent(parent, worldPositionStays: false);
        titleRoot.transform.SetSiblingIndex(backdrop.transform.GetSiblingIndex() + 1);
        var titleRt = (RectTransform)titleRoot.transform;
        titleRt.anchorMin = titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.anchoredPosition = new Vector2(0f, -120f);
        titleRt.sizeDelta = new Vector2(1400f, 200f);

        TextMeshProUGUI title = AddLabel(titleRoot.transform, "GameTitle", "AERIAL PLANE ATTACK",
            64f, UITheme.TextColor, FontStyles.Bold);
        title.characterSpacing = 10f;
        Place(title.rectTransform, new Vector2(1400f, 80f), new Vector2(0f, 0f));

        var underline = new GameObject("TitleUnderline", typeof(RectTransform)).AddComponent<Image>();
        underline.transform.SetParent(titleRoot.transform, worldPositionStays: false);
        underline.color = UITheme.Accent;
        Place(underline.rectTransform, new Vector2(320f, 3f), new Vector2(0f, -92f));

        TextMeshProUGUI subtitle = AddLabel(titleRoot.transform, "GameSubtitle",
            "AI  FLIGHT  TRAINING  SIMULATOR", 20f, UITheme.TextDimmed, FontStyles.Normal);
        subtitle.characterSpacing = 6f;
        Place(subtitle.rectTransform, new Vector2(1400f, 32f), new Vector2(0f, -122f));

        // Play is the menu's call-to-action; give the button column a little air.
        if (buttons != null)
        {
            foreach (Button button in buttons.GetComponentsInChildren<Button>(includeInactive: true))
                if (button.name == "Play") { UITheme.StylePrimary(button); break; }

            var layout = buttons.GetComponent<VerticalLayoutGroup>();
            if (layout != null && layout.spacing < 14f) layout.spacing = 14f;
        }
    }

    private static Image FindFullScreenImage(Transform parent)
    {
        foreach (Image img in parent.GetComponentsInChildren<Image>()) // active only — skips the closed selection window
        {
            if (img.transform.parent != parent) continue; // direct children only — a stretched image deep inside a panel is not the backdrop
            RectTransform rt = img.rectTransform;
            if (rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one && img.sprite == null)
                return img;
        }
        return null;
    }

    private static TextMeshProUGUI AddLabel(Transform parent, string name, string text,
        float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    // Anchored to the top-centre of the parent block.
    private static void Place(RectTransform rt, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
    }

    public void ToggleSelectionMenu()
    {
        selectionWindow.SetActive(!selectionWindow.activeSelf);
        
        // Reset the mode selection whenever the menu is toggled
        if (selectionController != null)
        {
            selectionController.ResetSelection();
        }
    }

    private void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void OpenSettings()
    {
        if (SettingsMenu.Instance != null)
            SettingsMenu.Instance.Open();
        else
            Debug.LogWarning("[MainMenuController] SettingsMenu not available.");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
