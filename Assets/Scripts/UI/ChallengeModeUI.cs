using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-only UI for the saved-run challenge: confirmation, three-second
/// countdown, compact flight HUD/artificial horizon, and the result card.
/// </summary>
public sealed class ChallengeModeUI : MonoBehaviour
{
    private SimulationManager owner;
    private Canvas canvas;

    private GameObject confirmationRoot;
    private GameObject hudRoot;
    private GameObject resultsRoot;
    private TextMeshProUGUI countdownLabel;
    private TextMeshProUGUI flightReadout;
    private TextMeshProUGUI raceReadout;
    private TextMeshProUGUI resultTitle;
    private TextMeshProUGUI resultBody;
    private RectTransform horizonInner;

    public static ChallengeModeUI Ensure(SimulationManager owner)
    {
        ChallengeModeUI existing = FindFirstObjectByType<ChallengeModeUI>(
            FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.owner = owner;
            return existing;
        }

        var root = new GameObject(
            "ChallengeModeUI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        ChallengeModeUI ui = root.AddComponent<ChallengeModeUI>();
        ui.owner = owner;
        ui.Build();
        return ui;
    }

    private void Build()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        BuildConfirmation();
        BuildHud();
        BuildResults();
        HideAll();
    }

    public void ShowConfirmation(Action onConfirm)
    {
        confirmationRoot.SetActive(true);
        confirmationRoot.transform.SetAsLastSibling();
        Button confirm = FindButton(confirmationRoot, "Confirm");
        confirm.onClick.RemoveAllListeners();
        confirm.onClick.AddListener(() =>
        {
            confirmationRoot.SetActive(false);
            onConfirm?.Invoke();
        });
    }

    public void ShowCountdown()
    {
        confirmationRoot.SetActive(false);
        resultsRoot.SetActive(false);
        hudRoot.SetActive(true);
        countdownLabel.gameObject.SetActive(true);
    }

    public void ShowRace()
    {
        hudRoot.SetActive(true);
        resultsRoot.SetActive(false);
    }

    public void ShowResults(string winner, ChallengeRaceStats player, ChallengeRaceStats ai)
    {
        hudRoot.SetActive(false);
        resultsRoot.SetActive(true);
        resultTitle.text = winner;
        resultTitle.color = winner.StartsWith("YOU WIN", StringComparison.Ordinal)
            ? UITheme.Accent
            : winner.StartsWith("AI WINS", StringComparison.Ordinal)
                ? UITheme.Amber
                : UITheme.TextColor;

        resultBody.text =
            BuildParticipantLine("YOU", player) + "\n\n" +
            BuildParticipantLine("SAVED AI", ai);
        resultsRoot.transform.SetAsLastSibling();
    }

    public void HideAll()
    {
        if (confirmationRoot != null) confirmationRoot.SetActive(false);
        if (hudRoot != null) hudRoot.SetActive(false);
        if (resultsRoot != null) resultsRoot.SetActive(false);
    }

    private void Update()
    {
        if (owner == null || hudRoot == null || !hudRoot.activeSelf) return;

        float remaining = owner.ChallengeCountdownRemaining;
        if (owner.IsChallengeWaitingForAI)
        {
            countdownLabel.gameObject.SetActive(true);
            countdownLabel.fontSize = 48f;
            countdownLabel.text = "SYNCING AI";
        }
        else if (remaining > 0f)
        {
            countdownLabel.gameObject.SetActive(true);
            countdownLabel.fontSize = 110f;
            countdownLabel.text = Mathf.CeilToInt(remaining).ToString();
        }
        else if (owner.IsChallengeRaceRunning)
        {
            countdownLabel.fontSize = 110f;
            countdownLabel.text = "GO";
            if (countdownLabel.gameObject.activeSelf && owner.ChallengeRaceElapsed > 0.65f)
                countdownLabel.gameObject.SetActive(false);
        }

        JetAgent player = owner.ChallengePlayer;
        if (player == null) return;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
        float throttle = owner.ChallengePlayerController != null
            ? owner.ChallengePlayerController.ThrustInput * 100f
            : 0f;

        Vector3 euler = player.transform.eulerAngles;
        float pitch = Mathf.Asin(Mathf.Clamp(player.transform.forward.y, -1f, 1f))
                      * Mathf.Rad2Deg;
        float bank = NormalizeAngle(euler.z);
        float heading = euler.y;

        flightReadout.text =
            $"THR  {throttle,3:0}%\n" +
            $"SPD  {speed,4:0} m/s\n" +
            $"ALT  {player.transform.position.y,4:0} m\n" +
            $"HDG  {heading,3:000}°\n" +
            $"PITCH {pitch,4:0}°";

        ChallengeRaceStats you = owner.PlayerChallengeStats;
        ChallengeRaceStats ai = owner.AIChallengeStats;
        raceReadout.text =
            $"YOU  {FormatHoops(you)}   {you?.ElapsedTime ?? 0f:0.0}s\n" +
            $"AI    {FormatHoops(ai)}   {ai?.ElapsedTime ?? 0f:0.0}s";

        if (horizonInner != null)
        {
            horizonInner.localRotation = Quaternion.Euler(0f, 0f, -bank);
            horizonInner.anchoredPosition =
                new Vector2(0f, Mathf.Clamp(-pitch * 2.2f, -150f, 150f));
        }
    }

