using System.Collections.Generic;
using UnityEngine;
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
    [Tooltip("Parent for all runtime-instantiated jets. A child named 'Jets' is created automatically when left empty.")]
    [SerializeField] private Transform jetParent;

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

    // Saved-run challenge state: one frozen-policy AI versus one human clone.
    private bool inChallengeMode;
    private bool challengeReturnToMenu;
    private bool challengeRaceRunning;
    private bool challengeResultsShown;
    private bool challengeWaitingForPolicy;
    private bool challengePlayerFinished;
    private bool challengeAIFinished;
    private float challengeCountdownEndsAt;
    private float challengeRaceStartedAt;
    private JetAgent challengePlayer;
    private JetAgent challengeAI;
    private PlayerController challengePlayerController;
    private FlightSchoolObjective challengeObjective;
    private ChallengeRaceStats playerChallengeStats;
    private ChallengeRaceStats aiChallengeStats;
    private ChallengeModeUI challengeUI;
    private ChallengeFollowCamera challengeCamera;

    public JetAgent ChallengePlayer => challengePlayer;
    public JetAgent ChallengeAI => challengeAI;
    public PlayerController ChallengePlayerController => challengePlayerController;
    public ChallengeRaceStats PlayerChallengeStats => playerChallengeStats;
    public ChallengeRaceStats AIChallengeStats => aiChallengeStats;
    public bool IsChallengeRaceRunning => inChallengeMode && challengeRaceRunning;
    public bool IsChallengeWaitingForAI => inChallengeMode && challengeWaitingForPolicy;
    public float ChallengeRaceElapsed =>
        challengeRaceRunning ? Mathf.Max(0f, Time.time - challengeRaceStartedAt) : 0f;
    public float ChallengeCountdownRemaining =>
        inChallengeMode && !challengeRaceRunning
            ? Mathf.Max(0f, challengeCountdownEndsAt - Time.unscaledTime)
            : 0f;

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

        // "Start from default" is distinct from merely starting without loading a
        // checkpoint. The latter normally keeps per-track tuning, while the explicit
        // dialog choice must discard it; otherwise a weak experimental PPO shape or
        // entropy value silently becomes the configuration of every later "fresh" run.
        var mode = objective.Mode;
        bool resetSettings = GameSession.ResetSettingsOnStart;
        GameSession.ResetSettingsOnStart = false;

        if (resetSettings)
        {
            settings = GameSession.SelectedAIType is AIType resetAI
                ? DataManager.GetDefaults(mode, resetAI)
                : DataManager.ResetToDefaults(Track, mode);

            // The selected-AI branch above returns a clone but does not persist it.
            // Keep settings.json aligned with the run the player explicitly requested.
            DataManager.SaveSettings(Track, settings);
            Debug.Log($"[SimulationManager] Restored default settings for {Track}/{settings.AIType}.");
        }
        else
        {
            // Load settings for this track (per-scene), seeded from the mode's defaults
            // on first run so each track keeps its own tuning.
            settings = DataManager.LoadSettings(Track, mode);
        }

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

        // Network shape (hidden-layer architecture) is a variable-length int[] that
        // can't ride the flat-float tuner, so it gets its own controller. Editing it is
        // always a cold change, so committing persists the new shape and reloads the run
        // from scratch (see ReloadRunForShape).
        ParameterTuners.NetworkShape = new NetworkShapeController(() => settings, ReloadRunForShape);

        // Create the correct paradigm for the chosen AI type
        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return;

        // Hand the population & objective to the paradigm
        activeParadigm.Initialize(population, settings, objective);

        // The main menu's continue-or-fresh dialog requested resuming the latest
        // save: rebuild from it now that the fresh run exists (LoadState is a full
        // teardown + rebuild, same as the hyperparameter-commit path). One-shot —
        // consume the flag first so scene reloads don't re-trigger the load.
        if (GameSession.LoadSaveOnStart)
        {
            GameSession.LoadSaveOnStart = false;
            if (DataManager.HasTrainingState(Track, settings.AIType))
                LoadState();
            else
                Debug.LogWarning($"[SimulationManager] Continue requested but no save exists for {Track}/{settings.AIType}; starting fresh.");
        }

        if (GameSession.StartChallengeOnStart)
        {
            GameSession.StartChallengeOnStart = false;
            EnterChallengeMode(returnToMenu: true);
        }
    }

    private void FixedUpdate()
    {
        if (inChallengeMode)
        {
            TickChallenge();
            return;
        }

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

    private void OnDestroy()
    {
        // Unity's destroyed-object "fake null" is not respected by ?. — use its
        // overloaded comparison before touching a component that may have been
        // destroyed by an older rematch path.
        if (challengeCamera != null) challengeCamera.Restore();
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

    // Commit path for the network-shape editor. The controller has already written the
    // new hidden layers into the live settings; persist them and reload so the run
    // rebuilds against the new architecture. A shape change is always structural, so the
    // saved weights can't carry over — the reload intentionally starts fresh (there is
    // no "keep progress" option, unlike the hot/cold hyperparameter split).
    private void ReloadRunForShape()
    {
        if (settings != null)
            DataManager.SaveSettings(Track, settings);

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
        snapshot.InChallengeMode = inChallengeMode;
        snapshot.ChallengeUnavailableReason = GetChallengeUnavailableReason();
        snapshot.ChallengeAvailable = string.IsNullOrEmpty(snapshot.ChallengeUnavailableReason);
        snapshot.AIName = settings != null ? settings.AIType.DisplayName() : "—";
        snapshot.ObjectiveName = objective != null ? objective.Mode.DisplayName() : "—";

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

    // ── Saved-run challenge ──────────────────────────────────────────

    /// <summary>
    /// Telemetry entry point. A challenge deliberately uses the last manual save,
    /// so the confirmation explains that unsaved live progress is not part of it.
    /// </summary>
    public void RequestChallengeFromTraining()
    {
        string reason = GetChallengeUnavailableReason();
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning($"[SimulationManager] Challenge unavailable: {reason}");
            return;
        }

        challengeUI = ChallengeModeUI.Ensure(this);
        challengeUI.ShowConfirmation(() => EnterChallengeMode(returnToMenu: false));
    }

    private string GetChallengeUnavailableReason()
    {
        if (inChallengeMode) return "CHALLENGE IN PROGRESS";
        if (inInferenceMode) return "EXIT INFERENCE FIRST";
        if (objective == null || settings == null) return "CHALLENGE UNAVAILABLE";
        if (objective.Mode != DataManager.GameMode.FlightSchool) return "FLIGHT SCHOOL ONLY";
        if (!DataManager.HasTrainingState(Track, settings.AIType)) return "SAVE REQUIRED";
        return string.Empty;
    }

    /// <summary>
    /// Replaces the current run with one frozen-policy AI and one human-controlled
    /// clone. No save is written and no learning occurs.
    /// </summary>
    private bool EnterChallengeMode(bool returnToMenu)
    {
        if (inChallengeMode) return false;

        string reason = GetChallengeUnavailableReason();
        if (!string.IsNullOrEmpty(reason))
        {
            Debug.LogWarning($"[SimulationManager] Challenge unavailable: {reason}");
            return false;
        }

        TrainingSaveData data = DataManager.LoadTrainingState(Track, settings.AIType);
        if (data == null || data.Mode != DataManager.GameMode.FlightSchool)
        {
            Debug.LogWarning("[SimulationManager] The selected save is not a Flight School run.");
            return false;
        }

        challengeObjective = objective as FlightSchoolObjective;
        if (challengeObjective == null)
        {
            Debug.LogWarning("[SimulationManager] Flight School challenge requires FlightSchoolObjective.");
            return false;
        }

        // The opponent must be exactly the saved run, including architecture,
        // decision cadence, and objective parameters.
        if (data.Settings != null) settings = data.Settings;
        objective.SetParameters(data.ObjectiveParameters);

        activeParadigm?.Dispose();
        activeParadigm = null;

        DestroyPopulation();

        population = InstantiatePopulation(1);
        ConfigureSensors(population, objective);
        activeParadigm = CreateParadigm(settings.AIType);
        if (activeParadigm == null) return false;
        activeParadigm.Initialize(population, settings, objective);

        if (!activeParadigm.CanRunInference || !activeParadigm.StartInference())
        {
            Debug.LogWarning("[SimulationManager] Saved AI could not start; restoring training from the save.");
            LoadState();
            return false;
        }

        challengeAI = population[0];
        JetMLAgent policyAgent = challengeAI.GetComponent<JetMLAgent>();
        if (policyAgent != null) policyAgent.SetChallengeMode(true);
        ChallengeGhostVisual.Apply(challengeAI.gameObject);

        GameObject playerObject = Instantiate(jetPrefab, GetOrCreateJetParent());
        playerObject.name = "Player Challenge Jet";
        challengePlayer = playerObject.GetComponent<JetAgent>();
        var playerList = new List<JetAgent> { challengePlayer };
        ConfigureSensors(playerList, objective);

        challengePlayer.Brain = null;
        challengePlayerController = playerObject.GetComponent<PlayerController>();
        if (challengePlayerController == null)
        {
            Debug.LogError("[SimulationManager] Jet prefab is missing PlayerController; challenge cancelled.");
            Destroy(playerObject);
            activeParadigm.Dispose();
            activeParadigm = null;
            DestroyPopulation();
            LoadState();
            return false;
        }
        challengePlayerController.ConfigureForChallenge();

        // Race only: projectiles can disturb the otherwise non-colliding comparison.
        WeaponSystem playerWeapons = playerObject.GetComponent<WeaponSystem>();
        if (playerWeapons != null) playerWeapons.enabled = false;
        WeaponSystem aiWeapons = challengeAI.GetComponent<WeaponSystem>();
        if (aiWeapons != null) aiWeapons.enabled = false;

        playerChallengeStats = new ChallengeRaceStats { TotalHoops = challengeObjective.WaypointCount };
        aiChallengeStats = new ChallengeRaceStats { TotalHoops = challengeObjective.WaypointCount };
        challengePlayerFinished = false;
        challengeAIFinished = false;
        challengeRaceRunning = false;
        challengeResultsShown = false;
        challengeReturnToMenu = returnToMenu;
        inChallengeMode = true;
        inInferenceMode = false;

        PrepareChallengeStartingGrid();
        SetChallengeParticipantActive(challengePlayer, false);
        SetChallengeParticipantActive(challengeAI, false, keepPolicyListening: true);
        IgnoreParticipantCollisions();

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        challengeWaitingForPolicy = policyAgent != null;
        challengeCountdownEndsAt = challengeWaitingForPolicy
            ? float.PositiveInfinity
            : Time.unscaledTime + 3f;

        challengeCamera = ChallengeFollowCamera.Attach(challengePlayer.transform);
        challengeUI = ChallengeModeUI.Ensure(this);
        challengeUI.ShowCountdown();
        if (UIManager.Instance != null) UIManager.Instance.SetTelemetryVisible(false);

        Debug.Log($"<color=yellow>[SimulationManager]</color> Challenge starting for {Track}/{settings.AIType}.");
        return true;
    }

    private void TickChallenge()
    {
        if (!challengeRaceRunning)
        {
            if (challengeResultsShown) return;
            if (challengeWaitingForPolicy)
            {
                JetMLAgent policyAgent = challengeAI != null
                    ? challengeAI.GetComponent<JetMLAgent>()
                    : null;
                if (policyAgent == null || policyAgent.HasReceivedAction)
                {
                    challengeWaitingForPolicy = false;
                    challengeCountdownEndsAt = Time.unscaledTime + 3f;
                }
                return;
            }
            if (Time.unscaledTime >= challengeCountdownEndsAt)
                BeginChallengeRace();
            return;
        }

        float elapsed = ChallengeRaceElapsed;

        if (!challengePlayerFinished && challengePlayer != null)
        {
            playerChallengeStats.Sample(challengePlayer, challengeObjective, elapsed);
            challengePlayer.CurrentFitness += challengeObjective.GetStepReward(challengePlayer);
            if (challengeObjective.CheckTerminalState(challengePlayer))
                FinishChallengeParticipant(challengePlayer, playerChallengeStats, isPlayer: true);
        }

        if (!challengeAIFinished && challengeAI != null)
        {
            aiChallengeStats.Sample(challengeAI, challengeObjective, elapsed);

            // Evolutionary brains only drive controls themselves; their containing
            // paradigm normally pumps objective progression. RL's JetMLAgent already
            // performs reward/progression/terminal checks in OnActionReceived.
            bool aiTerminal;
            if (challengeAI.GetComponent<JetMLAgent>() == null)
            {
                challengeAI.CurrentFitness += challengeObjective.GetStepReward(challengeAI);
                aiTerminal = challengeObjective.CheckTerminalState(challengeAI);
            }
            else
            {
                // A crashed ML-Agent stops receiving actions, so its normal
                // OnActionReceived terminal check may never run. The race manager
                // still owns the participant lifetime and must freeze its clock.
                aiTerminal = challengeObjective.CheckTerminalState(challengeAI);
            }

            if (aiTerminal || challengeObjective.HasFinished(challengeAI))
                FinishChallengeParticipant(challengeAI, aiChallengeStats, isPlayer: false);
        }

        if (challengePlayerFinished && challengeAIFinished)
            CompleteChallenge();
    }

    private void BeginChallengeRace()
    {
        // SetStartingState assigns the objective's 600 m/s launch velocity, which
        // Unity rejects on a kinematic body. Unfreeze physics first while leaving
        // all control scripts disabled until the starting grid is ready.
        SetChallengeKinematic(challengePlayer, false);
        SetChallengeKinematic(challengeAI, false);
        PrepareChallengeStartingGrid();
        IgnoreParticipantCollisions();

        JetMLAgent policyAgent = challengeAI != null
            ? challengeAI.GetComponent<JetMLAgent>()
            : null;
        if (policyAgent != null) policyAgent.SetChallengeRaceActive(true);

        SetChallengeParticipantActive(challengePlayer, true);
        SetChallengeParticipantActive(challengeAI, true);
        challengeRaceStartedAt = Time.time;
        challengeRaceRunning = true;
        challengeUI?.ShowRace();
    }

    private void PrepareChallengeStartingGrid()
    {
        if (challengePlayer == null || challengeAI == null || challengeObjective == null) return;

        challengeAI.ResetAgent();
        challengeObjective.SetStartingState(challengeAI, 0, 2);
        challengePlayer.ResetAgent();
        challengeObjective.SetStartingState(challengePlayer, 1, 2);

        // Both competitors keep the objective's exact spawn. The saved policy may
        // intentionally be overfit to this line; visibility comes from ghosting the
        // AI renderer rather than perturbing its initial state.
    }

    private static void SetChallengeKinematic(JetAgent jet, bool kinematic)
    {
        if (jet == null) return;
        Rigidbody rb = jet.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.isKinematic = kinematic;

        // Challenge cameras render between FixedUpdate ticks. Interpolating both
        // competitors prevents their 600 m/s motion from appearing as 50 Hz steps.
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void SetChallengeParticipantActive(
        JetAgent jet, bool active, bool keepPolicyListening = false)
    {
        if (jet == null) return;

        SetChallengeKinematic(jet, !active);

        PlayerController player = jet.GetComponent<PlayerController>();
        if (player != null) player.enabled = active && jet == challengePlayer;

        JetMLAgent mlAgent = jet.GetComponent<JetMLAgent>();
        if (mlAgent != null) mlAgent.enabled = active || keepPolicyListening;

        // Avoid coupling this manager to ML-Agents' DecisionRequester type.
        foreach (MonoBehaviour behaviour in jet.GetComponents<MonoBehaviour>())
            if (behaviour != null && behaviour.GetType().Name == "DecisionRequester")
                behaviour.enabled = active || keepPolicyListening;

        jet.enabled = active;
    }

    private void FinishChallengeParticipant(
        JetAgent jet, ChallengeRaceStats stats, bool isPlayer)
    {
        if (jet == null || stats == null || stats.Finished) return;
        stats.Finish(jet, challengeObjective, ChallengeRaceElapsed);
        SetChallengeParticipantActive(jet, false);

        if (isPlayer)
        {
            challengePlayerFinished = true;

            // Keep spectating the race instead of staring at the player's frozen
            // wreck/finish position while the saved AI is still flying.
            if (!challengeAIFinished && challengeAI != null)
                if (challengeCamera != null)
                    challengeCamera.Follow(challengeAI.transform);
        }
        else challengeAIFinished = true;
    }

    private void CompleteChallenge()
    {
        challengeRaceRunning = false;
        challengeResultsShown = true;
        string winner = DetermineChallengeWinner();
        challengeUI?.ShowResults(winner, playerChallengeStats, aiChallengeStats);
    }

    private string DetermineChallengeWinner()
    {
        if (playerChallengeStats.HoopsPassed != aiChallengeStats.HoopsPassed)
            return playerChallengeStats.HoopsPassed > aiChallengeStats.HoopsPassed
                ? "YOU WIN"
                : "AI WINS";

        // Average race speed is a stable interpretation of the requested speed
        // tie-breaker; it avoids deciding the race from one noisy physics frame.
        float delta = playerChallengeStats.AverageSpeed - aiChallengeStats.AverageSpeed;
        if (Mathf.Abs(delta) < 0.5f) return "DRAW";
        return delta > 0f ? "YOU WIN" : "AI WINS";
    }

    private void IgnoreParticipantCollisions()
    {
        if (challengePlayer == null || challengeAI == null) return;
        Collider[] playerColliders = challengePlayer.GetComponentsInChildren<Collider>(true);
        Collider[] aiColliders = challengeAI.GetComponentsInChildren<Collider>(true);
        foreach (Collider playerCollider in playerColliders)
            foreach (Collider aiCollider in aiColliders)
                Physics.IgnoreCollision(playerCollider, aiCollider, true);
    }

    public void RematchChallenge()
    {
        if (!inChallengeMode) return;
        bool returnToMenu = challengeReturnToMenu;
        CleanupChallengeRuntime();
        EnterChallengeMode(returnToMenu);
    }

    public void ExitChallengeMode()
    {
        if (!inChallengeMode) return;
        bool returnToMenu = challengeReturnToMenu;
        CleanupChallengeRuntime();

        if (returnToMenu)
        {
            GameSession.Clear();
            const string menu = "Main Menu";
            if (LoadingOverlay.Instance != null) LoadingOverlay.Instance.LoadScene(menu);
            else SceneManager.LoadScene(menu);
            return;
        }

        LoadState();
        if (UIManager.Instance != null) UIManager.Instance.SetTelemetryVisible(true);
    }

    private void CleanupChallengeRuntime()
    {
        challengeRaceRunning = false;
        challengeResultsShown = false;
        challengeWaitingForPolicy = false;
        inChallengeMode = false;

        activeParadigm?.Dispose();
        activeParadigm = null;
        challengeObjective?.ForgetAgent(challengeAI);
        challengeObjective?.ForgetAgent(challengePlayer);
        DestroyPopulation();

        if (challengePlayer != null) Destroy(challengePlayer.gameObject);
        challengePlayer = null;
        challengeAI = null;
        challengePlayerController = null;

        if (challengeCamera != null) challengeCamera.Restore();
        challengeCamera = null;
        challengeUI?.HideAll();

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }

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
        Transform parent = GetOrCreateJetParent();

        for (int i = 0; i < count; i++)
        {
            GameObject jetObject = Instantiate(jetPrefab, parent);
            JetAgent agent = jetObject.GetComponent<JetAgent>();
            pop.Add(agent);
        }

        return pop;
    }

    private Transform GetOrCreateJetParent()
    {
        if (jetParent != null) return jetParent;

        var container = new GameObject("Jets");
        jetParent = container.transform;
        jetParent.SetParent(transform, false);
        return jetParent;
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
