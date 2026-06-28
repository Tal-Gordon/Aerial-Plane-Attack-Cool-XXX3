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
/// <para>Three tiers, matched to the weight of the operation:</para>
/// <list type="bullet">
/// <item><b>Fullscreen</b> (<see cref="LoadScene"/>, <see cref="ReloadActiveScene"/>) —
/// opaque animated screen for context switches (menu → scene, scene reset). Uses
/// async scene loading so the spinner/progress actually animate.</item>
/// <item><b>Modal</b> (<see cref="RunModal"/>) — dims the current scene and blocks input
/// for in-scene rebuilds (Load, enter/exit Inference). Not a scene change, so it sits
/// over the existing scene. Paints a frame before running the (synchronous) work so the
/// overlay is visible during the unavoidable hitch.</item>
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
    private static readonly Color BackgroundColor = new Color(0.05f, 0.06f, 0.09f, 1f);
    private static readonly Color AccentColor = new Color(0.30f, 0.80f, 1f, 1f);
    private static readonly Color TextColor = new Color(0.90f, 0.93f, 0.97f, 1f);
    private const float ModalDimAlpha = 0.78f;   // background opacity in modal mode
    private const float FadeSeconds = 0.20f;
    private const float SpinDegPerSec = 220f;
    private const float ToastSeconds = 1.8f;

    // ── Main overlay (fullscreen + modal share one canvas group) ──
    private CanvasGroup overlayGroup;
    private Image backgroundImage;
    private RectTransform spinnerRect;
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
        if (spinnerActive && spinnerRect != null)
            spinnerRect.Rotate(0f, 0f, -SpinDegPerSec * Time.unscaledDeltaTime);
    }

    // ===================================================================
    //  PUBLIC API
    // ===================================================================

    /// <summary>Tier A — fade up the opaque screen, async-load <paramref name="sceneName"/>
    /// with a live progress bar, then fade out. Safe to call from a button.</summary>
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

    /// <summary>Tier A — reload the active scene (used by the reset button).</summary>
    public void ReloadActiveScene()
    {
        if (busy) return;
        StartCoroutine(LoadSceneRoutine(null, SceneManager.GetActiveScene().buildIndex));
    }

    /// <summary>Tier C — dim the scene, block input, paint a frame, then run the
    /// (synchronous) <paramref name="work"/> and hide. Use for in-scene rebuilds
    /// (Load, enter/exit Inference) where the work hitches the main thread.</summary>
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
        SetSpinner(true);
        SetStatus(string.IsNullOrEmpty(sceneName) ? "Loading…" : $"Loading {sceneName}…");
        ShowProgress(true);
        SetProgress(0f);
        backgroundImage.color = BackgroundColor; // fully opaque for a context switch
        yield return Fade(overlayGroup, 1f, true);

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

        // One painted frame at 100% so the bar visibly fills before the activation
        // hitch (all the new scene's Awake/Start fire on the activation frame).
        yield return null;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // New scene is live. Hold one frame so its first render replaces ours, then fade.
        yield return null;
        ShowProgress(false);
        yield return Fade(overlayGroup, 0f, false);
        SetSpinner(false);
        busy = false;
    }

    private IEnumerator ModalRoutine(Action work, string status)
    {
        busy = true;
        SetSpinner(true);
        SetStatus(status);
        ShowProgress(false);
        // Dimmed, not opaque — the scene shows through.
        backgroundImage.color = new Color(BackgroundColor.r, BackgroundColor.g, BackgroundColor.b, ModalDimAlpha);
        yield return Fade(overlayGroup, 1f, true);

        // Paint at least one full frame so the overlay is on screen *before* the
        // synchronous work freezes the main thread.
        yield return null;
        yield return new WaitForEndOfFrame();

        Exception captured = null;
        try { work?.Invoke(); }
        catch (Exception e) { captured = e; }

        // Let the rebuilt scene settle for a frame, then fade out — even if the work
        // threw, so a failure never leaves the screen stuck behind the overlay.
        yield return null;
        yield return Fade(overlayGroup, 0f, false);
        SetSpinner(false);
        busy = false;

        if (captured != null) Debug.LogException(captured);
    }

    private IEnumerator ToastWorkRoutine(Action work, string busyMessage, string doneMessage)
    {
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        SetToast(busyMessage);
        yield return Fade(toastGroup, 1f, false);

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
        yield return Fade(toastGroup, 1f, false);
        yield return StartCoroutine(ToastHoldThenHide(seconds));
    }

    private IEnumerator ToastHoldThenHide(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
        yield return Fade(toastGroup, 0f, false);
        toastRoutine = null;
    }

    private IEnumerator Fade(CanvasGroup group, float target, bool blockRaycasts)
    {
        group.blocksRaycasts = blockRaycasts;
        group.interactable = blockRaycasts;
        float start = group.alpha;
        float t = 0f;
        while (t < FadeSeconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, t / FadeSeconds);
            yield return null;
        }
        group.alpha = target;
        if (target <= 0f) { group.blocksRaycasts = false; group.interactable = false; }
    }

    // ===================================================================
    //  SMALL SETTERS
    // ===================================================================
    private void SetSpinner(bool on)
    {
        spinnerActive = on;
        if (spinnerRect != null) spinnerRect.gameObject.SetActive(on);
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

        // Spinner — generated "comet" ring sprite, rotated in Update.
        var spinnerImg = AddImage(overlayGO.transform, "Spinner", AccentColor);
        spinnerImg.sprite = CreateCometSprite(128, 10);
        spinnerImg.type = Image.Type.Simple;
        spinnerRect = spinnerImg.rectTransform;
        Center(spinnerRect, new Vector2(96, 96), new Vector2(0, 40));

        // Status text under the spinner.
        statusText = AddText(overlayGO.transform, "Status", font, 30, TextColor);
        Center(statusText.rectTransform, new Vector2(900, 50), new Vector2(0, -48));

        // Progress bar (shown for scene loads only).
        progressBar = NewUIChild("Progress", overlayGO.transform);
        var barBg = progressBar.AddComponent<Image>();
        barBg.color = new Color(1f, 1f, 1f, 0.12f);
        Center(barBg.rectTransform, new Vector2(520, 8), new Vector2(0, -100));
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
        toastRect.sizeDelta = new Vector2(360, 56);
        var toastBg = toastGO.AddComponent<Image>();
        toastBg.color = new Color(0.05f, 0.06f, 0.09f, 0.92f);
        toastText = AddText(toastGO.transform, "ToastText", font, 24, TextColor);
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
        img.color = color;
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

    /// <summary>Generates a circular "comet" spinner sprite: a ring whose alpha sweeps
    /// from solid to transparent around the circle, so rotating it reads as a spinner.</summary>
    private static Sprite CreateCometSprite(int size, int ringThickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float center = (size - 1) / 2f;
        float outer = size / 2f - 1f;
        float inner = outer - ringThickness;
        var pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Radial band with 1px soft edges for anti-aliasing.
                float band = Mathf.Clamp01(Mathf.Min(dist - inner, outer - dist));
                // Angular sweep 0..1 around the circle for the comet tail.
                float angle = (Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI);
                float tail = Mathf.Pow(angle, 1.4f);

                pixels[y * size + x] = new Color(1f, 1f, 1f, band * tail);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
