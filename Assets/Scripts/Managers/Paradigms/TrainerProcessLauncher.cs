using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class TrainerProcessLauncher : IDisposable
{
    private Process trainerProcess;
    private readonly int port;

    private const string CondaEnvName = "mlagents";

    public TrainerProcessLauncher(int port = 5004)
    {
        this.port = port;
    }

    public bool Launch(string yamlRelativePath, string runId = "training", bool resume = false, bool inference = false, int timeoutSeconds = 60)
    {
        string pythonPath = FindCondaPython();
        if (pythonPath == null)
        {
            Debug.LogError(
                "[TrainerLauncher] Could not find Python in conda env 'mlagents'.\n" +
                "Set the MLAGENTS_PYTHON environment variable to your Python executable path.\n" +
                "Example: C:\\Users\\YourName\\miniconda3\\envs\\mlagents\\python.exe");
            return false;
        }

        Debug.Log($"[TrainerLauncher] Using Python: {pythonPath}");

        KillProcessOnPort(port);

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        // --resume continues an existing run-id from its latest checkpoint; --force
        // wipes any existing run-id and starts fresh. The caller picks based on
        // whether a saved checkpoint has been staged into results/<run-id>/.
        string runModeFlag = resume ? "--resume" : "--force";

        // --inference loads the (resumed) policy and runs it WITHOUT training — no
        // gradient updates, no new checkpoints. Always paired with --resume so a
        // saved model is actually loaded; on its own it would replay a random net.
        string inferenceFlag = inference ? " --inference" : "";

        var psi = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"-u -m mlagents.trainers.learn \"{yamlRelativePath}\" --run-id={runId} {runModeFlag}{inferenceFlag}",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        try
        {
            trainerProcess = Process.Start(psi);
            if (trainerProcess == null)
            {
                Debug.LogError("[TrainerLauncher] Process.Start returned null.");
                return false;
            }

            StartOutputPipe(trainerProcess.StandardOutput);
            StartOutputPipe(trainerProcess.StandardError);

            Debug.Log($"[TrainerLauncher] Started mlagents-learn (PID {trainerProcess.Id}). " +
                      $"Waiting up to {timeoutSeconds}s for port {port}...");

            // TODO: WaitForPort blocks the Unity main thread (up to timeoutSeconds) while the
            // Python trainer boots, freezing the editor/game on every RL launch and contributing
            // to the first-frame hitch. Consider moving trainer startup off the main thread
            // (coroutine/Task) and only handing the population to the paradigm once the port is ready.
            bool ready = WaitForPort(timeoutSeconds);
            if (ready)
                Debug.Log($"[TrainerLauncher] Trainer ready on port {port}.");
            else
                Debug.LogError($"[TrainerLauncher] Trainer not ready after {timeoutSeconds}s. Check Python errors above.");

            return ready;
        }
        catch (Exception e)
        {
            Debug.LogError($"[TrainerLauncher] Failed to start process: {e.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (trainerProcess == null) return;

        try
        {
            if (!trainerProcess.HasExited)
            {
                Debug.Log("[TrainerLauncher] Killing trainer process tree...");
                // Kill the whole tree, not just the parent: mlagents-learn spawns an
                // env-worker subprocess that inherits our stdout pipe and owns the
                // trainer port. Killing only the parent orphans the worker, which then
                // prints a BrokenPipe/EOFError traceback into the Unity console and
                // keeps port 5004 busy for the next launch.
                if (!TryKillProcessTree(trainerProcess.Id))
                    trainerProcess.Kill();
                trainerProcess.WaitForExit(3000);
            }
        }
        catch (InvalidOperationException) { }

        trainerProcess.Dispose();
        trainerProcess = null;
    }

    // Process.Kill(entireProcessTree) is .NET Core 3+ and not available on Unity's
    // Mono profile, so shell out to taskkill instead.
    private static bool TryKillProcessTree(int pid)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/PID {pid} /T /F",
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var taskkill = Process.Start(psi);
            if (taskkill == null) return false;

            taskkill.WaitForExit(5000);
            return taskkill.HasExited && taskkill.ExitCode == 0;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TrainerLauncher] taskkill failed ({e.Message}); killing the parent process only.");
            return false;
        }
    }

    private bool WaitForPort(int timeoutSeconds)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < deadline)
        {
            if (trainerProcess != null && trainerProcess.HasExited)
            {
                Debug.LogError($"[TrainerLauncher] Python exited (code {trainerProcess.ExitCode}) before becoming ready.");
                return false;
            }

            if (IsPortOpen(port))
                return true;

            Thread.Sleep(500);
        }

        return false;
    }

    private static bool IsPortOpen(int testPort)
    {
        try
        {
            using var tcp = new TcpClient();
            tcp.Connect("127.0.0.1", testPort);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void KillProcessOnPort(int targetPort)
    {
        if (!IsPortOpen(targetPort)) return;

        Debug.Log($"[TrainerLauncher] Port {targetPort} already in use — killing stale process...");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            };

            using var netstat = Process.Start(psi);
            if (netstat == null) return;

            string output = netstat.StandardOutput.ReadToEnd();
            netstat.WaitForExit(5000);

            string search = $":{targetPort}";
            foreach (string line in output.Split('\n'))
            {
                if (!line.Contains(search) || !line.Contains("LISTENING")) continue;

                string[] parts = line.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 5 && int.TryParse(parts[parts.Length - 1], out int pid))
                {
                    try
                    {
                        Process.GetProcessById(pid).Kill();
                        Debug.Log($"[TrainerLauncher] Killed stale process PID {pid}.");
                    }
                    catch (Exception) { }

                    break;
                }
            }

            Thread.Sleep(1000);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TrainerLauncher] Could not clear port {targetPort}: {e.Message}");
        }
    }

    // TODO (deferred until demo/packaging — intentionally NOT done during normal dev):
    // Goal: a self-contained, "double-click and it runs" build that bundles the ENTIRE
    // Python stack (Python + torch + mlagents) INSIDE the artifact, so the user installs
    // nothing themselves. Bloated on purpose — it's a show-off dev tool, not a lean product.
    // Plan:
    //   1. conda-pack the working 'mlagents' env into a relocatable folder shipped next to
    //      the build:  conda pack -n mlagents -o mlagents-env.tar.gz   (env is pinned in
    //      environment.yml at the repo root). Keep the GPU torch for fast on-device training;
    //      only swap to CPU-only torch if you need the demo to run on non-NVIDIA machines.
    //   2. Make this method check that BUNDLED interpreter first (e.g. alongside the
    //      executable / under StreamingAssets), then fall back to the conda / MLAGENTS_PYTHON
    //      search below. Same code path then works in the editor (your conda env) and in a
    //      built demo (the bundled env). This is a release-time artifact, not a dev dependency.
    private static string FindCondaPython()
    {
        string envVar = Environment.GetEnvironmentVariable("MLAGENTS_PYTHON");
        if (!string.IsNullOrEmpty(envVar) && File.Exists(envVar))
            return envVar;

        string condaExe = Environment.GetEnvironmentVariable("CONDA_EXE");
        if (!string.IsNullOrEmpty(condaExe) && File.Exists(condaExe))
        {
            string condaRoot = Path.GetDirectoryName(Path.GetDirectoryName(condaExe));
            string path = Path.Combine(condaRoot, "envs", CondaEnvName, "python.exe");
            if (File.Exists(path)) return path;
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] roots =
        {
            Path.Combine(home, "miniconda3"),
            Path.Combine(home, "anaconda3"),
            @"C:\ProgramData\miniconda3",
            @"C:\ProgramData\anaconda3",
        };

        foreach (string root in roots)
        {
            string path = Path.Combine(root, "envs", CondaEnvName, "python.exe");
            if (File.Exists(path)) return path;
        }

        return null;
    }

    private static void StartOutputPipe(StreamReader reader)
    {
        var thread = new Thread(() =>
        {
            try
            {
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (line != null)
                        Debug.Log("[mlagents] " + line);
                }
            }
            catch (ObjectDisposedException) { }
        });
        thread.IsBackground = true;
        thread.Start();
    }
}