    private void BuildConfirmation()
    {
        confirmationRoot = FullScreen("ChallengeConfirmation");
        AddImage(confirmationRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.62f), true);

        Image panel = AddImage(confirmationRoot.transform, "Panel", UITheme.Panel);
        Center(panel.rectTransform, new Vector2(700f, 350f));
        AddTopAccent(panel.transform, 700f);

        TextMeshProUGUI title = AddText(panel.transform, "Title", "CHALLENGE YOUR BEST RUN",
            30f, UITheme.Accent, FontStyles.Bold);
        Top(title.rectTransform, new Vector2(650f, 46f), new Vector2(0f, -30f));

        TextMeshProUGUI body = AddText(panel.transform, "Body",
            "Race the AI from your latest save.\n\n" +
            "Entering replaces the current on-screen run with the saved run; " +
            "unsaved training progress is not included.\n\n" +
            "W/S pitch  •  A/D roll  •  Q/E yaw  •  Up/Down throttle",
            19f, UITheme.TextColor, FontStyles.Normal);
        Top(body.rectTransform, new Vector2(610f, 165f), new Vector2(0f, -92f));

        Button cancel = BuildButton(panel.transform, "Cancel", "CANCEL", new Vector2(-145f, 38f));
        cancel.onClick.AddListener(() => confirmationRoot.SetActive(false));

