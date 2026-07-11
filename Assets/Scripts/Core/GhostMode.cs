using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// "Ghost mode": a runtime toggle (G) that stops the GPU from rendering while
/// leaving training fully intact. Training runs in SimulationManager.FixedUpdate
/// (paradigm.Tick()) and is completely independent of rendering, so disabling every
/// camera + UI canvas drops the render pass to ~zero without skipping a single
/// training step.
///
/// Why this instead of just hiding the jets: disabling jet MeshRenderers only saves
/// the cost of drawing the jets — the camera still does a full pass every frame
/// (clear, skybox, the rest of the scene, post-processing, the UI canvas). Turning
/// the cameras + canvases off removes the whole pass.
///
/// What it does NOT reduce: CPU cost (physics for the whole population + NN forward
/// passes) is the actual training work and is unchanged. Ghost mode tames the GPU
/// (heat/fans/power), and for RL it frees the GPU for the PyTorch trainer that shares
/// it. Intended for unattended overnight runs.
///
/// Self-contained: spawns itself at startup (like AppBootstrap) so it needs no scene
/// wiring and works in every scene. State is cached on enter and restored exactly on
/// exit, so it never fights other code that legitimately enabled/disabled a camera.
/// </summary>
public class GhostMode : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[GhostMode]");
        go.AddComponent<GhostMode>();
        DontDestroyOnLoad(go);
    }

    private bool active;

    // Only the objects we actually turned off, so toggling back restores exactly the
    // prior state (we never re-enable something that was already disabled).
    private readonly List<Camera> suppressedCameras = new();
    private readonly List<Canvas> suppressedCanvases = new();
    private int previousRenderFrameInterval = 1;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.gKey.wasPressedThisFrame)
            Toggle();
    }

    private void Toggle()
    {
        if (active) Disable();
        else Enable();
    }

    private void Enable()
    {
        suppressedCameras.Clear();
        suppressedCanvases.Clear();

        // Disabling the Camera component stops it submitting a render pass while
        // leaving its transform/scripts running.
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (!cam.enabled) continue;
            cam.enabled = false;
            suppressedCameras.Add(cam);
        }

        // Disabling the Canvas component stops the UI (telemetry, brain visualizer)
        // from rendering. Layout/state is preserved; only the draw is skipped.
        foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (!canvas.enabled) continue;
            canvas.enabled = false;
            suppressedCanvases.Add(canvas);
        }

        // Belt-and-suspenders: even with no cameras, push the render cadence right
        // down so anything that still triggers a present costs almost nothing.
        previousRenderFrameInterval = OnDemandRendering.renderFrameInterval;
        OnDemandRendering.renderFrameInterval = 100;

        active = true;
        Debug.Log($"<color=grey>[GhostMode]</color> ON — rendering suppressed ({suppressedCameras.Count} camera(s), {suppressedCanvases.Count} canvas(es)). Training continues. Press G to show.");
    }

    private void Disable()
    {
        foreach (var cam in suppressedCameras)
            if (cam != null) cam.enabled = true;

        foreach (var canvas in suppressedCanvases)
            if (canvas != null) canvas.enabled = true;

        OnDemandRendering.renderFrameInterval = previousRenderFrameInterval;

        suppressedCameras.Clear();
        suppressedCanvases.Clear();

        active = false;
        Debug.Log("<color=grey>[GhostMode]</color> OFF — rendering restored. Press G to hide.");
    }
}
