using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// The single Unity-side manager. Instantiates the population once,
/// creates the correct ITrainingParadigm, and pumps Tick() from FixedUpdate().
/// UI reads from this via GetSnapshot().
/// </summary>
public class SimulationManager : MonoBehaviour
{
    // TODO: Add a controls per step parameter
    [Header("Simulation Setup")]
    [SerializeField] private GameObject jetPrefab;

    [Header("Objective Setup")]
    [Tooltip("Drag a GameObject with an IObjective component here.")]
    [SerializeField] private MonoBehaviour objectiveProvider;

    // ── Runtime state ────────────────────────────────────────────────
    private ITrainingParadigm activeParadigm;
    private IObjective objective;
    private SimulationSettings settings;
    private List<JetAgent> population;
    private JetAgent selectedAgent;

    // Adapter backing ParameterTuners.Hyperparameters. Reads/writes the live
    // settings through () => settings, so it stays valid when LoadState swaps the
    // settings object. Kept so the commit handler can apply staged values and
    // read each descriptor's RequiresReset flag.
    private ModelHyperparameters hyperParams;

    // ── Inference mode ───────────────────────────────────────────────
    // Inference reduces the run to a single jet that replays the saved champion /
    // policy on a loop, owned by the active paradigm. Training is fully torn down
    // while inference is active; nothing learns and nothing is saved. Pressing the
    // toggle key rebuilds the training run from the same save (LoadState).
    private bool inInferenceMode = false;

    // Save-slot key for this run: the active scene (track). Each scene saves
    // independently, so two tracks sharing an objective type (e.g. the FlightSchool
    // tracks) never clobber each other. The objective's Mode is still used for
    // baked defaults; the track is only the on-disk slot identity.
    private static string Track => DataManager.CurrentTrack;

    private void Start()
    {
        // Resolve the objective
        objective = objectiveProvider as IObjective;
        if (objective == null)
        {
            Debug.LogError("[SimulationManager] Objective Provider is missing or does not implement IObjective!");
            return;
        }

        // TODO: Remove in production
        // DataManager.ResetToDefaults(Track, objective.Mode);

        // Load settings for this track (per-scene), seeded from the mode's defaults
        // on first run so each track keeps its own tuning.
        var mode = objective.Mode;
        settings = DataManager.LoadSettings(Track, mode);

        // Apply the AI type chosen in the main menu, if any. When it differs from
        // the loaded settings we swap in a fresh default for the (mode, AIType)
        // pair; when it matches we keep the loaded settings (preserving any saved
        // tuning). A null selection (e.g. pressing Play directly here) keeps the
        // mode's default.
        if (GameSession.SelectedAIType is AIType chosen && chosen != settings.AIType)
        {
            settings = DataManager.GetDefaults(mode, chosen);
            Debug.Log($"[SimulationManager] Using menu-selected AI type: {chosen} for {mode}.");
        }

        // Instantiate the population (factory — done once)
        population = InstantiatePopulation(settings.PopulationSize);

        // Activate the correct sensor on each agent for this mode
        ConfigureSensors(population, objective);

        // Expose the objective's reward parameters for runtime tuning. The UI
        // stages edits on this tuner; committing routes back into
        // CommitRewardParameters below. The objective is long-lived (never
        // recreated by load/inference), so this binding stays valid.
        ParameterTuners.Reward = new ParameterTuner(objective, CommitRewardParameters);

        // Expose the model's hyperparameters as a second, independent tuner. The
        // adapter reads the live settings (which load swaps out) via the closure,
        // and the commit routes hot vs cold per descriptor (see CommitHyperparameters).
        hyperParams = new ModelHyperparameters(() => settings);
        ParameterTuners.Hyperparameters = new ParameterTuner(hyperParams, CommitHyperparameters);

        // TEMP: prove the observers fire. Remove with the rest of the smoke test.
        ParameterTuners.Reward.OnStateChanged += LogRewardTunerState;
        ParameterTuners.Hyperparameters.OnStateChanged += LogHyperTunerState;

        // Create the correct paradigm for the chosen AI type
        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return;

        // Hand the population & objective to the paradigm
        activeParadigm.Initialize(population, settings, objective);
    }

