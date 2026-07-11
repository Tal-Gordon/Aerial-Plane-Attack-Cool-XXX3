using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Single, self-contained loading-feedback service for the whole game. Auto-boots
/// once per play session (no scene placement needed) onto a DontDestroyOnLoad object
/// and builds its own UGUI canvas in code, so there is nothing to wire in the
/// Inspector and no art to import — swap in real sprites/colours later if desired.
///
/// <para>Deliberately low-fi: solid panels, blocky cycling dots, instant show/hide
/// (no fades). Three tiers, matched to the weight of the operation:</para>
/// <list type="bullet">
/// <item><b>Fullscreen</b> (<see cref="LoadScene"/>, <see cref="ReloadActiveScene"/>) —
/// opaque screen for context switches (menu → scene, scene reset, cold reload). Uses
/// async scene loading so the dots/progress actually animate.</item>
/// <item><b>Modal</b> (<see cref="RunModal"/>) — dims the current scene and blocks input
/// for in-scene rebuilds (Load, enter/exit Inference, hot param commits). Not a scene
/// change, so it sits over the existing scene. Paints a frame before running the
/// (synchronous) work so the overlay is visible during the unavoidable hitch.</item>
/// <item><b>Toast</b> (<see cref="RunToast"/>, <see cref="Toast"/>) — small, non-blocking
/// corner pill for light/frequent actions (Save). Never takes the screen.</item>
/// </list>
///
/// All timing uses unscaled time, since <c>Time.timeScale</c> can be 0 or very high
/// while these operations run.
/// </summary>
public class LoadingOverlay : MonoBehaviour
{
    public static LoadingOverlay Instance { get; private set; }

    // ── Tunables (edit here, or tweak the generated objects at runtime) ──
    private static readonly Color BackgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
    private static readonly Color AccentColor = new Color(0.85f, 0.87f, 0.90f, 1f); // active dot / fill
    private static readonly Color DotIdleColor = new Color(0.85f, 0.87f, 0.90f, 0.22f);
    private static readonly Color TextColor = new Color(0.85f, 0.87f, 0.90f, 1f);
    private const float ModalDimAlpha = 0.80f; // background opacity in modal mode
    private const int DotCount = 3;
    private const float DotStepSeconds = 0.12f; // how fast the active dot advances
    private const float MinVisibleSeconds = 0.8f; // keep a fast scene-load up long enough to animate
    private const float ToastSeconds = 1.8f;

    // ── Main overlay (fullscreen + modal share one canvas group) ──
    private CanvasGroup overlayGroup;
    private Image backgroundImage;
    private GameObject spinnerRoot;
    private Image[] dots;
    private int dotIndex;
    private float dotTimer;
    private Text statusText;
    private RectTransform progressFill;
    private GameObject progressBar;
    private bool spinnerActive;
    private bool busy; // one main-overlay operation at a time

    // ── Toast (independent of the main overlay) ──
    private CanvasGroup toastGroup;
    private Text toastText;
    private Coroutine toastRoutine;

