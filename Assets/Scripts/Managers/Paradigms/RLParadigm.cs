using System;
using System.Collections.Generic;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

public class RLParadigm : ITrainingParadigm
{
    private IObjective objective;
    private SimulationSettings settings;
    private List<JetAgent> population;
    private List<JetMLAgent> mlAgents;

    // Per-episode reward tracking (NOT cumulative across the run): lastEpisodeScores[i]
    // is agent i's most recent completed-episode reward — i.e. the points that jet
    // earned over its last life (birth to death). hasReported[i] marks whether that
    // jet has finished at least one life yet, so the live MAX/AVG only count jets
    // that have actually scored (early zeros don't drag them down).
    private float[] lastEpisodeScores;
    private bool[] hasReported;
    private int totalEpisodes;

    // All-time best single life ever — kept only for the saved "champion score",
    // NOT shown live (the live MAX is the current population's best last life, so it
    // stays comparable to the AVG). Starts at -inf so the first episode registers.
    private float bestEpisodeScore = float.NegativeInfinity;
    private float trainingStartTime;
    private float elapsedBeforeCurrentSession;

    // bestEpisodeScore before any episode completes is the -inf sentinel; report 0
    // instead so the save metadata never shows "-Infinity".
    private float ReportedBest => float.IsNegativeInfinity(bestEpisodeScore) ? 0f : bestEpisodeScore;

    // Live MAX/AVG of jets' last-life scores, over jets that have finished a life.
    // One shared definition feeding both the numbers widget and the history graph,
    // so MAX is just the top of the same quantity AVG averages — directly comparable.
    private void ComputeCurrentStats(out float max, out float avg)
    {
        float sum = 0f;
        max = float.NegativeInfinity;
        int n = 0;

        for (int i = 0; i < lastEpisodeScores.Length; i++)
        {
            if (!hasReported[i]) continue;
            float s = lastEpisodeScores[i];
            if (s > max) max = s;
            sum += s;
            n++;
        }

        if (n == 0) { max = 0f; avg = 0f; }
        else avg = sum / n;
    }

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

    // ML-Agents forces windowed mode via Screen.SetResolution(w, h, false) when the
    // trainer connects, kicking the player out of fullscreen the moment an RL scene
    // starts. Capture the user's chosen mode/resolution before the connect and re-assert
    // it for a short window of ticks afterwards — the engine-config side channel that
    // does the windowing arrives a few frames after StartTrainer returns, so a one-shot
    // restore would race it. Cosmetic only: observations are vector-based, not visual,
    // so the render resolution never affects training.
    private FullScreenMode desiredFullScreenMode;
    private bool restoreFullScreen;
    private int restoreFullScreenTicks;
    private int desiredScreenWidth;
    private int desiredScreenHeight;

    private string AlgorithmName => settings.AIType == AIType.SAC_MLAgents ? "SAC" : "PPO";
    private string YamlFileName => settings.AIType == AIType.SAC_MLAgents ? "jet_sac.yaml" : "jet_ppo.yaml";
    private string YamlPath => Path.Combine(Application.dataPath, "..", "config", YamlFileName);

    // Stable, per-(track, AI type) run-id so each scene/algorithm combination owns
    // its own results/<run-id>/ directory and can be resumed independently — two
    // tracks sharing an objective type get separate run-ids. DataManager.CurrentTrack
    // is already sanitized (no spaces / path separators), so it is safe to drop
    // straight into the mlagents --run-id argument.
    private string RunId => $"{DataManager.CurrentTrack}_{settings.AIType}";

    private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

    // Live directory the trainer writes checkpoints to (mlagents-learn writes a
    // checkpoint every checkpoint_interval steps, see RLSettings.ToYaml).
    private string ResultsDir => Path.Combine(ProjectRoot, "results", RunId);

    // Persistent save slot: a snapshot copy of ResultsDir kept under the same
    // GameData/<track>/ folder the rest of the save system uses. Decoupled from the
    // live results dir so it survives a later --force and acts as a stable restore
    // point, mirroring how the evolutionary save captures the population at save time.
    private string SaveCheckpointDir => Path.Combine(DataManager.TrackPath(DataManager.CurrentTrack), $"rl_checkpoint_{settings.AIType}");

