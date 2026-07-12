using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
    private const int MLAgentsPort = 5004;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        // This player always supports RL, so install the pinned training environment
        // before showing the menu rather than waiting until PPO/SAC is selected.
        if (!TrainerProcessLauncher.EnsureBundledEnvironmentInstalled())
        {
            Debug.LogError("[AppBootstrap] The ML-Agents environment is required. Exiting because setup failed.");
            Application.Quit();
            return;
        }

        // ML-Agents does not create a communicator in a standalone player unless
        // --mlagents-port is present. Make the normal game executable its own launcher
        // so users can double-click it instead of knowing about a special batch file.
        if (RelaunchWithMLAgentsPortIfMissing())
            return;
#endif

        // vSync, when on, overrides Application.targetFrameRate entirely — disable it
        // so the cap below actually takes effect.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    private static bool RelaunchWithMLAgentsPortIfMissing()
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--mlagents-port", StringComparison.OrdinalIgnoreCase) ||
                args[i].StartsWith("--mlagents-port=", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        string executable = args.Length > 0 ? args[0] : null;
        if (string.IsNullOrEmpty(executable) || !File.Exists(executable))
        {
            Debug.LogError("[AppBootstrap] Could not resolve the player executable for ML-Agents relaunch.");
            return false;
        }

        var forwarded = new StringBuilder();
        for (int i = 1; i < args.Length; i++)
            forwarded.Append(QuoteArgument(args[i])).Append(' ');
        forwarded.Append("--mlagents-port ").Append(MLAgentsPort);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = forwarded.ToString(),
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = true,
            });
            Application.Quit();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AppBootstrap] Could not relaunch with the ML-Agents port: {e.Message}");
            return false;
        }
    }

    private static string QuoteArgument(string value)
    {
        if (string.IsNullOrEmpty(value)) return "\"\"";
        if (value.IndexOfAny(new[] { ' ', '\t', '\"' }) < 0) return value;
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
#endif
}
