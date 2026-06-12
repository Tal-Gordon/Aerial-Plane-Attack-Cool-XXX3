using System;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

public class RLParadigm : ITrainingParadigm
{
    // TODO: Fix widget on all AI types
    private IObjective objective;
    private SimulationSettings settings;
    private List<JetAgent> population;
    private List<JetMLAgent> mlAgents;

    private float[] cumulativeScores;
    private int totalEpisodes;
    private float bestCumulativeScore;
    private float trainingStartTime;

    private SimulationSnapshot cachedSnapshot;
    private TrainerProcessLauncher trainerLauncher;
    private DateTime trainerLaunchedUtc;

    // ML-Agents binds the Unity<->trainer connection when the first agent (or
    // DecisionRequester) touches Academy.Instance. Academy.Dispose() resets that
    // lazy singleton, so the trainer CAN be swapped mid-session: shut the stack
    // down (ShutdownTrainer), launch a new trainer, re-enable the agents, and the
    // fresh Academy binds to it. That is how LoadState resumes a saved checkpoint
    // without leaving Play mode.
    private bool trainerStarted;

    private string AlgorithmName => settings.AIType == AIType.SAC_MLAgents ? "SAC" : "PPO";
    private string YamlFileName => settings.AIType == AIType.SAC_MLAgents ? "jet_sac.yaml" : "jet_ppo.yaml";
    private string YamlPath => Path.Combine(Application.dataPath, "..", "config", YamlFileName);

    // Stable, per-(mode, AI type) run-id so each objective/algorithm combination
    // owns its own results/<run-id>/ directory and can be resumed independently.
    private string RunId => $"{objective.Mode}_{settings.AIType}";

    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    // Live directory the trainer writes checkpoints to (mlagents-learn writes a
    // checkpoint every checkpoint_interval steps, see RLSettings.ToYaml).
    private string ResultsDir => Path.Combine(ProjectRoot, "results", RunId);

    // Persistent save slot: a snapshot copy of ResultsDir kept under the same
    // GameData/<Mode>/ folder the rest of the save system uses. Decoupled from the
    // live results dir so it survives a later --force and acts as a stable restore
    // point, mirroring how the evolutionary save captures the population at save time.
    private string SaveCheckpointDir => Path.Combine(DataManager.ModePath(objective.Mode), $"rl_checkpoint_{settings.AIType}");

    public void Initialize(List<JetAgent> population, SimulationSettings settings, IObjective objective)
    {
        this.population = population;
        this.settings = settings;
        this.objective = objective;

        mlAgents = new List<JetMLAgent>(population.Count);
        cumulativeScores = new float[population.Count];

        trainingStartTime = Time.time;

        cachedSnapshot = new SimulationSnapshot
        {
            ParadigmName = $"RL ({AlgorithmName})",
            Population = population,
            RLData = new RLSnapshot()
        };

        // The trainer is deliberately NOT started here. A plain Play starts it
        // fresh on the first Tick; SimulationManager's load flow calls LoadState()
        // right after Initialize, which starts it in resume mode instead. Deferring
        // the start is what lets load avoid booting a fresh trainer just to kill it.
    }

