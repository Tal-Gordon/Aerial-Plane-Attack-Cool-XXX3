using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Zero-wiring entry point for new scenes: after every scene load, if the scene has
/// a SimulationManager but no UIManager, this creates the canvas (when needed) and
/// a UIManager, and builds the telemetry window from a layout config in Resources.
///
/// Config lookup, in order:
///   Resources/UI/TelemetryLayout_&lt;SceneName&gt;   (per-scene override)
///   Resources/UI/TelemetryLayout                 (shared default)
/// No asset found → no UI is built (scene opts out by simply having no config).
///
/// Scenes that author their own UIManager (like TestingPlayerControl today) are
/// left untouched — the presence of any UIManager disables the bootstrap.
/// </summary>
public static class TelemetryUIBootstrap
{
    private const string ConfigFolder = "UI/";
    private const string DefaultConfigName = "TelemetryLayout";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded += (_, _) => TryBootstrap();
        TryBootstrap(); // the first scene loaded before the callback was registered
    }

    private static void TryBootstrap()
    {
        if (Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include) != null) return;
        if (Object.FindFirstObjectByType<SimulationManager>() == null) return; // menu scenes etc.

        string sceneName = SceneManager.GetActiveScene().name;
        var config = Resources.Load<TelemetryLayoutConfig>(ConfigFolder + DefaultConfigName + "_" + sceneName);
        if (config == null) config = Resources.Load<TelemetryLayoutConfig>(ConfigFolder + DefaultConfigName);
        if (config == null) return;

        Canvas canvas = FindOverlayCanvas();
        if (canvas == null) canvas = CreateCanvas();
        EnsureEventSystem();

        // Inactive-GameObject trick: inject the config before Awake runs.
        var go = new GameObject("UIManager (bootstrapped)");
        go.SetActive(false);
        var manager = go.AddComponent<UIManager>();
        manager.SetLayout(config, canvas);
        go.SetActive(true);
    }

    private static Canvas FindOverlayCanvas()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return canvas;
        return null;
    }

    private static Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas (bootstrapped)");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem (bootstrapped)");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
