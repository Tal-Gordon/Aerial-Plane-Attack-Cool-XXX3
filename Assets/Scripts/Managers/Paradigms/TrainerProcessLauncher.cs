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

    public bool Launch(string yamlRelativePath, string runId = "training", int timeoutSeconds = 60)
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

        var psi = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"-u -m mlagents.trainers.learn \"{yamlRelativePath}\" --run-id={runId} --force",
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
                Debug.Log("[TrainerLauncher] Killing trainer process...");
                trainerProcess.Kill();
                trainerProcess.WaitForExit(3000);
            }
        }
        catch (InvalidOperationException) { }

        trainerProcess.Dispose();
        trainerProcess = null;
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