    // Launches mlagents-learn (fresh, or resuming from a staged checkpoint) and
    // only then wires up the agents: the first agent/DecisionRequester to touch
    // Academy.Instance creates the Academy, which immediately tries to bind to
    // the trainer — so the trainer must already be listening on the port.
    private void StartTrainer(bool resume)
    {
        trainerStarted = true;

        WriteYamlConfig();

        // --force does NOT clear old checkpoint files, so a fresh run would leave
        // a previously staged checkpoint.pt lying around — and SaveState would
        // silently snapshot that stale state instead of this run's. Wipe the live
        // results so a fresh run's saves can only contain its own data.
        if (!resume)
            ClearLiveResults();

        trainerLauncher = new TrainerProcessLauncher();
        if (!trainerLauncher.Launch($"config/{YamlFileName}", RunId, resume))
        {
            Debug.LogError($"[RLParadigm] Python trainer failed to start ({AlgorithmName}). Agents will fall back to heuristic mode.");
        }
        else if (resume)
        {
            Debug.Log($"[RLParadigm] Resumed {AlgorithmName} training from saved checkpoint (run-id {RunId}).");
        }

        // Anything in results/ written after this moment was checkpointed by
        // THIS trainer; anything older was staged from a previous save.
        trainerLaunchedUtc = DateTime.UtcNow;

        mlAgents.Clear();
        for (int i = 0; i < population.Count; i++)
        {
            var jetAgent = population[i];
            var go = jetAgent.gameObject;

            var mlAgent = go.GetComponent<JetMLAgent>();
            if (mlAgent == null)
                mlAgent = go.AddComponent<JetMLAgent>();

            ConfigureBehaviorParameters(jetAgent);
            ConfigureDecisionRequester(go);

            mlAgent.Inject(objective, this, i, population.Count);
            mlAgent.enabled = true;
            mlAgents.Add(mlAgent);

            jetAgent.ResetAgent();
            objective.SetStartingState(jetAgent, i, population.Count);
            go.SetActive(true);
        }
    }

    private void ConfigureBehaviorParameters(JetAgent jetAgent)
    {
        var rl = settings.RLSettings;
        var bp = jetAgent.gameObject.GetComponent<BehaviorParameters>();

        // The observation size must match the active sensor exactly or ML-Agents throws
        // at connect. Derive it from the sensor so the two can't silently drift; treat
        // RLSettings.InputSize as a sanity check and warn (don't fail) on mismatch.
        int observationSize = jetAgent.Sensor?.GetSensorCount() ?? rl.InputSize;
        if (jetAgent.Sensor != null && observationSize != rl.InputSize)
            Debug.LogWarning($"[RLParadigm] RLSettings.InputSize ({rl.InputSize}) does not match the active sensor's count ({observationSize}). Using the sensor count; update InputSize to silence this.");

        bp.BrainParameters.VectorObservationSize = observationSize;
        bp.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(rl.OutputSize);
        bp.BehaviorName = "JetBrain";
        bp.BehaviorType = BehaviorType.Default;
    }

    private void ConfigureDecisionRequester(GameObject go)
    {
        // Always recreate: DecisionRequester subscribes to the Academy in Awake
        // and only unsubscribes in OnDestroy, so an instance from before an
        // Academy recycle stays wired to the dead Academy and never requests
        // decisions again.
        var stale = go.GetComponent<DecisionRequester>();
        if (stale != null)
            UnityEngine.Object.DestroyImmediate(stale);

        var dr = go.AddComponent<DecisionRequester>();
        dr.DecisionPeriod = settings.RLSettings.DecisionPeriod;
        dr.TakeActionsBetweenDecisions = true;
    }

    public void Tick()
    {
        if (!trainerStarted)
            StartTrainer(resume: false);

        cachedSnapshot.AgentsAlive = population.Count;
    }

    public SimulationSnapshot GetSnapshot()
    {
        cachedSnapshot.IterationNumber = totalEpisodes;
        cachedSnapshot.ChampionScore = bestCumulativeScore;
        cachedSnapshot.RLData.TotalEpisodes = totalEpisodes;
        cachedSnapshot.RLData.TrainingTime = Time.time - trainingStartTime;
        cachedSnapshot.RLData.BestCumulativeScore = bestCumulativeScore;
        cachedSnapshot.RLData.CumulativeScores = cumulativeScores;

        return cachedSnapshot;
    }

    public void RecordEpisodeEnd(int agentIndex, float episodeReward)
    {
        totalEpisodes++;
        cumulativeScores[agentIndex] += episodeReward;

        if (cumulativeScores[agentIndex] > bestCumulativeScore)
            bestCumulativeScore = cumulativeScores[agentIndex];
    }

    public IBrain GetChampionBrain()
    {
        return null;
    }

    public float GetChampionScore()
    {
        return bestCumulativeScore;
    }

    public void SaveChampion(string directoryPath)
    {
        Debug.Log("[RLParadigm] Champion saving not yet implemented. Train via mlagents-learn and export the .onnx model.");
    }