    private void FixedUpdate()
    {
        if (inInferenceMode)
        {
            // The paradigm owns the replay loop (evo pumps the objective + respawns;
            // RL is driven by ML-Agents and this is a no-op).
            activeParadigm?.TickInference();
            return;
        }

        // The paradigm owns the entire lifecycle: step rewards,
        // terminal checks, generation/episode boundaries, resets.
        activeParadigm?.Tick();
    }

    private void Update()
    {
        // Temporary hotkeys for testing save/load until UI buttons are wired up.
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // Save/load only apply to a live training run, not during inference replay.
        if (!inInferenceMode)
        {
            if (keyboard.sKey.wasPressedThisFrame)
                SaveState();

            if (keyboard.lKey.wasPressedThisFrame)
                LoadState();
        }

        // Inference toggle: 'i' replays the saved champion, 't' resumes training.
        if (keyboard.iKey.wasPressedThisFrame)
            EnterInferenceMode();

        if (keyboard.tKey.wasPressedThisFrame)
            ExitInferenceMode();

        // ── TEMP: reward-tuning smoke test (remove once the UI is wired) ──
        // K = stage a bump on the first reward param, C = commit, V = discard.
        if (keyboard.kKey.wasPressedThisFrame) TempStageRewardParam();
        if (keyboard.cKey.wasPressedThisFrame) ParameterTuners.Reward?.Commit();
        if (keyboard.vKey.wasPressedThisFrame) ParameterTuners.Reward?.Discard();

        // ── TEMP: hyperparameter-tuning smoke test (remove once the UI is wired) ──
        // H = stage a HOT knob (round-trip keeps trained state),
        // B = stage a COLD knob (commit rebuilds from scratch),
        // N = commit, M = discard.
        if (keyboard.hKey.wasPressedThisFrame) TempStageHyperParam(wantReset: false);
        if (keyboard.bKey.wasPressedThisFrame) TempStageHyperParam(wantReset: true);
        if (keyboard.nKey.wasPressedThisFrame) ParameterTuners.Hyperparameters?.Commit();
        if (keyboard.mKey.wasPressedThisFrame) ParameterTuners.Hyperparameters?.Discard();
    }

    // ── TEMP: reward-tuning smoke test helpers (remove once the UI is wired) ──
    private void TempStageRewardParam()
    {
        ParameterTuner tuner = ParameterTuners.Reward;
        if (tuner == null || tuner.Descriptors.Count == 0) return;

        ParameterDescriptor d = tuner.Descriptors[0];
        float current = tuner.GetEffectiveValue(d.Key);
        // Bump by 10% of the range, wrapping back to Min once we hit Max.
        float step = (d.Max - d.Min) * 0.1f;
        float next = current + step > d.Max ? d.Min : current + step;

        tuner.Stage(d.Key, next);
        Debug.Log($"[RewardTuneTest] Staged '{d.Key}' -> {next:F2} | live={tuner.GetLiveValue(d.Key):F2} effective={tuner.GetEffectiveValue(d.Key):F2}");
    }

    private void LogRewardTunerState()
    {
        ParameterTuner tuner = ParameterTuners.Reward;
        if (tuner == null) return;
        Debug.Log($"[RewardTuneTest] OnStateChanged — pending changes: {tuner.HasPendingChanges} ({tuner.Pending.Count})");
    }

    // Stages a bump on the first hyperparameter whose RequiresReset matches
    // wantReset, so the smoke test can exercise both the hot (round-trip) and
    // cold (rebuild) commit paths.
    private void TempStageHyperParam(bool wantReset)
    {
        ParameterTuner tuner = ParameterTuners.Hyperparameters;
        if (tuner == null) return;

        foreach (ParameterDescriptor d in tuner.Descriptors)
        {
            if (d.RequiresReset != wantReset) continue;

            float current = tuner.GetEffectiveValue(d.Key);
            float step = (d.Max - d.Min) * 0.1f;
            float next = current + step > d.Max ? d.Min : current + step;

            tuner.Stage(d.Key, next);
            Debug.Log($"[HyperTuneTest] Staged '{d.Key}' ({(d.RequiresReset ? "COLD" : "HOT")}) -> {next:F3} | live={tuner.GetLiveValue(d.Key):F3} effective={tuner.GetEffectiveValue(d.Key):F3}");
            return;
        }

        Debug.Log($"[HyperTuneTest] No {(wantReset ? "COLD" : "HOT")} hyperparameter to stage for {settings.AIType}.");
    }

