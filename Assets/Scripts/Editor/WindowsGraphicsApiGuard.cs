#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps Windows standalone builds on D3D11. D3D12 device removal was reproducible
/// after CUDA-backed ML-Agents training and left later player launches unable to
/// create a graphics device until the NVIDIA driver (or PC) was restarted.
/// </summary>
[InitializeOnLoad]
public static class WindowsGraphicsApiGuard
{
    static WindowsGraphicsApiGuard() => Enforce();

    public static void Enforce()
    {
        const BuildTarget target = BuildTarget.StandaloneWindows64;
        GraphicsDeviceType[] current = PlayerSettings.GetGraphicsAPIs(target);
        bool alreadySafe =
            !PlayerSettings.GetUseDefaultGraphicsAPIs(target) &&
            current is { Length: 1 } &&
            current[0] == GraphicsDeviceType.Direct3D11;

        if (alreadySafe)
            return;

        PlayerSettings.SetUseDefaultGraphicsAPIs(target, false);
        PlayerSettings.SetGraphicsAPIs(
            target,
            new[] { GraphicsDeviceType.Direct3D11 });
        Debug.Log("[WindowsGraphicsApiGuard] Windows standalone graphics API pinned to Direct3D 11 for RL stability.");
    }
}

public sealed class WindowsGraphicsApiBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => int.MinValue;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform is BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64)
            WindowsGraphicsApiGuard.Enforce();
    }
}
#endif