    public void Initialize(List<JetAgent> population, SimulationSettings settings, IObjective objective)
    {
        this.population = population;
        this.settings = settings;
        this.objective = objective;

        mlAgents = new List<JetMLAgent>(population.Count);
        lastEpisodeScores = new float[population.Count];
        hasReported = new bool[population.Count];

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
    private void StartTrainer(bool resume, bool inference = false)
    {
        trainerStarted = true;

        // Snapshot the player's current fullscreen state BEFORE the connect windows it.
        // Tick() re-asserts this for restoreFullScreenTicks frames once the engine-config
        // message has done its (unwanted) windowing.
        desiredFullScreenMode = Screen.fullScreenMode;
        restoreFullScreen = Screen.fullScreen;
        desiredScreenWidth = Display.main.systemWidth;
        desiredScreenHeight = Display.main.systemHeight;
        restoreFullScreenTicks = restoreFullScreen ? 90 : 0;

        WriteYamlConfig();

        // --force does NOT clear old checkpoint files, so a fresh run would leave
        // a previously staged checkpoint.pt lying around — and SaveState would
        // silently snapshot that stale state instead of this run's. Wipe the live
        // results so a fresh run's saves can only contain its own data. Inference
        // always resumes, so this never runs for it.
        if (!resume)
            ClearLiveResults();

        trainerLauncher = new TrainerProcessLauncher();
        if (!trainerLauncher.Launch($"config/{YamlFileName}", RunId, resume, inference))
        {
            Debug.LogError($"[RLParadigm] Python trainer failed to start ({AlgorithmName}). Agents will fall back to heuristic mode.");
        }
        else if (inference)
        {
            Debug.Log($"[RLParadigm] Replaying saved {AlgorithmName} policy in inference mode (no learning, run-id {RunId}).");
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

            // Configure the behavior BEFORE adding/enabling Agent. JetMLAgent's
            // Agent base class requires BehaviorParameters and can register with
            // the Academy as soon as it is added. If Unity auto-creates the
            // component at that point, its default ActionSpec has zero actions and
            // the Python policy crashes in action_model with an empty tensor list.
            var behavior = go.GetComponent<BehaviorParameters>();
            if (behavior == null)
                behavior = go.AddComponent<BehaviorParameters>();
            ConfigureBehaviorParameters(jetAgent, behavior);

            var mlAgent = go.GetComponent<JetMLAgent>();
            if (mlAgent == null)
                mlAgent = go.AddComponent<JetMLAgent>();

            ConfigureDecisionRequester(go);

            mlAgent.Inject(objective, this, i, population.Count);
            mlAgent.enabled = true;
            mlAgents.Add(mlAgent);

            jetAgent.ResetAgent();
            objective.SetStartingState(jetAgent, i, population.Count);
            go.SetActive(true);
        }
    }

    private void ConfigureBehaviorParameters(JetAgent jetAgent, BehaviorParameters bp)
    {
        var rl = settings.RLSettings;

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

        // Undo ML-Agents' connect-time switch to windowed: if the user was fullscreen
        // and the engine-config message has since dropped us to windowed, put them back.
        // Stops re-checking once the window expires (or once it sticks, since the guard
        // below is false when already in the desired mode).
        if (restoreFullScreenTicks > 0)
        {
            restoreFullScreenTicks--;
            if (restoreFullScreen && !Screen.fullScreen)
                Screen.SetResolution(desiredScreenWidth, desiredScreenHeight, desiredFullScreenMode);
        }

        cachedSnapshot.AgentsAlive = population.Count;
    }

    public SimulationSnapshot GetSnapshot()
    {
        ComputeCurrentStats(out float curMax, out float curAvg);

        cachedSnapshot.IterationNumber = totalEpisodes;
        cachedSnapshot.ChampionScore = ReportedBest; // all-time, used by inference/save
        cachedSnapshot.RLData.TotalEpisodes = totalEpisodes;
        cachedSnapshot.RLData.TrainingTime =
            elapsedBeforeCurrentSession + Mathf.Max(0f, Time.time - trainingStartTime);
        cachedSnapshot.ElapsedTime = cachedSnapshot.RLData.TrainingTime;
        cachedSnapshot.RLData.CurrentMax = curMax;
        cachedSnapshot.RLData.CurrentAvg = curAvg;

        return cachedSnapshot;
    }

    public void RecordEpisodeEnd(int agentIndex, float episodeReward)
    {
        totalEpisodes++;

        // Record THIS life's reward, replacing the agent's previous one — these are
        // per-life figures, never a running sum (that grew without bound).
        lastEpisodeScores[agentIndex] = episodeReward;
        hasReported[agentIndex] = true;

        if (episodeReward > bestEpisodeScore)
            bestEpisodeScore = episodeReward;
    }

    public IBrain GetChampionBrain()
    {
        return null;
    }

    public IBrain LoadChampionBrain()
    {
        // RL has no in-process IBrain — the policy lives in the trainer/checkpoint
        // and is replayed by launching mlagents-learn with --inference (see
        // StartInference). This hook is only meaningful for the in-memory
        // evolutionary brains, so it stays null here.
        return null;
    }

    // ── Inference replay ─────────────────────────────────────────────

    public bool CanRunInference => true;

    public bool StartInference()
    {
        // Replay the SAVED policy, not the live run: stage the saved checkpoint
        // into the live results dir (same as a resume) so the trainer can load it.
        if (!StageSavedCheckpoint())
        {
            Debug.LogWarning($"[RLParadigm] No usable checkpoint in the save for {DataManager.CurrentTrack}/{settings.AIType}; cannot run inference. Train, save, then retry.");
            return false;
        }

        cachedSnapshot.ParadigmName = $"Inference ({AlgorithmName})";

        // --resume loads the staged checkpoint; --inference runs it with no
        // learning. The single jet the manager spawned binds to this trainer and
        // its episodes auto-respawn, looping the course exactly like training but
        // with a frozen policy.
        StartTrainer(resume: true, inference: true);
        return true;
    }

    public void TickInference()
    {
        // ML-Agents + the inference trainer drive the lone agent (observe → act →
        // EndEpisode → OnEpisodeBegin respawns). Nothing to pump here; just keep
        // the snapshot's alive count current.
        cachedSnapshot.AgentsAlive = population.Count;
    }

    public float GetChampionScore()
    {
        return ReportedBest;
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

        ComputeCurrentStats(out float topScore, out float average);

        var data = new TrainingSaveData
        {
            AIType = settings.AIType,
            Mode = objective.Mode,
            Track = DataManager.CurrentTrack,
            Settings = settings.Clone(),
            ObjectiveParameters = objective.GetParameters(),
            Generation = totalEpisodes,
            PopulationSize = population.Count,
            ChampionScore = ReportedBest,
            TopScore = topScore,
            AverageScore = average,
            TrainingElapsedSeconds =
                elapsedBeforeCurrentSession + Mathf.Max(0f, Time.time - trainingStartTime),
            SavedAtUtc = DateTime.UtcNow.ToString("o"),
            // Opaque marker for the RL path: the run-id whose checkpoint snapshot
            // backs this save. Non-empty so HasTrainingState / load checks pass.
            EngineState = RunId,
        };

        DataManager.SaveTrainingState(DataManager.CurrentTrack, settings.AIType, data);
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

        TrainingSaveData data = DataManager.LoadTrainingState(DataManager.CurrentTrack, settings.AIType);
        if (data != null)
        {
            // Carry the all-time best episode forward across the load. (Saves made
            // before the per-episode fix stored a runaway accumulated value, so an
            // old save will show an inflated BEST until you re-save with current code.)
            bestEpisodeScore = data.ChampionScore;
            totalEpisodes = data.Generation;
            elapsedBeforeCurrentSession = Mathf.Max(0f, data.TrainingElapsedSeconds);
            trainingStartTime = Time.time;
        }

        bool resume = StageSavedCheckpoint();
        if (!resume)
            Debug.LogWarning($"[RLParadigm] No usable checkpoint in the save for {DataManager.CurrentTrack}/{settings.AIType}; starting a fresh run instead.");

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