    private void LogHyperTunerState()
    {
        ParameterTuner tuner = ParameterTuners.Hyperparameters;
        if (tuner == null) return;
        Debug.Log($"[HyperTuneTest] OnStateChanged — pending changes: {tuner.HasPendingChanges} ({tuner.Pending.Count})");
    }

    private void OnDestroy()
    {
        activeParadigm?.Dispose();
        if (ParameterTuners.Reward != null) ParameterTuners.Reward = null;
        if (ParameterTuners.Hyperparameters != null) ParameterTuners.Hyperparameters = null;
    }

    // ── Reward-parameter tuning ──────────────────────────────────────
    // Called by ParameterTuner.Commit() with the user's staged edits. Reward
    // changes are NOT applied on the fly — committing applies them via a clean
    // Save→Load restart so the trained brains/policy carry over and (for RL) the
    // trainer relaunches cleanly with the new reward dynamics. The round-trip is
    // wrapped so the user's real manual save is never overwritten.
    private void CommitRewardParameters(Dictionary<string, float> staged)
    {
        if (objective == null || activeParadigm == null || staged == null || staged.Count == 0)
            return;

        // Apply onto the live objective so SaveState bakes the new values in.
        objective.SetParameters(staged);

        // Reward changes are always "hot" — keep the trained state.
        ApplyHotCommit();

        Debug.Log($"<color=cyan>[SimulationManager]</color> Committed {staged.Count} reward parameter change(s) for {Track}/{settings.AIType}; real save left untouched.");
    }

    // ── Hyperparameter tuning ────────────────────────────────────────
    // Called by ParameterTuners.Hyperparameters.Commit(). The change is applied
    // to the live settings, then adopted one of two ways depending on whether any
    // staged key is "cold" (RequiresReset):
    //   • all hot  → the same protected Save→Load round-trip as reward params, so
    //     the trained brains/policy carry over (RL relaunches the trainer with the
    //     regenerated YAML; evo/NEAT rebuild their engine from the new settings).
    //   • any cold → a full rebuild from scratch. Population size / architecture
    //     make the saved weights incompatible, so trained state is discarded — the
    //     only correct option. The new settings are persisted as the live config.
    private void CommitHyperparameters(Dictionary<string, float> staged)
    {
        if (objective == null || activeParadigm == null || staged == null || staged.Count == 0)
            return;

        bool requiresReset = StagedRequiresReset(staged);

        // Apply onto the live settings so both paths bake the new values in
        // (SaveState clones settings; RebuildFromScratch persists them).
        hyperParams.SetParameters(staged);

        if (requiresReset)
        {
            RebuildFromScratch();
            Debug.Log($"<color=cyan>[SimulationManager]</color> Committed {staged.Count} hyperparameter change(s) for {Track}/{settings.AIType}; a cold change forced a rebuild — trained state was discarded.");
        }
        else
        {
            ApplyHotCommit();
            Debug.Log($"<color=cyan>[SimulationManager]</color> Committed {staged.Count} hyperparameter change(s) for {Track}/{settings.AIType}; trained state kept, real save left untouched.");
        }
    }

    // True if any staged key maps to a descriptor flagged RequiresReset.
    private bool StagedRequiresReset(Dictionary<string, float> staged)
    {
        foreach (ParameterDescriptor d in hyperParams.GetParameterDescriptors())
            if (d.RequiresReset && staged.ContainsKey(d.Key))
                return true;
        return false;
    }