    // ───────────────────────────────────────────────────────────────────
    // Bootstrap — runs once, before the first scene's objects come alive.
    // ───────────────────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[LoadingOverlay]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<LoadingOverlay>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildUI();
    }

    private void Update()
    {
        if (!spinnerActive || dots == null) return;
        dotTimer += Time.unscaledDeltaTime;
        if (dotTimer < DotStepSeconds) return;
        dotTimer -= DotStepSeconds;
        dotIndex = (dotIndex + 1) % dots.Length;
        for (int i = 0; i < dots.Length; i++)
            dots[i].color = i == dotIndex ? AccentColor : DotIdleColor;
    }

    // ===================================================================
    //  PUBLIC API
    // ===================================================================

    /// <summary>Tier A — show the opaque screen, async-load <paramref name="sceneName"/>
    /// with a live progress bar, then hide. Safe to call from a button.</summary>
    public void LoadScene(string sceneName)
    {
        if (busy) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[LoadingOverlay] Scene '{sceneName}' is not in Build Settings — loading directly without the overlay.");
            SceneManager.LoadScene(sceneName);
            return;
        }
        StartCoroutine(LoadSceneRoutine(sceneName, -1));
    }

    /// <summary>Tier A — reload the active scene (used by reset / cold param reload).</summary>
    public void ReloadActiveScene()
    {
        if (busy) return;
        StartCoroutine(LoadSceneRoutine(null, SceneManager.GetActiveScene().buildIndex));
    }

    /// <summary>Tier C — dim the scene, block input, paint a frame, then run the
    /// (synchronous) <paramref name="work"/> and hide. Use for in-scene rebuilds
    /// (Load, enter/exit Inference, hot param commits) where the work hitches the
    /// main thread.</summary>
    public void RunModal(Action work, string status)
    {
        if (busy) { work?.Invoke(); return; } // never drop the action
        StartCoroutine(ModalRoutine(work, status));
    }

    /// <summary>Tier B — show "<paramref name="busyMessage"/>", paint a frame, run the
    /// light <paramref name="work"/>, then flip the toast to "<paramref name="doneMessage"/>".
    /// Non-blocking: the scene stays interactive.</summary>
    public void RunToast(Action work, string busyMessage, string doneMessage)
    {
        StartCoroutine(ToastWorkRoutine(work, busyMessage, doneMessage));
    }

    /// <summary>Tier B — show a transient corner message with no associated work.</summary>
    public void Toast(string message)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastShowRoutine(message, ToastSeconds));
    }

    // ===================================================================
    //  ROUTINES
    // ===================================================================

    private IEnumerator LoadSceneRoutine(string sceneName, int buildIndex)
    {
        busy = true;
        float startTime = Time.unscaledTime;
        SetSpinner(true);
        SetStatus(string.IsNullOrEmpty(sceneName) ? "Loading…" : $"Loading {sceneName}…");
        ShowProgress(true);
        SetProgress(0f);
        backgroundImage.color = BackgroundColor; // fully opaque for a context switch
        SetVisible(overlayGroup, true, true);

        AsyncOperation op = sceneName != null
            ? SceneManager.LoadSceneAsync(sceneName)
            : SceneManager.LoadSceneAsync(buildIndex);
        op.allowSceneActivation = false;

        // 0 → 0.9 is the actual load; it stalls at 0.9 until we allow activation.
        while (op.progress < 0.9f)
        {
            SetProgress(op.progress / 0.9f);
            yield return null;
        }
        SetProgress(1f);

        // Keep the loader up until a minimum time has passed so the dots actually cycle —
        // a small/cached scene otherwise finishes in a frame or two, before one dot-step
        // elapses, and the overlay just flashes a single lit square. These are real
        // rendered frames (the load is async), so the dots animate during the wait.
        while (Time.unscaledTime - startTime < MinVisibleSeconds)
            yield return null;

        // One painted frame at 100% so the bar visibly fills before the activation
        // hitch (all the new scene's Awake/Start fire on the activation frame).
        yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // New scene is live. Hold one frame so its first render replaces ours, then hide.
        yield return null;
        ShowProgress(false);
        SetVisible(overlayGroup, false, false);
        SetSpinner(false);
        busy = false;
    }

    private IEnumerator ModalRoutine(Action work, string status)
    {
        busy = true;
        // No animated spinner here on purpose: the work below runs synchronously and
        // freezes the main thread, so Update() can't fire and any spinner would just
        // freeze mid-cycle and look broken. A static dim + status line reads as "busy"
        // honestly. (Animating *through* the freeze would require chunking the rebuild
        // across frames — see notes.)
        SetSpinner(false);
        SetStatus(status);
        ShowProgress(false);
        // Dimmed, not opaque — the scene shows through.
        backgroundImage.color = new Color(BackgroundColor.r, BackgroundColor.g, BackgroundColor.b, ModalDimAlpha);
        SetVisible(overlayGroup, true, true);

        // Paint at least one full frame so the overlay is on screen *before* the
        // synchronous work freezes the main thread.
        yield return null;
        yield return new WaitForEndOfFrame();

        Exception captured = null;
        try { work?.Invoke(); }
        catch (Exception e) { captured = e; }

        // Let the rebuilt scene settle for a frame, then hide — even if the work threw,
        // so a failure never leaves the screen stuck behind the overlay.
        yield return null;
        SetVisible(overlayGroup, false, false);
        SetSpinner(false);
        busy = false;

        if (captured != null) Debug.LogException(captured);
    }

    private IEnumerator ToastWorkRoutine(Action work, string busyMessage, string doneMessage)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        SetToast(busyMessage);
        SetVisible(toastGroup, true, false);

        // Paint a frame, then run the light work (Save is short but still on the main thread).
        yield return null;
        yield return new WaitForEndOfFrame();

        Exception captured = null;
        try { work?.Invoke(); }
        catch (Exception e) { captured = e; }

        SetToast(captured == null ? doneMessage : "Failed");
        toastRoutine = StartCoroutine(ToastHoldThenHide(ToastSeconds));
        if (captured != null) Debug.LogException(captured);
    }

    private IEnumerator ToastShowRoutine(string message, float seconds)
    {
        SetToast(message);
        SetVisible(toastGroup, true, false);
        yield return StartCoroutine(ToastHoldThenHide(seconds));
    }

    private IEnumerator ToastHoldThenHide(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        SetVisible(toastGroup, false, false);
        toastRoutine = null;
    }

    // ===================================================================
    //  SMALL SETTERS
    // ===================================================================
    private static void SetVisible(CanvasGroup group, bool show, bool block)
    {
        group.alpha = show ? 1f : 0f;
        group.blocksRaycasts = show && block;
        group.interactable = show && block;
    }

    private void SetSpinner(bool on)
    {
        spinnerActive = on;
        if (spinnerRoot != null) spinnerRoot.SetActive(on);
        if (!on || dots == null) return;
        // Reset to a known frame so it doesn't resume mid-cycle.
        dotIndex = 0;
        dotTimer = 0f;
        for (int i = 0; i < dots.Length; i++)
            dots[i].color = i == 0 ? AccentColor : DotIdleColor;
    }

    private void SetStatus(string s) { if (statusText != null) statusText.text = s; }
    private void SetToast(string s) { if (toastText != null) toastText.text = s; }
    private void ShowProgress(bool on) { if (progressBar != null) progressBar.SetActive(on); }
    private void SetProgress(float v01)
    {
        if (progressFill != null)
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(v01), 1f);
    }

    // ===================================================================
    //  UI CONSTRUCTION (all code — no prefab, no imported art)
    // ===================================================================
    private void BuildUI()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Root canvas, drawn above everything.
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // ── Main overlay group (fullscreen + modal) ──
        var overlayGO = NewUIChild("Overlay", transform);
        overlayGroup = overlayGO.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 0f;
        overlayGroup.blocksRaycasts = false;
        Stretch(overlayGO.GetComponent<RectTransform>());

        backgroundImage = AddImage(overlayGO.transform, "Background", BackgroundColor);
        Stretch(backgroundImage.rectTransform);

        // Spinner — a row of blocky square dots; the bright one advances on a timer.
        spinnerRoot = NewUIChild("Spinner", overlayGO.transform);
        const float dotSize = 18f, gap = 14f;
        float totalW = DotCount * dotSize + (DotCount - 1) * gap;
        Center(spinnerRoot.GetComponent<RectTransform>(), new Vector2(totalW, dotSize), new Vector2(0, 36));
        dots = new Image[DotCount];
        for (int i = 0; i < DotCount; i++)
        {
            var dot = AddImage(spinnerRoot.transform, $"Dot{i}", DotIdleColor);
            var rt = dot.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(dotSize, dotSize);
            rt.anchoredPosition = new Vector2(-totalW / 2f + dotSize / 2f + i * (dotSize + gap), 0f);
            dots[i] = dot;
        }

        // Status text under the spinner.
        statusText = AddText(overlayGO.transform, "Status", font, 28, TextColor);
        Center(statusText.rectTransform, new Vector2(900, 44), new Vector2(0, -48));

        // Progress bar (shown for scene loads only).
        progressBar = NewUIChild("Progress", overlayGO.transform);
        var barBg = progressBar.AddComponent<Image>();
        barBg.color = new Color(0.85f, 0.87f, 0.90f, 0.15f);
        Center(barBg.rectTransform, new Vector2(480, 6), new Vector2(0, -96));
        var fillGO = NewUIChild("Fill", progressBar.transform);
        var fill = fillGO.AddComponent<Image>();
        fill.color = AccentColor;
        progressFill = fill.rectTransform;
        progressFill.anchorMin = new Vector2(0f, 0f);
        progressFill.anchorMax = new Vector2(0f, 1f); // width driven via anchorMax.x in SetProgress
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        progressBar.SetActive(false);

        // ── Toast group (independent, bottom-centre) ──
        var toastGO = NewUIChild("Toast", transform);
        toastGroup = toastGO.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
        toastGroup.blocksRaycasts = false;
        var toastRect = toastGO.GetComponent<RectTransform>();
        toastRect.anchorMin = toastRect.anchorMax = new Vector2(0.5f, 0f);
        toastRect.pivot = new Vector2(0.5f, 0f);
        toastRect.anchoredPosition = new Vector2(0, 40);
        toastRect.sizeDelta = new Vector2(340, 52);
        var toastBg = toastGO.AddComponent<Image>();
        toastBg.color = new Color(0.06f, 0.07f, 0.09f, 0.94f);
        toastText = AddText(toastGO.transform, "ToastText", font, 22, TextColor);
        Stretch(toastText.rectTransform);

        SetSpinner(false);
    }

    // ── UI helpers ──
    private static GameObject NewUIChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image AddImage(Transform parent, string name, Color color)
    {
        var go = NewUIChild(name, parent);
        var img = go.AddComponent<Image>();
        img.color = color; // no sprite → a plain solid quad
        return img;
    }

    private static Text AddText(Transform parent, string name, Font font, int size, Color color)
    {
        var go = NewUIChild(name, parent);
        var txt = go.AddComponent<Text>();
        txt.font = font;
        txt.fontSize = size;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return txt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rt, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
    }
}
