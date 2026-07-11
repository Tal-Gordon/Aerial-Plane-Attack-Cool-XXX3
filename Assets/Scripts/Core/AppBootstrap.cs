using UnityEngine;

/// <summary>
/// App-wide startup configuration. ML-Agents only applies a frame cap once an RL
/// trainer connects (via its engine-config side channel), which leaves the main menu
/// and the evolutionary scenes completely uncapped — the GPU renders thousands of
/// frames per second for no benefit. Cap the frame rate once at process start so every
/// scene is bounded; the RL trainer later re-applies the same cap (RLSettings.TargetFrameRate)
/// on connect, so the two agree.
/// </summary>
public static class AppBootstrap
{
    // Keep in sync with RLSettings.TargetFrameRate so RL scenes don't visibly change
    // cadence when the trainer connects.
    private const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
        // vSync, when on, overrides Application.targetFrameRate entirely — disable it
        // so the cap below actually takes effect.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }
}