    // Adopts already-applied (live) parameter edits without losing trained state:
    // a Save→Load round-trip wrapped so the user's real manual save is never
    // overwritten — backed up, used purely as a vehicle, then restored (or the
    // temp slot deleted if there was nothing to protect). Shared by reward and
    // hot-hyperparameter commits.
    private void ApplyHotCommit()
    {
        var track = Track;
        var aiType = settings.AIType;

        bool realExisted = DataManager.HasTrainingState(track, aiType);
        if (realExisted) DataManager.BackupTrainingState(track, aiType);

        SaveState();   // current brains/policy + new params → slot
        LoadState();   // rebuild the run from it, re-applying the params

        if (realExisted) DataManager.RestoreTrainingStateBackup(track, aiType);
        else             DataManager.DeleteTrainingState(track, aiType);
    }

    // Tears the run down and rebuilds it fresh from the current (already updated)
    // settings, intentionally discarding trained state. Mirrors the Start() build
    // path minus the settings load; persists the new settings as the live config
    // so a later restart matches. Used for cold hyperparameter changes (population
    // size, architecture) where the saved weights can't be restored.
    private void RebuildFromScratch()
    {
        activeParadigm?.Dispose();
        activeParadigm = null;
        DestroyPopulation();

        DataManager.SaveSettings(Track, settings);

        population = InstantiatePopulation(settings.PopulationSize);
        ConfigureSensors(population, objective);

        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return;

        activeParadigm.Initialize(population, settings, objective);
    }

    // ── Hyperparameter editor (cold path) ────────────────────────────
    // The hyperparameter editor widget routes hot changes through the tuner's
    // normal Commit (ApplyHotCommit). Cold changes can't be adopted in place, so
    // the widget confirms with the user and calls this instead: it bakes the staged
    // values into the live settings, persists them so the reloaded scene picks them
    // up, optionally saves training progress, then reloads the scene.

