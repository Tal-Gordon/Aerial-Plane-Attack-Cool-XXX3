using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Code-built telemetry entry placed immediately after Inference.</summary>
public sealed class ChallengeButton : MonoBehaviour
{
    private UIManager manager;
    private Button button;
    private TextMeshProUGUI label;
    private bool? lastAvailable;

    public static void Install(UIManager manager, Transform fallbackParent)
    {
        if (manager == null || FindFirstObjectByType<ChallengeButton>() != null) return;

        InferenceToggleWidget inference = FindFirstObjectByType<InferenceToggleWidget>(
            FindObjectsInactive.Include);
        Transform parent = inference != null ? inference.transform.parent : fallbackParent;
        if (parent == null) return;

        var root = new GameObject("ChallengeBestRun", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        if (inference != null)
            root.transform.SetSiblingIndex(inference.transform.GetSiblingIndex() + 1);

        Image image = root.AddComponent<Image>();
        image.color = Color.white;
        Button uiButton = root.AddComponent<Button>();
        uiButton.targetGraphic = image;
        root.AddComponent<LayoutElement>().preferredHeight = 40f;

        var textObject = new GameObject("Label", typeof(RectTransform));
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = "CHALLENGE BEST RUN";
        text.fontSize = 14f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;

        ChallengeButton component = root.AddComponent<ChallengeButton>();
        component.manager = manager;
        component.button = uiButton;
        component.label = text;
        uiButton.onClick.AddListener(component.OnClick);

        UITheme.StylePrimary(uiButton);
    }

    private void Update()
    {
        if (manager == null || button == null) return;
        SimulationSnapshot snapshot = manager.Snapshot;
        bool available = snapshot != null && snapshot.ChallengeAvailable;
        button.interactable = available;

        if (lastAvailable != available)
        {
            lastAvailable = available;
            if (available)
            {
                UITheme.StylePrimary(button);
            }
            else
            {
                UITheme.StyleSelectable(button);
                if (label != null) label.color = UITheme.TextDimmed;
            }
        }

        if (label == null) return;
        label.text = available
            ? "CHALLENGE BEST RUN"
            : snapshot?.ChallengeUnavailableReason ?? "CHALLENGE UNAVAILABLE";
    }

    private void OnClick() => manager?.RequestChallenge();
}