    public void SaveState()
    {
        // ML-Agents owns the weights: the trainer auto-writes checkpoints into
        // results/<run-id>/ every checkpoint_interval steps. Saving snapshots that
        // directory into a persistent slot (a stable restore point that survives
        // later fresh runs) and writes a TrainingSaveData so settings, objective
        // params and stats round-trip through the same generic save system the
        // evolutionary modes use. LoadState stages the slot back and resumes it.
        if (!Directory.Exists(ResultsDir))
        {
            Debug.LogWarning($"[RLParadigm] No results to snapshot at {ResultsDir}. The trainer writes its first checkpoint after checkpoint_interval steps (see RLSettings.ToYaml) — train longer, then save again.");
            return;
        }

        try
        {
            CopyDirectory(ResultsDir, SaveCheckpointDir);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RLParadigm] Failed to snapshot checkpoints: {e.Message}");
            return;
        }

        if (!HasCheckpointFile(SaveCheckpointDir))
            Debug.LogWarning($"[RLParadigm] Snapshot contains no .pt checkpoint yet; a resume from this save will fall back to a fresh run until the trainer has checkpointed at least once.");
        else if (TryGetLatestCheckpointStep(out int checkpointStep, out bool fromThisSession) && fromThisSession)
            Debug.Log($"[RLParadigm] Save captured the trainer's checkpoint at step {checkpointStep}. Progress since that checkpoint (up to {settings.RLSettings.CheckpointInterval} steps) is not included.");
        else
            Debug.LogWarning($"[RLParadigm] Save taken, but the trainer hasn't written a NEW checkpoint this session (it checkpoints every {settings.RLSettings.CheckpointInterval} steps) — loading this save resumes from the previously saved step, not from current progress. Train past the next checkpoint and save again to capture it.");

        float topScore = float.NegativeInfinity;
        float sum = 0f;
        for (int i = 0; i < cumulativeScores.Length; i++)
        {
            if (cumulativeScores[i] > topScore) topScore = cumulativeScores[i];
            sum += cumulativeScores[i];
        }
        int count = cumulativeScores.Length;
        float average = count > 0 ? sum / count : 0f;
        if (count == 0) topScore = 0f;

        var data = new TrainingSaveData
        {
            AIType = settings.AIType,
            Mode = objective.Mode,
            Settings = settings.Clone(),
            ObjectiveParameters = objective.GetParameters(),
            Generation = totalEpisodes,
            PopulationSize = population.Count,
            ChampionScore = bestCumulativeScore,
            TopScore = topScore,
            AverageScore = average,
            SavedAtUtc = DateTime.UtcNow.ToString("o"),
            // Opaque marker for the RL path: the run-id whose checkpoint snapshot
            // backs this save. Non-empty so HasTrainingState / load checks pass.
            EngineState = RunId,
        };