    /// <summary>
    /// Applies <paramref name="staged"/> hyperparameter values to the live settings,
    /// persists them, optionally saves the current training progress, then reloads
    /// the active scene so the new (cold) values take effect from a clean start.
    /// </summary>
    public void ApplyHyperparametersAndReload(Dictionary<string, float> staged, bool saveProgress)
    {
        if (objective != null && settings != null && staged != null && staged.Count > 0)
        {
            hyperParams.SetParameters(staged);
            DataManager.SaveSettings(Track, settings);
        }

        // Optionally preserve the trained brains/policy on disk before reloading.
        if (saveProgress) SaveState();

        Time.timeScale = 1f;
        if (LoadingOverlay.Instance != null)
            LoadingOverlay.Instance.ReloadActiveScene();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Returns the baked-default hyperparameter values for the current (mode, AI type)
    /// as a flat key → value map, for the editor's "reset to default" action.
    /// </summary>
    public Dictionary<string, float> GetDefaultHyperparameters()
    {
        if (objective == null || settings == null)
            return new Dictionary<string, float>();

        SimulationSettings defaults = DataManager.GetDefaults(objective.Mode, settings.AIType);
        return new ModelHyperparameters(() => defaults).GetParameters();
    }

    /// <summary>
    /// Returns the baked-default reward parameters for the current mode as a flat
    /// key → value map, for the editor's "reset to default" action.
    /// </summary>
    public Dictionary<string, float> GetDefaultRewardParameters()
    {
        if (objective == null)
            return new Dictionary<string, float>();

        return DataManager.GetDefaultRewardParameters(objective.Mode);
    }

    // ── Public API ───────────────────────────────────────────────────

    /// <summary>
    /// Returns a snapshot with paradigm data + manager-level fields.
    /// Called by UIManager every Update().
    /// </summary>
    public SimulationSnapshot GetSnapshot()
    {
        if (activeParadigm == null) return new SimulationSnapshot();

        // The paradigm fills its own snapshot for both training and inference.
        SimulationSnapshot snapshot = activeParadigm.GetSnapshot();

        // Manager stamps on the fields only IT knows about
        snapshot.TimeScale = Time.timeScale;
        snapshot.SelectedAgent = selectedAgent;
        snapshot.TracksAttrition = objective != null && objective.TracksAttrition;
        snapshot.InInferenceMode = inInferenceMode;

        return snapshot;
    }

    public void SelectAgent(JetAgent agent)
    {
        selectedAgent = agent;
    }

    public IBrain GetChampionBrain()
    {
        return activeParadigm?.GetChampionBrain();
    }

    public float GetChampionScore()
    {
        return activeParadigm.GetChampionScore();
    }

    public void SaveChampion()
    {
        if (activeParadigm == null) return;

        string dir = DataManager.TrackPath(Track);
        DataManager.EnsureDirectory(dir);
        activeParadigm.SaveChampion(dir);
    }

    /// <summary>
    /// Saves the full training state (all brains + AI/objective params + stats)
    /// for the current mode and AI type, overwriting any previous save.
    /// </summary>
    public void SaveState()
    {
        if (activeParadigm == null) return;
        activeParadigm.SaveState();
    }

    /// <summary>
    /// Completely resets the simulation and rebuilds it from the saved run for
    /// the current mode + AI type: re-applies the saved settings and objective
    /// parameters, re-instantiates the population at the saved size, and loads
    /// the saved brains. Training then resumes normally.
    /// </summary>
    public void LoadState()
    {
        if (objective == null) return;

        TrainingSaveData data = DataManager.LoadTrainingState(Track, settings.AIType);
        if (data == null)
        {
            Debug.LogWarning($"[SimulationManager] No saved state found for {Track}/{settings.AIType}.");
            return;
        }

        // Tear down the current run
        activeParadigm?.Dispose();
        activeParadigm = null;
        DestroyPopulation();

        // Re-apply saved settings (population size, mutation rate, shape, ...)
        // and persist them so a later restart matches the loaded run.
        if (data.Settings != null)
        {
            settings = data.Settings;
            DataManager.SaveSettings(Track, settings);
        }

        // Re-apply saved objective parameters
        objective.SetParameters(data.ObjectiveParameters);

        // Rebuild the population at the saved size and re-wire sensors
        population = InstantiatePopulation(settings.PopulationSize);
        ConfigureSensors(population, objective);

        // Recreate the paradigm, seed it, then overwrite the fresh brains with the saved ones
        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return;

        activeParadigm.Initialize(population, settings, objective);
        activeParadigm.LoadState();

        Debug.Log($"[SimulationManager] Loaded saved {settings.AIType} run for track {Track} ({objective.Mode}).");
    }

    private void DestroyPopulation()
    {
        if (population == null) return;

        foreach (JetAgent jet in population)
            if (jet != null) Destroy(jet.gameObject);

        population.Clear();
        selectedAgent = null;
    }

    // ── Inference mode ───────────────────────────────────────────────

    /// <summary>
    /// Tears down the training run, reduces the scene to a single jet, and hands
    /// that jet to the paradigm to replay the saved champion/policy on repeat.
    /// Only works when a saved run exists and the active paradigm supports
    /// inference. Triggering inference never writes a save of its own; the actual
    /// replay (and its per-step loop) is owned by the paradigm.
    /// </summary>
    public void EnterInferenceMode()
    {
        if (inInferenceMode) return;
        if (objective == null || activeParadigm == null) return;

        if (!activeParadigm.CanRunInference)
        {
            Debug.LogWarning($"[SimulationManager] Inference mode is not yet implemented for {settings.AIType}.");
            return;
        }

        // Inference replays a saved run, so one must exist first.
        if (!DataManager.HasTrainingState(Track, settings.AIType))
        {
            Debug.LogWarning($"[SimulationManager] No saved run for {Track}/{settings.AIType}. Train and save (S) before entering inference.");
            return;
        }

        // Stop training and clear the whole population off the screen, then rebuild
        // a fresh single-jet run for the paradigm to drive in inference.
        activeParadigm.Dispose();
        activeParadigm = null;
        DestroyPopulation();

        population = InstantiatePopulation(1);
        ConfigureSensors(population, objective);

        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return;

        activeParadigm.Initialize(population, settings, objective);

        if (!activeParadigm.StartInference())
        {
            Debug.LogWarning("[SimulationManager] Could not start inference; rebuilding the training run from the save.");
            LoadState();
            return;
        }

        inInferenceMode = true;
        Debug.Log($"<color=yellow>[SimulationManager]</color> Inference ON for {Track}/{settings.AIType}. Press 'T' to resume training from the save.");
    }

    /// <summary>
    /// Leaves inference mode and rebuilds the training run from the same save the
    /// champion came from, so training continues exactly where it left off.
    /// </summary>
    public void ExitInferenceMode()
    {
        if (!inInferenceMode) return;

        inInferenceMode = false;

        // LoadState disposes the inference paradigm (evo: nothing; RL: kills the
        // inference trainer + recycles the Academy) and rebuilds the training run
        // from the save, resuming where it left off.
        LoadState();
        Debug.Log("<color=yellow>[SimulationManager]</color> Inference OFF. Resumed training from the saved run.");
    }

    // ── Private helpers ──────────────────────────────────────────────

    private void ConfigureSensors(List<JetAgent> population, IObjective objective)
    {
        SensorType required = objective.RequiredSensorType;

        foreach (var jetAgent in population)
        {
            ISensor activeSensor = null;
            int matchCount = 0;

            foreach (var sensor in jetAgent.GetComponents<ISensor>())
            {
                if (sensor is MonoBehaviour mb)
                {
                    bool match = sensor.SensorType == required;
                    mb.enabled = match;
                    if (match)
                    {
                        matchCount++;
                        // Keep the FIRST match so this agrees with the GetComponent<T>()
                        // calls elsewhere (e.g. objectives writing currentWaypoint).
                        if (activeSensor == null) activeSensor = sensor;
                    }
                }
            }

            if (matchCount > 1)
                Debug.LogWarning($"[SimulationManager] {jetAgent.name} has {matchCount} sensors of type {required}. Only one per type is supported — remove the duplicates from the jet prefab, or the wrong instance may be used.");

            if (activeSensor != null)
                jetAgent.Sensor = activeSensor;
            else
                Debug.LogWarning($"[SimulationManager] No sensor of type {required} found on {jetAgent.name}. Add the matching sensor component to the jet prefab.");
        }

        // The mode's active sensor is the source of truth for the input width, so
        // any AI type works in any mode regardless of the baked default's nominal
        // InputSize. Run after wiring so the active sensor is resolved.
        ApplyObservationSizeFromSensors(population);
    }

    // Stamps the active sensor's observation count onto the settings' input width.
    // Output size is fixed by the flight controls (see JetAgent) and left as-is.
    private void ApplyObservationSizeFromSensors(List<JetAgent> population)
    {
        if (settings == null || population == null || population.Count == 0) return;

        ISensor sensor = population[0].Sensor;
        if (sensor == null) return;

        int observationSize = sensor.GetSensorCount();

        if (settings.NeatSettings != null) settings.NeatSettings.InputSize = observationSize;
        if (settings.RLSettings != null) settings.RLSettings.InputSize = observationSize;

        int[] shape = settings.NeuroEvoSettings?.NetworkShape;
        if (shape != null && shape.Length > 0) shape[0] = observationSize;
    }

    private List<JetAgent> InstantiatePopulation(int count)
    {
        var pop = new List<JetAgent>(count);

        for (int i = 0; i < count; i++)
        {
            GameObject jetObject = Instantiate(jetPrefab);
            JetAgent agent = jetObject.GetComponent<JetAgent>();
            pop.Add(agent);
        }

        return pop;
    }

    private ITrainingParadigm CreateParadigm(AIType type)
    {        
        switch (type)
        {
            case AIType.FixedNeuroEvo:
                return new EvolutionaryParadigm(new ClassicNeuroEvoEngine());
            case AIType.NEAT:
                return new EvolutionaryParadigm(new NeatEngine());
            case AIType.PPO_MLAgents:
            case AIType.SAC_MLAgents:
                return new RLParadigm();
            default:
                Debug.LogError($"[SimulationManager] Unsupported AI type: {type}");
                return null;
        }
    }
}