        Button confirm = BuildButton(panel.transform, "Confirm", "START CHALLENGE", new Vector2(145f, 38f));
        UITheme.StylePrimary(confirm);
    }

    private void BuildHud()
    {
        hudRoot = FullScreen("ChallengeHUD");

        // Countdown / GO.
        countdownLabel = AddText(hudRoot.transform, "Countdown", "3",
            110f, UITheme.Accent, FontStyles.Bold);
        Center(countdownLabel.rectTransform, new Vector2(360f, 150f));

        // Artificial horizon (clipped rolling sky/ground card).
        Image horizonFrame = AddImage(hudRoot.transform, "ArtificialHorizon", new Color(0.02f, 0.03f, 0.04f, 0.72f));
        Center(horizonFrame.rectTransform, new Vector2(300f, 190f));
        horizonFrame.rectTransform.anchoredPosition = new Vector2(0f, -300f);
        horizonFrame.gameObject.AddComponent<RectMask2D>();

        var moving = new GameObject("MovingHorizon", typeof(RectTransform));
        moving.transform.SetParent(horizonFrame.transform, false);
        horizonInner = (RectTransform)moving.transform;
        Center(horizonInner, new Vector2(520f, 520f));

        Image sky = AddImage(moving.transform, "Sky", new Color(0.16f, 0.42f, 0.66f, 0.88f));
        sky.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        sky.rectTransform.anchorMax = Vector2.one;
        sky.rectTransform.offsetMin = sky.rectTransform.offsetMax = Vector2.zero;

        Image ground = AddImage(moving.transform, "Ground", new Color(0.39f, 0.25f, 0.13f, 0.9f));
        ground.rectTransform.anchorMin = Vector2.zero;
        ground.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        ground.rectTransform.offsetMin = ground.rectTransform.offsetMax = Vector2.zero;

        Image horizon = AddImage(moving.transform, "HorizonLine", Color.white);
        Center(horizon.rectTransform, new Vector2(520f, 3f));

        // Fixed aircraft reference.
        Image leftWing = AddImage(horizonFrame.transform, "AircraftLeft", UITheme.Accent);
        Center(leftWing.rectTransform, new Vector2(85f, 4f));
        leftWing.rectTransform.anchoredPosition = new Vector2(-55f, 0f);
        Image rightWing = AddImage(horizonFrame.transform, "AircraftRight", UITheme.Accent);
        Center(rightWing.rectTransform, new Vector2(85f, 4f));
        rightWing.rectTransform.anchoredPosition = new Vector2(55f, 0f);
        Image nose = AddImage(horizonFrame.transform, "AircraftNose", UITheme.Accent);
        Center(nose.rectTransform, new Vector2(5f, 18f));
        nose.rectTransform.anchoredPosition = new Vector2(0f, -7f);

        Image readoutPanel = AddImage(hudRoot.transform, "FlightReadout", new Color(0.04f, 0.06f, 0.09f, 0.72f));
        Anchor(readoutPanel.rectTransform, new Vector2(0f, 1f), new Vector2(250f, 185f), new Vector2(30f, -30f));
        flightReadout = AddText(readoutPanel.transform, "Text", string.Empty, 21f, UITheme.TextColor, FontStyles.Bold);
        Stretch(flightReadout.rectTransform, 16f);
        flightReadout.alignment = TextAlignmentOptions.TopLeft;

        Image racePanel = AddImage(hudRoot.transform, "RaceReadout", new Color(0.04f, 0.06f, 0.09f, 0.72f));
        Anchor(racePanel.rectTransform, Vector2.one, new Vector2(360f, 105f), new Vector2(-30f, -30f));
        raceReadout = AddText(racePanel.transform, "Text", string.Empty, 21f, UITheme.TextColor, FontStyles.Bold);
        Stretch(raceReadout.rectTransform, 14f);
        raceReadout.alignment = TextAlignmentOptions.TopLeft;

        TextMeshProUGUI hint = AddText(hudRoot.transform, "Controls",
            "W/S PITCH   A/D ROLL   Q/E YAW   UP/DOWN THROTTLE",
            16f, UITheme.TextDimmed, FontStyles.Normal);
        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(760f, 34f);
        hintRect.anchoredPosition = new Vector2(0f, 18f);
    }

    private void BuildResults()
    {
        resultsRoot = FullScreen("ChallengeResults");
        AddImage(resultsRoot.transform, "Dim", new Color(0f, 0f, 0f, 0.68f), true);

        Image panel = AddImage(resultsRoot.transform, "Panel", UITheme.Panel);
        Center(panel.rectTransform, new Vector2(760f, 500f));
        AddTopAccent(panel.transform, 760f);

        resultTitle = AddText(panel.transform, "Title", "RESULT",
            38f, UITheme.Accent, FontStyles.Bold);
        Top(resultTitle.rectTransform, new Vector2(710f, 58f), new Vector2(0f, -32f));

        resultBody = AddText(panel.transform, "Body", string.Empty,
            21f, UITheme.TextColor, FontStyles.Normal);
        Top(resultBody.rectTransform, new Vector2(650f, 265f), new Vector2(0f, -112f));

        Button rematch = BuildButton(panel.transform, "Rematch", "REMATCH", new Vector2(-145f, 42f));
        rematch.onClick.AddListener(() => owner?.RematchChallenge());
        UITheme.StylePrimary(rematch);

        Button exit = BuildButton(panel.transform, "Exit", "EXIT", new Vector2(145f, 42f));
        exit.onClick.AddListener(() => owner?.ExitChallengeMode());
    }

    private static string BuildParticipantLine(string name, ChallengeRaceStats stats)
    {
        if (stats == null) return $"{name}\nNo result";
        string status = stats.CompletedTrack ? "Completed" : stats.Crashed ? "Crashed" : "Timed out";
        return
            $"<b>{name}</b>  —  {status}\n" +
            $"Hoops: {FormatHoops(stats)}     Time: {stats.ElapsedTime:0.00}s\n" +
            $"Average speed: {stats.AverageSpeed:0} m/s     " +
            $"Finish speed: {stats.FinishSpeed:0} m/s     Max: {stats.MaxSpeed:0} m/s";
    }

    private static string FormatHoops(ChallengeRaceStats stats)
    {
        if (stats == null) return "—";
        return stats.TotalHoops > 0 && stats.HoopsPassed >= stats.TotalHoops
            ? "ALL"
            : $"{stats.HoopsPassed}/{stats.TotalHoops}";
    }

    private GameObject FullScreen(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    private static Image AddImage(Transform parent, string name, Color color, bool stretch = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        if (stretch) Stretch(image.rectTransform, 0f);
        return image;
    }

    private static TextMeshProUGUI AddText(
        Transform parent, string name, string value, float size, Color color, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static Button BuildButton(Transform parent, string name, string label, Vector2 position)
    {
        Image image = AddImage(parent, name, Color.white);
        RectTransform rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(260f, 54f);
        rt.anchoredPosition = position;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        UITheme.StyleSelectable(button);

        TextMeshProUGUI text = AddText(image.transform, "Label", label, 19f, UITheme.TextColor, FontStyles.Bold);
        Stretch(text.rectTransform, 4f);
        return button;
    }

    private static Button FindButton(GameObject root, string name)
    {
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button;
        return null;
    }

    private static void AddTopAccent(Transform parent, float width)
    {
        Image line = AddImage(parent, "AccentLine", UITheme.Accent);
        Top(line.rectTransform, new Vector2(width, 3f), Vector2.zero);
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Center(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void Top(RectTransform rt, Vector2 size, Vector2 position)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