        DataManager.SaveTrainingState(objective.Mode, settings.AIType, data);
    }

    public void LoadState()
    {
        // Runs after SimulationManager tore down the previous run (Dispose closed
        // the old Academy and killed its trainer) and re-initialized this paradigm
        // with the saved settings. The trainer hasn't started yet — Initialize
        // defers that — so it can be started directly in resume mode: stage the
        // saved checkpoint into results/<run-id>/ and launch with --resume. The
        // agents enabled by StartTrainer then bind a fresh Academy to the new
        // trainer, making this a true in-place, mid-simulation load.
        if (trainerStarted)
            ShutdownTrainer(); // direct call outside the manager flow — recycle first

        TrainingSaveData data = DataManager.LoadTrainingState(objective.Mode, settings.AIType);
        if (data != null)
        {
            bestCumulativeScore = data.ChampionScore;
            totalEpisodes = data.Generation;
        }

        bool resume = StageSavedCheckpoint();
        if (!resume)
            Debug.LogWarning($"[RLParadigm] No usable checkpoint in the save for {objective.Mode}/{settings.AIType}; starting a fresh run instead.");

        StartTrainer(resume);
    }

    // Stages the saved checkpoint into the live results dir so the trainer can
    // --resume from it. False when the save has no usable checkpoint.
    private bool StageSavedCheckpoint()
    {
        if (!Directory.Exists(SaveCheckpointDir) || !HasCheckpointFile(SaveCheckpointDir))
            return false;

        try
        {
            CopyDirectory(SaveCheckpointDir, ResultsDir);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RLParadigm] Failed to stage saved checkpoint for resume: {e.Message}. Starting fresh instead.");
            return false;
        }
    }

    private static bool HasCheckpointFile(string dir) =>
        Directory.Exists(dir) && Directory.GetFiles(dir, "*.pt", SearchOption.AllDirectories).Length > 0;

    // Finds the newest numbered checkpoint (JetBrain-<step>.pt) in the live
    // results by write time, and whether the running trainer wrote it — files
    // older than the launch were staged from a previous save, so a save that
    // only contains those does not capture this session's progress. The step
    // is only trustworthy when fromThisSession is true: staged files share one
    // copy timestamp, so 'newest' among them is arbitrary.
    private bool TryGetLatestCheckpointStep(out int step, out bool fromThisSession)
    {
        step = 0;
        DateTime newest = DateTime.MinValue;

        foreach (string file in Directory.GetFiles(ResultsDir, "*.pt", SearchOption.AllDirectories))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            int dash = name.LastIndexOf('-');
            if (dash < 0 || !int.TryParse(name.Substring(dash + 1), out int fileStep))
                continue;

            DateTime written = File.GetLastWriteTimeUtc(file);
            if (written > newest)
            {
                newest = written;
                step = fileStep;
            }
        }

        fromThisSession = newest > trainerLaunchedUtc;
        return step > 0;
    }

    // A fresh (--force) launch does not clean results/<run-id>/ — stale staged
    // checkpoints would survive and poison the next save. Locked files (e.g. a
    // tensorboard tail) just downgrade to the trainer's own leftover handling.
    private void ClearLiveResults()
    {
        if (!Directory.Exists(ResultsDir)) return;

        try
        {
            Directory.Delete(ResultsDir, true);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Debug.LogWarning($"[RLParadigm] Could not fully clear {ResultsDir} before a fresh run: {e.Message}");
        }
    }

    // Mirrors a directory tree into dest (replacing it). Files are opened with a
    // shared read so a running trainer holding handles (e.g. tensorboard event
    // files) doesn't block the snapshot; an individually locked file is skipped
    // with a warning rather than aborting the whole copy.
    private static void CopyDirectory(string source, string dest)
    {
        if (Directory.Exists(dest))
            Directory.Delete(dest, true);
        Directory.CreateDirectory(dest);

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string target = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target));

            try
            {
                using var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
                src.CopyTo(dst);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[RLParadigm] Skipped locked file during copy: {Path.GetFileName(file)} ({e.Message})");
            }
        }
    }

    private void WriteYamlConfig()
    {
        string dir = Path.GetDirectoryName(YamlPath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        string yaml = settings.RLSettings.ToYaml(settings.AIType);
        File.WriteAllText(YamlPath, yaml);
        Debug.Log($"[RLParadigm] Training config written to {YamlPath}");
    }

    public void Dispose()
    {
        ShutdownTrainer();
    }

    // Tears down the live ML-Agents stack so a new trainer can be bound later in
    // the SAME Play session. Order matters: agents are disabled first so their
    // unregistration runs against the still-live Academy; the trainer tree is
    // killed before the Academy is disposed so the Python side never sees a
    // disconnect (it would start respawning its env worker, racing the kill);
    // disposing the Academy last resets its lazy singleton so the next agent
    // enable can bind a fresh one (the package is built for this — see
    // Agent.OnDisable).
    private void ShutdownTrainer()
    {
        if (mlAgents != null)
        {
            foreach (var mlAgent in mlAgents)
            {
                if (mlAgent != null)
                    mlAgent.enabled = false;
            }
        }

        trainerLauncher?.Dispose();
        trainerLauncher = null;

        if (Academy.IsInitialized)
            Academy.Instance.Dispose();

        trainerStarted = false;
    }
}
