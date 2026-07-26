# AerialPlaneArchitectureReport.docx

- Title: Aerial Plane Attack Cool XXX3 - Software Architecture Report
- Subject: Current implemented architecture of the Unity AI flight simulation
- Author: Aerial Plane Attack Project
- Sections: 1
- Inline images: 0

SOFTWARE ARCHITECTURE REPORT

Aerial Plane Attack
Cool XXX3

Unity Flight Simulation and AI Training Platform

A source-audited description of runtime composition, training paradigms, persistence, inference, parameter tuning, telemetry, and Windows deployment.

Audit date  |  13 July 2026

Unity 6000.3.10f1  |  ML-Agents 4.0.3  |  SharpNEAT 2.4.4

## Contents

- Executive summary

- Scope and architectural character

- Technology and dependency baseline

- System context and runtime boundaries

- High-level decomposition

- Startup, menu selection, and scene entry

- Simulation orchestration

- Agent, observation, control, and physics pipeline

- Objectives and reward contracts

- Evolutionary training architecture

- Reinforcement-learning architecture

- Persistence, save/load, and inference

- Runtime parameter tuning

- Telemetry, visualization, and UI architecture

- Cameras, selection, track authoring, and weapons

- Standalone packaging and operational model

- Principal runtime flows

- Extension model

- Architectural qualities, limitations, and risks

- Implementation status and glossary

## 1. Executive summary

Aerial Plane Attack Cool XXX3 is a Unity 6 flight simulation in which populations of jets are controlled by learned policies. The system supports two training families behind a common orchestration boundary. Evolutionary modes run entirely inside Unity and use either a fixed-topology neural network or SharpNEAT. Reinforcement-learning modes use Unity ML-Agents inside the player while a separate Python process trains PPO or SAC policies.

The architecture is centered on SimulationManager. It resolves the scene objective, loads track-specific settings, creates the jet population, activates exactly one sensor family, selects a training paradigm from AIType, and advances that paradigm from Unity's fixed-timestep loop. The manager deliberately does not implement either evolution or reinforcement learning itself. The common ITrainingParadigm contract keeps lifecycle, snapshots, persistence, and inference consistent while allowing the two training families to operate very differently.

The flight body is shared across every control mode. JetPhysics owns the aerodynamic simulation; JetAgent is the in-process brain driver used by evolutionary training; and JetMLAgent is a runtime-added ML-Agents wrapper used by PPO and SAC. Both control paths read the same objective-selected sensor and apply the same pitch, roll, yaw, and throttle controls. This is an important architectural strength: algorithm differences are kept above the physical plant rather than duplicated inside it.

Persistence is more complete than the previous UML suggested. Settings and saves are keyed by the sanitized active scene, called the track, so multiple Flight School scenes do not overwrite each other. Evolutionary saves serialize the entire population and champion. RL saves snapshot the trainer's checkpoint directory and restore it through a resumed Python trainer. Inference is also paradigm-owned: evolutionary modes load an in-process champion, while PPO and SAC launch the trainer with --resume --inference because no Unity-side policy object exists.

Runtime tuning is implemented, not merely groundwork. Reward parameters and scalar model hyperparameters use reusable staged ParameterTuner instances. A separate NetworkShapeController handles the variable-length layer shape that cannot fit the scalar map. The UI distinguishes hot changes, which preserve learned state through a protected save/load round trip, from cold structural changes, which require a clean rebuild or scene reload.

The telemetry UI is data-driven and can install itself after a scene loads. A Resources-based TelemetryLayoutConfig specifies sections and widget prefabs; a builder creates window chrome, sections, and widgets; and UIManager reads one SimulationSnapshot per rendered frame. The snapshot is the primary read boundary between training and presentation. Brain visualization uses renderer strategies for fixed topology, NEAT, and RL rather than placing algorithm checks throughout the widget.

The principal architectural constraints are operational rather than conceptual. RL depends on a large external Python environment, trainer startup currently blocks Unity's main thread while waiting for a port, RL saves are limited to the most recent trainer checkpoint, and RL inference still requires Python. Dogfight remains an incomplete mode: enum, menu, camera, selection, and weapon infrastructure exist, but there is no combat objective or combat sensor. Several global/static access points simplify Unity integration but increase hidden coupling and make isolated testing harder.

Overall assessment: The codebase has a coherent plugin-style training core, a shared physical agent model, and unusually complete runtime persistence and tuning. Its largest remaining architectural gap is productization around the external RL trainer, followed by completion or removal of the partially represented Dogfight mode.

## 2. Scope and architectural character

This report describes the implemented C# runtime in Assets/Scripts, the project package baseline, and the repository scripts that create or install the ML-Agents Python environment. It focuses on components, responsibilities, data ownership, runtime flows, persistence boundaries, and extension points. It does not inventory scene GameObjects, serialized prefab instances, or every method.

The application combines several architectural styles:

- A central orchestrator coordinates Unity scene state but delegates algorithm-specific work through interfaces.

- Strategy and adapter patterns isolate brains, evolution engines, objectives, sensors, renderers, and ML-Agents integration.

- A snapshot-based presentation boundary keeps the telemetry UI read-oriented.

- Static bootstraps and locators provide zero-wiring services for settings, loading overlays, telemetry, and parameter tuning.

- External-process integration places the PPO/SAC optimizer, model weights, and checkpoint writer outside the Unity process.

- Track-keyed persistence treats a scene as the durable identity of a course, while objective mode remains the identity of a reward family and default set.

The architecture is intentionally asymmetric between evolutionary and RL training. Evolutionary training is generation-based: all jets finish, the population is scored, and a new population is produced. RL training is episode-based: each ML-Agents agent ends and respawns independently while the external trainer updates the policy. The common paradigm interface standardizes lifecycle and persistence without forcing the algorithms into a false shared loop.

## 3. Technology and dependency baseline

| Area | Current baseline | Architectural role |
| --- | --- | --- |
| Engine | Unity 6000.3.10f1 | Scene lifecycle, GameObjects, physics, UI, build runtime |
| Language/runtime | C# in Unity | Application, simulation, algorithms, adapters, and presentation |
| Reinforcement learning | Unity ML-Agents package 4.0.3 | Unity agent lifecycle, communicator, behavior and decision scheduling |
| Python trainer | mlagents 1.1.0 in Python 3.10.12 environment | PPO/SAC optimization, checkpoints, TensorBoard data, ONNX export |
| Fixed neuroevolution | Project-native implementation | Dense feed-forward networks, tournament selection, mutation, elitism |
| NEAT | SharpNEAT 2.4.4 compiled library | Genome representation, decoding, speciation, structural evolution |
| Input | Unity Input System 1.18.0 | Player flight controls, selection, pause, and camera actions |
| Rendering | Universal Render Pipeline 17.3.0 | Visual presentation; training observations remain vector-only |
| UI | Unity UI 2.0.0 and TextMesh Pro | Menu, runtime-built telemetry, modal feedback, visualization |
| Serialization | Newtonsoft JSON for Unity 3.2.2 | Settings, training-save metadata, engine-state wrappers |
| Authoring | ProBuilder 6.0.9 | Track/scene content support |

The RL Python environment is deliberately large because it includes a CUDA-enabled PyTorch build. It is not committed to Git. The environment is produced with Conda/conda-pack and distributed as a pinned GitHub Release asset. The Unity package version and Python trainer version therefore form a compatibility boundary that should be upgraded as a coordinated pair and verified with an end-to-end communicator test.

## 4. System context and runtime boundaries

The system has four important runtime boundaries.

### 4.1 Unity player or Editor process

Unity owns scenes, the physics timestep, jets, objectives, sensors, the in-game UI, and evolutionary training. It also owns the ML-Agents Agent components that collect observations and apply actions. SimulationManager.FixedUpdate is the top-level simulation clock for paradigm work, while each JetAgent and JetPhysics component participates in Unity's own fixed update cycle.

### 4.2 External Python trainer process

PPO and SAC weights are not represented as IBrain objects inside Unity. RLParadigm writes a YAML configuration and uses TrainerProcessLauncher to start python -m mlagents.trainers.learn. Unity and Python communicate over the ML-Agents port, normally 5004. The Python process owns optimization state, replay/buffer state as supported by ML-Agents, checkpoint files, summaries, and exported ONNX models.

The launcher also owns process cleanup. It clears stale listeners on the configured port, redirects trainer output to the Unity log on background threads, and attempts to kill the complete process tree during shutdown. This prevents orphaned environment-worker processes from retaining the port or stdout pipe.

### 4.3 Durable storage

There are three storage roots with distinct purposes:

- Application.persistentDataPath/GameData/<Track>/ contains per-track settings, save JSON, and stable RL checkpoint snapshots.

- <ProjectRoot>/results/<Track>_<AIType>/ is the live ML-Agents results directory written by the trainer.

- <ProjectRoot>/config/jet_ppo.yaml or jet_sac.yaml is regenerated at trainer startup from the active RLSettings.

Unity PlayerPrefs separately stores user display resolution and fullscreen mode. This is user preference data rather than training state.

### 4.4 Operating system and distribution services

Windows process, networking, PowerShell, curl, tar, and the filesystem are part of the operational architecture. A standalone player needs the --mlagents-port 5004 command-line argument. AppBootstrap adds it by relaunching the executable when necessary. A thin build also uses a PowerShell installer to download and extract the pinned training environment before the menu is shown.

## 5. High-level decomposition

| Subsystem | Primary responsibility | Main collaborators |
| --- | --- | --- |
| Core/session | Startup configuration, menu-to-scene choices, display settings, render suppression | App bootstrap, game session, settings menu, cameras/canvases |
| Simulation management | Population factory, sensor selection, paradigm selection, save/load coordination, inference switching | Data manager, objectives, paradigms, tuners, UI manager |
| Paradigms | Own training lifecycle and paradigm-specific persistence/inference | Evolution engines or ML-Agents trainer |
| Evolution engines | Create, score, evolve, capture, and restore populations | Evolvable brains, SharpNEAT |
| Agents and physics | Convert observations/policies into shared flight controls and physical motion | Sensors, objectives, weapons |
| Objectives | Spawn setup, reward shaping, terminal logic, reward breakdown, sensor requirement | Jets, waypoint sensor, parameter tuner |
| Persistence | Defaults, per-track settings, save metadata, temporary save protection | Paradigms and simulation manager |
| Runtime tuning | Stage edits, describe controls, route hot/cold changes, edit network shape | Simulation settings, objectives, editor widgets |
| Telemetry/UI | Build data-driven windows, display snapshots, forward user commands | Simulation manager, parameter locators, cameras |
| Content/input | Camera paths, track recording, selection, manual control, weapons | Objectives, UI manager, Unity Input System |

The central dependency direction is from the manager toward interfaces, not concrete algorithms. Concrete classes are chosen in one factory method based on AIType. This gives the application one deliberate composition root for training. Unity scene composition remains the other composition root: the jet prefab supplies physics, sensors, and optional weapons; the scene supplies the objective and content.

## 6. Startup, menu selection, and scene entry

Several services install themselves through RuntimeInitializeOnLoadMethod. AppBootstrap caps rendering at 60 frames per second, disables vSync so the cap is effective, performs standalone environment setup, and ensures the player has the ML-Agents port argument. GameSettings reapplies a saved resolution and display mode before scene load. LoadingOverlay, SettingsMenu, and GhostMode create persistent GameObjects so every scene can use them without explicit wiring.

The main menu separates course selection from AI selection. GameModeData is a ScriptableObject carrying display text, hero artwork, and a scene name. GameModeSelectionController builds the mode list, updates the hero panel, and checks whether the selected track and AI type have a save. If so, it asks whether to continue or start fresh. AITypeSelectionWriter converts the selector's button index into AIType and writes it into GameSession.

GameSession is intentionally transient. It survives the scene load but resets on domain reload or process exit. It carries two pieces of information: the selected AI type and a one-shot request to load the save immediately after the gameplay scene initializes.

At scene entry, SimulationManager resolves the IObjective from its serialized provider. It loads settings using the sanitized scene name as the track key and the objective's mode as the default family. If the menu selected a different AI type, the manager swaps to a fresh default block for that (mode, AIType) combination. It then spawns the population, configures sensors, creates tuning services, creates the paradigm, and initializes it. A requested continue operation is consumed only after this fresh runtime structure exists, because load is implemented as a controlled teardown and rebuild.

## 7. Simulation orchestration

SimulationManager is the Unity-side application service for a training scene. Its responsibilities are deliberately broad but cohesive around runtime composition:

- Resolve the scene objective and active track identity.

- Load and persist SimulationSettings.

- Instantiate and destroy the population under a dedicated parent.

- Enable the one sensor type required by the objective and disable the others.

- Derive the observation width from the actual active sensor and stamp it into the selected model settings.

- Construct either EvolutionaryParadigm or RLParadigm.

- Advance training or inference on each fixed tick.

- Produce the manager-completed SimulationSnapshot for presentation.

- Coordinate full save/load, inference entry/exit, and parameter commits.

The manager is also the consistency boundary for population reconstruction. Load, cold tuning, and inference all change the number or meaning of active jets. Those operations tear down the paradigm first, destroy old GameObjects, create a correctly sized population, reselect sensors, create a new paradigm, and initialize it before restoring state. This sequencing matters for ML-Agents because enabled agents can bind to the Academy and trainer as soon as their runtime components are created.

The simulation snapshot is a read model, not a command model. Paradigms fill their own iteration, score, population, timing, and sub-snapshot fields. The manager stamps cross-cutting fields such as time scale, selected jet, objective and AI display names, attrition visibility, and inference status. UIManager therefore does not need to inspect the active paradigm.

## 8. Agent, observation, control, and physics pipeline

### 8.1 Shared jet state

JetAgent is the common Unity component attached to the jet prefab. It holds fitness, survival time, crash state, starting position, accumulated control effort, the selected ISensor, and an optional IBrain. In evolutionary modes it performs the inference loop directly: read observations, evaluate the brain, cache actions, and apply them to JetPhysics.

Evolutionary action evaluation supports decision cadence. DecisionPeriod determines how many fixed ticks an action is held, and AgentIndex phase-staggers decisions across a large population. This reduces synchronized neural-network spikes without reducing the physics update rate. A period of one preserves the original evaluate-every-tick behavior.

JetMLAgent is added and enabled at runtime only for PPO/SAC. It derives from ML-Agents Agent, discovers the enabled sensor, collects vector observations, copies continuous actions for telemetry, applies them to the same physics component, accumulates objective reward, and ends an episode on the objective's terminal condition. Episode restart calls the same objective spawn method used by evolution.

### 8.2 Observation model

The sensor contract exposes a stable sensor type, an observation buffer, and its count. BasicFlightSensors produces 12 normalized values: three local linear-velocity components, three local angular-velocity components, the world-space forward vector, and the world-space up vector.

WaypointSensors extends the basic vector to 19 values. It adds the target hoop's local direction, normalized distance, and local forward orientation. FlightSchoolObjective owns target progression and rewrites the sensor's current target when a hoop is passed.

The active sensor is selected by objective requirement, not by AI type. After selection, the manager overwrites the current model's input width from the sensor count. This allows all four AI types to run against either implemented objective without relying on baked input-size defaults.

The jet prefab must contain exactly one component for each supported sensor type. Duplicate components of the required type are ambiguous because the manager keeps the first match while objective code may independently call GetComponent. The manager logs a warning, but prefab correctness remains necessary.

### 8.3 Action model

The first four continuous outputs are fixed across paradigms:

- Pitch in [-1, 1].

- Roll in [-1, 1].

- Yaw in [-1, 1].

- Throttle remapped from [-1, 1] to [0, 1].

If a policy produces at least six outputs and the prefab has a WeaponSystem, output five fires and output six switches weapon on an edge-triggered basis. Current default training settings produce four outputs, so weapon actions are infrastructure for a future combat policy rather than part of the implemented objectives.

### 8.4 Aerodynamic plant

JetPhysics wraps a Unity Rigidbody. It applies thrust on every fixed tick, then computes aerodynamic effects when speed is nontrivial. Air density decays exponentially with altitude. Lift and drag come from configurable animation curves and dynamic pressure; control torque is capped to approximate fly-by-wire/hydraulic limits; and stability combines angular damping with pitch, yaw, and roll restoring torque.

The physics component is policy-agnostic. Manual input, fixed-topology brains, NEAT phenotypes, PPO, and SAC all call the same control interface. That separation is crucial for fair algorithm comparison because algorithms do not receive different flight dynamics.

Two serialized stall-related fields currently describe critical angle and buffet intensity, but the inspected physics pipeline does not apply a separate stall-buffet force. The lift/drag curves can still encode stall behavior. The distinction should be documented in tuning work so serialized field names are not mistaken for an active subsystem.

## 9. Objectives and reward contracts

IObjective combines environment setup and evaluation. It declares the game mode, required sensor type, whether independent deaths make population attrition meaningful, spawn/reset behavior, step reward, final fitness, terminal detection, reward breakdown, and tunable-parameter access.

| Objective | Sensor | Main reward | Terminal conditions | Attrition |
| --- | --- | --- | --- | --- |
| Max Altitude | Basic flight, 12 values | Height gain minus control-effort penalty | Crash or common time limit | Hidden; population ends on a shared timer except crashes |
| Flight School | Waypoint, 19 values | Hoop progress, orientation shaping, hoop bonuses, penalties, completion time bonus | Crash, global time limit, time since last hoop, or course completion | Shown; agents finish independently |

### 9.1 Max Altitude

Jets spawn at a fixed height with forward velocity and identity rotation. Each step rewards vertical displacement and penalizes newly accumulated squared control effort. Final fitness is total height gained minus total effort penalty. Only the penalty coefficient is exposed as a runtime reward dial; the time limit remains persisted but is intentionally not editable through the descriptor list.

### 9.2 Flight School

The scene provides an ordered array of hoop transforms. In the Editor, the objective mirrors direct child order into the array; at runtime it can rebuild the list when missing. A jet begins behind the first hoop, aligned with it, and the waypoint sensor points to that hoop.

Reward shaping combines distance progress, velocity/heading alignment, a strictly nonpositive look-at term, control-effort penalty, backwards-drift penalty, and a large hoop-passage bonus. Passage uses plane crossing in the hoop's local coordinates, which prevents high-speed tunneling through a thin trigger. Crossing outside the ring marks the jet as crashed. Completion adds a remaining-time bonus during final fitness calculation.

The objective publishes hoop-passed and agent-finished events. The Flight School camera uses them to distribute automatic camera coverage according to how far active jets have progressed.

An important coupling is that GetStepReward also advances hoop state and retargets the sensor. Evolutionary inference must call it even when learning is disabled, otherwise observations become stale. This behavior is correct in current code but means the method is not a pure reward function; future objective APIs could separate progression from reward calculation to make the contract clearer.

## 10. Evolutionary training architecture

EvolutionaryParadigm owns the generation loop. Initialization requests the first population of brains from an IEvolutionEngine, assigns brains to jets, configures decision cadence, resets the agents, applies objective spawn state, and marks the full population alive.

During a generation, active jets accumulate step rewards. When an objective reports terminal state, the paradigm computes final fitness, disables the jet, and decrements the alive count. When all jets are inactive, the paradigm gathers the fitness list, records stable maximum and average statistics for the completed generation, asks the engine for the next population, assigns the returned brains, and respawns the batch.

The paradigm publishes BestJetReady after a generation spawns. The max-altitude camera subscribes to follow the previous generation's winning elite. Both evolution engines report the index at which that elite survived in the newly ordered population.

### 10.1 Fixed-topology neuroevolution

NeuroEvoBrain is a dense feed-forward network. Hidden layers use ReLU and the output layer uses tanh, yielding flight controls in [-1, 1]. Weights and biases are stored in jagged arrays. Forward-pass buffers are reused to avoid per-decision allocation, while the brain visualizer uses independent buffers so it cannot disturb a cached action.

ClassicNeuroEvoEngine creates one brain per population slot from NetworkShape. After scoring, it sorts brains by fitness, preserves at least the top one percent, maintains an all-time champion, and uses tournament selection plus additive mutation for remaining slots. If the historical champion is older than the current winner, both are retained when population size permits. Parent brains are copied into an immutable pool before offspring slots are overwritten, preventing later tournaments from selecting already-mutated children under old scores.

Full state is JSON containing shape, every flattened brain, champion weights, champion score, and generation. The separate champion export is champion.brain.json.

### 10.2 NEAT

NeatEngine adapts the synchronous Unity generation loop to SharpNEAT 2.4. A custom SteppableNeatEvolutionAlgorithm exposes one protected generation step and advances SharpNEAT's internal generation counter. A PreScoredGenomeListEvaluator is intentionally a no-op because Unity has already simulated and scored the genomes.

Fitness is stamped onto genome objects before SharpNEAT rebuilds its list. Negative raw scores are shifted upward with a positive baseline because SharpNEAT expects nonnegative fitness. Raw champion reporting remains unshifted.

The engine builds its genome factory, decoder, evaluator, speciation strategy, and evolution algorithm from NeatSettings. Speciation uses K-means with Manhattan distance. Complexity regulation is currently unrestricted. Because SharpNEAT can probabilistically round small-species elite counts to zero, the adapter preserves the best two percent and reinjects any elite that disappears.

NeatBrain wraps a decoded IBlackBox. SharpNEAT outputs use a sigmoid range of (0, 1) and are remapped to (-1, 1). The last outputs are cached so visualization does not activate a recurrent phenotype a second time. The general IEvolvableBrain interface is imperfect for NEAT: copying throws and direct mutation is a no-op because those operations belong to the engine.

Full NEAT state stores complete-genome XML for the population and champion inside a JSON wrapper. The standalone champion export is champion.genome.xml.

## 11. Reinforcement-learning architecture

RLParadigm owns Unity-side PPO/SAC integration. Initialization creates tracking arrays and a snapshot but deliberately defers trainer startup. This lets a load operation initialize the runtime structure and immediately start the trainer in resume mode without first booting and killing a fresh trainer.

At startup the paradigm regenerates the algorithm-specific YAML. It clears live results for a fresh run, launches the trainer, then configures each jet's BehaviorParameters before adding or enabling JetMLAgent. Configuration order prevents an Agent from registering with a zero-action default specification. The behavior uses vector observations and continuous actions under the name JetBrain.

A DecisionRequester is recreated for every trainer binding. It uses RLSettings.DecisionPeriod and holds actions between decisions. Recreating the requester is necessary after an Academy recycle because the component subscribes during Awake and otherwise remains attached to a disposed Academy.

Each completed life replaces that agent's last-episode score. Live maximum and average therefore compare the same quantity across agents rather than accumulating unbounded reward over the run. An all-time best episode is kept separately for save metadata and the headline champion score.

PPO and SAC share network settings, learning rate, batch/buffer sizes, discount, horizon, maximum steps, checkpoint interval, decision period, engine time scale, requested window size, and target frame rate. PPO adds entropy beta, clipping epsilon, GAE lambda, and epoch count. SAC adds target smoothing tau, steps per update, initial entropy coefficient, and initial replay-buffer steps.

The trainer controls Unity engine settings on connect. The YAML explicitly supplies 1280 by 720, training time scale 1, and target frame rate 60 by default. Because the ML-Agents side channel forces windowed mode, RLParadigm captures the user's fullscreen state before connecting and reasserts it for a short period after startup.

Trainer replacement within one Play session follows an intentional shutdown order: disable agents while the old Academy is live, terminate the Python process tree, then dispose the Academy singleton. A subsequent trainer can then bind to a fresh Academy.

## 12. Persistence, save/load, and inference

### 12.1 Identity and settings

The active scene name is sanitized into DataManager.CurrentTrack. Letters, digits, and hyphens are retained; other characters become underscores. Settings live at GameData/<Track>/settings.json. Training save metadata lives at GameData/<Track>/save_<AIType>.json. Consequently, two scenes with the same GameMode maintain independent settings and training slots.

Mode and track have different roles:

- Mode selects baked settings defaults, reward defaults, the primary AI type, and the sensor family through the objective.

- Track selects durable user settings, training metadata, RL checkpoint snapshots, and the ML-Agents run ID.

SimulationSettings contains universal population/spawn fields and one relevant nested settings object. The manager derives input size from the active sensor. TrainingSaveData combines identity, cloned settings, objective parameters, progress statistics, timestamp, and an opaque engine-state string.

### 12.2 Evolutionary save and load

The evolutionary paradigm asks its engine to capture the complete population and champion. On load, the manager first applies saved settings and objective parameters, rebuilds the population, initializes a fresh engine, and then asks the engine to overwrite it from the opaque state. Generation numbering and champion score continue from the save.

### 12.3 RL save and load

ML-Agents owns the model and optimizer state, so Unity cannot serialize it into the JSON payload. RLParadigm copies results/<Track>_<AIType>/ into GameData/<Track>/rl_checkpoint_<AIType>/. The JSON EngineState is a marker containing the run ID.

On load, the saved checkpoint directory is copied back to the live results path and the trainer launches with --resume. The manager recycles all jets and the Academy so the connection is established against the restored trainer.

RL save granularity is limited by checkpoint_interval, default 25,000 trainer steps. Saving does not force ML-Agents to emit a checkpoint. Progress since the last checkpoint is not captured, and a save made before the first .pt file exists cannot restore a policy.

### 12.4 Manual-save protection during tuning

Hot parameter commits reuse save/load as a state-preserving restart mechanism. To avoid overwriting the user's manual save, DataManager copies the JSON and RL checkpoint directory to backup names, performs the temporary save/load, then restores the original slot. If no manual save existed, the temporary slot is deleted afterward.

### 12.5 Inference

Inference reduces the population to one jet and disables learning and saving. It requires a saved run.

- Fixed neuroevolution and NEAT restore the saved engine state, extract the champion IBrain, assign it to the single jet, and loop the objective on terminal state.

- PPO and SAC stage the saved checkpoint and launch mlagents-learn --resume --inference. The policy remains external; normal ML-Agents episode reset loops the jet.

Leaving inference calls the same full load path so training resumes from the save. RL inference therefore has the same Python startup dependency and hitch as RL training and is not deployment-style embedded inference.

| Capability | Fixed NeuroEvo | NEAT | PPO/SAC |
| --- | --- | --- | --- |
| Live policy in Unity | Yes | Yes, decoded SharpNEAT phenotype | No |
| Full save representation | JSON weights/biases | XML genomes inside JSON | Trainer checkpoint directory plus JSON marker |
| Champion export | Brain JSON | Genome XML | No direct API; trainer produces ONNX |
| Resume mechanism | Restore population | Restore genomes and scaffolding | Stage checkpoint and launch --resume |
| Inference mechanism | One champion jet | One champion jet | External trainer with --inference |

## 13. Runtime parameter tuning

The tuning system separates parameter description, staging, commit behavior, and UI.

ITunableParameters represents a scalar parameter group as descriptors plus a flat string-to-float map. Descriptors supply display name, range, default, cold/hot classification, and toggle behavior. Objectives implement this interface directly. ModelHyperparameters adapts the current SimulationSettings; it reads through a provider function so it remains valid when load replaces the settings object.

ParameterTuner wraps one scalar source. A staged value does not immediately mutate the source. The tuner exposes live and effective values, removes staged values that match live state, publishes a state-changed event, and passes a private copy of staged edits to its commit delegate. ParameterTuners is the runtime locator for reward, hyperparameter, and network-shape controls.

Hot changes preserve the learned state. The manager applies them, performs the protected temporary save/load round trip, and resumes the rebuilt runtime. Examples include reward weights, fixed-network mutation rate and decision period, NEAT evolution probabilities, and most PPO/SAC optimizer/training settings.

Cold changes alter structure or population cardinality. Population size, RL hidden units, RL layer count, and RL input normalization are cold in the scalar editor. The current editor asks whether to save progress before persisting settings and reloading the scene; structurally incompatible weights are not applied to the new run. A direct cold tuner commit also has a manager path that rebuilds from scratch.

Network shape is a separate cold controller because it is a list of integers rather than one float:

- Fixed NeuroEvo supports arbitrary hidden-layer widths, from a direct input-to-output network up to 32 hidden layers.

- PPO and SAC expose one to five uniform-width hidden layers, matching the ML-Agents model representation.

- NEAT disables the editor because topology is evolved rather than configured.

| Group | Exposed controls | Commit class |
| --- | --- | --- |
| Max Altitude reward | Control-effort penalty | Hot |
| Flight School reward | Effort, distance, hoop bonus, backwards drift, look-at weight | Hot |
| Universal model | Population size | Cold |
| Fixed NeuroEvo | Mutation rate, decision period | Hot |
| NEAT | Decision period, species, elitism, selection, four mutation probabilities | Hot |
| PPO | Optimizer/training values hot; hidden units, layers, normalization cold | Mixed |
| SAC | Optimizer/training values hot; hidden units, layers, normalization cold | Mixed |
| Network shape | Fixed NeuroEvo arbitrary list; PPO/SAC uniform list | Cold |

Some values round-trip without being exposed as dials. Objective time limits and hoop radius are persisted but hidden. RL training time scale is read and written by the adapter and saved in settings, but it is intentionally absent from the descriptor list because it is treated as an operational speed control rather than a model hyperparameter.

EvoControlsWidget is a legacy direct-edit path that broadcasts mutation and lambda events to EvolutionaryParadigm. It bypasses the staged tuner model, and its lambda event updates EvoSettings.Lambda rather than the objective's authoritative reward field. New UI work should prefer HyperparameterEditorWidget and the reward tuner; the legacy widget should be retired or rewired to avoid two competing parameter paths.

## 14. Telemetry, visualization, and UI architecture

The telemetry system supports both scene-authored and runtime-built layouts. TelemetryUIBootstrap runs after scene load. When it finds a SimulationManager but no UIManager, it loads a per-scene Resources layout or a shared default, creates a screen-space canvas and Input System event system when required, injects the layout into a new inactive manager, then activates it.

TelemetryLayoutConfig is a ScriptableObject containing ordered section definitions and widget prefab references. TelemetryWindowBuilder creates or instantiates window chrome, a scroll view, foldable sections, separators, and widgets. UIManager initializes every widget and obtains one snapshot per rendered frame. Sections then tick their children with that snapshot.

Widgets issue commands only through UIManager or the parameter locators. Save/load, inference, time scale, cold parameter reload, and agent selection therefore have explicit presentation-to-application routes. The main widget set includes generation/episode statistics, run history, brain visualization, parameter editor, network shape, save/load, inference, time scale, camera auto-follow, and scene reset.

The brain visualizer uses a strategy family:

- Fixed topology reads actual shape, weights, and a side-effect-free activation pass.

- NEAT reads genome nodes/connections and cached output values; hidden activations that cannot be observed are neutral.

- RL renders the configured representative topology and samples current observations and last actions. Trainer-side weights and hidden activations are inaccessible.

SimulationSnapshot is deliberately reused by paradigms to reduce garbage collection. UI graph history is stored separately by the widget and decimated when it exceeds its sample cap.

UITheme is a static runtime skinning service. Scene and prefab visuals may remain plain in the Editor; runtime code recolors panels, text, and selectable states. Content images with real sprites are left intact. Components created after a broad skin call must be skinned at their creation site. Some serialized visuals are explicitly pinned during widget initialization because Inspector values override code defaults.

LoadingOverlay and SettingsMenu build their own UI and persist across scenes. The overlay supports asynchronous scene-load presentation, blocking modal work, short toasts, and save/load feedback. PauseMenuController preserves and restores the previous time scale and routes menu transitions through the overlay.

## 15. Cameras, selection, track authoring, and weapons

The camera layer is mode-specific rather than abstracted behind an interface.

CameraControllerMaxAltitude subscribes to the evolutionary best-jet event and can select the highest active jet while maintaining a side view. CameraControllerFlight traverses a linked list of camera Waypoint nodes. It also subscribes to Flight School hoop and finish events, tracks how many active jets have reached configured trigger hoops, and advances coverage automatically. The telemetry toggle controls this auto-follow mode.

CameraControllerDogfight is a selection-focus camera. It listens to static selection events and transitions between its initial view and the selected transform. SelectionInputManager raycasts against a configured layer and emits generic transform events. JetSelectionController bridges these older transform events and screen-space nearest-jet selection into UIManager.SelectAgent, also applying selected-jet highlighting. CubeVisuals listens to the same events for fade feedback.

TrackRecorder is an authoring aid carried by the jet prefab but inactive by default. During a Play Mode recording session it drops hoop prefabs at a spacing threshold, records their transforms, and can rebake them into an edit-time hierarchy after Play Mode. Parenting recorded hoops under a Flight School objective lets that objective's editor synchronization adopt their hierarchy order.

The weapon subsystem supports machine-gun bullets and missiles with hardpoints, cooldowns, inherited aircraft velocity, collision damage placeholders, explosion effects, and trail cleanup. It is available to manual control and to policies with six actions, but no implemented objective scores combat outcomes. This is supporting infrastructure, not a complete Dogfight training architecture.

## 16. Standalone packaging and operational model

Evolutionary modes need only the Unity player. RL modes need Python, ML-Agents, PyTorch, and their dependencies. The project supports two Windows distributions.

### 16.1 Thin build

The build includes StreamingAssets/setup-training-env.ps1 but not the multi-gigabyte environment. At process startup, AppBootstrap calls the trainer launcher's environment-install check. If StreamingAssets/mlagents-env/python.exe is absent, the PowerShell script downloads the pinned env-v1/mlagents-env.tar.zst release asset with curl, resumes partial downloads, extracts it with Windows tar, and verifies the interpreter marker. Setup occurs before the menu and is mandatory for the current standalone build; failure exits the player.

### 16.2 Self-contained build

Maintainers or offline users can populate Assets/StreamingAssets/mlagents-env before building. package.ps1 creates a relocated environment from the mlagents Conda environment, runs conda-unpack, and smoke-tests ML-Agents and PyTorch. download-env.ps1 instead downloads the already published release bundle for editor use. Unity copies the extracted directory into the built player's StreamingAssets.

### 16.3 Interpreter resolution

The trainer searches in this order: bundled StreamingAssets interpreter, MLAGENTS_PYTHON, and a Conda environment named mlagents via known roots. A Windows standalone gets one additional chance to run the bundled installer. Environment variables set after Unity Hub started are not inherited by an already-running Hub or its Editor child, so both must be restarted.

### 16.4 Port and writable-directory requirements

The standalone Unity player needs --mlagents-port 5004. The bootstrap relaunches the executable with this argument if missing; the batch launcher remains a fallback. The build should live in a writable directory because the trainer writes config and results relative to the project/player root. User saves themselves remain under Unity's persistent-data path.

The release bundle uses Zstandard compression and depends on a Windows tar build that can decode it. Older Windows installations may require an updated archive tool or a differently compressed release.

## 17. Principal runtime flows

### 17.1 Fresh evolutionary run

- Menu selection writes the AI type and loads a track scene.

- The manager resolves the objective and track settings.

- It spawns jets and activates the objective's sensor.

- It derives input width and constructs the selected evolution engine and paradigm.

- The engine creates brains; the paradigm assigns and spawns them.

- Each fixed tick accumulates reward and retires terminal jets.

- When all jets finish, the engine evolves the population and the batch respawns.

- The manager and paradigm expose a stable snapshot to telemetry.

### 17.2 Fresh RL run

- The manager builds jets, sensors, and an unstarted RL paradigm.

- On the first training tick, the paradigm writes YAML and starts Python.

- After the trainer port opens, it configures behaviors and runtime ML-Agents components.

- Each agent collects vector observations and receives continuous actions at its decision cadence.

- The objective supplies reward and terminal state; EndEpisode respawns only that agent.

- The Python trainer updates the shared policy and periodically writes checkpoints.

### 17.3 Load

- The manager reads the current (track, AIType) save.

- It disposes the paradigm and destroys the population.

- It applies saved settings and objective parameters.

- It recreates the population, sensors, and paradigm.

- Evolution restores in-process population state; RL stages checkpoints and starts a resumed trainer.

### 17.4 Inference entry and exit

- Entry verifies paradigm support and a saved run.

- Training is disposed and the population is replaced with one jet.

- Evolution assigns a restored champion; RL starts an inference trainer.

- The one jet loops terminal episodes without learning.

- Exit disables inference and performs a full load from the same save.

### 17.5 Hot and cold tuning

- UI rows stage scalar changes in their group tuner.

- Hot-only edits commit through a modal operation.

- The manager applies values and runs protected save/load, preserving learned state and the user's manual slot.

- If any scalar edit is cold, the editor shows a destructive-change dialog and reloads after persisting settings.

- Network-shape edits use their bespoke widget and always follow the cold reload path.

## 18. Extension model

### 18.1 Add an AI type

Add the enum value, settings representation, defaults, display name, and manager factory branch. If it belongs to an existing paradigm, implement the relevant engine/framework adapter. If its lifecycle differs, implement ITrainingParadigm, including complete save/load and inference semantics. Update brain visualization if the policy can be inspected.

### 18.2 Add an objective

Implement IObjective, select or add a SensorType, place the objective in the scene, and provide its reward defaults. The jet prefab must contain the corresponding sensor component. Keep progression side effects explicit; ideally separate environment progression from reward computation in new designs. Add a scene-specific telemetry layout only if the shared layout is insufficient.

### 18.3 Add a sensor

Implement ISensor, return a stable count, reuse observation buffers where practical, add the component once to the jet prefab, and extend SensorType. The manager will derive model input width after activation. Any objective-specific target mutation should update the exact active sensor instance.

### 18.4 Add a scalar tuning control

For reward values, update the objective's get/set maps and descriptor list. For model values, add the field to the appropriate settings class and update the matching descriptor and adapter read/write paths. Mark a value cold whenever existing weights or population state cannot be restored safely after it changes.

### 18.5 Add a telemetry widget

Derive from UIWidget, consume SimulationSnapshot or call manager commands, create a prefab, and add it to the layout asset. Skin runtime-created children at creation time. Avoid reaching directly into a concrete paradigm; extend the snapshot or manager boundary when new read data or commands are genuinely cross-cutting.

### 18.6 Complete Dogfight

A complete combat training mode needs more than enabling the existing weapons. It requires an IObjective implementation, a combat observation model, spawn/team semantics, damage/kill/death ownership, terminal rules, reward attribution, action width of at least six, default settings for all AI types intended to participate, and persistence/inference validation. The existing camera and selection infrastructure can then become presentation support for that objective.

## 19. Architectural qualities, limitations, and risks

### 19.1 Strengths

- Training families share orchestration, agents, sensors, objectives, physics, persistence metadata, inference controls, and telemetry while keeping different learning loops separate.

- Interfaces provide clear extension seams for objectives, sensors, brains, engines, and paradigms.

- Track-keyed storage prevents same-objective scenes from overwriting one another.

- Full evolutionary population persistence and RL checkpoint staging support meaningful resume, not only champion export.

- Sensor-derived input width prevents baked default drift.

- Cached snapshots, observation arrays, brain buffers, and action holding show attention to runtime allocation and large populations.

- Runtime-built telemetry and theme application reduce fragile scene/prefab YAML edits.

- Hot/cold tuning semantics make structural incompatibility explicit.

### 19.2 External trainer startup blocks the main thread

TrainerProcessLauncher waits for the port with repeated sleeps for up to 60 seconds. During this interval the Editor or player can appear frozen. Moving process startup and readiness polling into an asynchronous state machine or coroutine is the highest-value responsiveness improvement. The paradigm should expose a starting/ready/failed state so telemetry can represent startup without enabling agents too early.

### 19.3 RL save semantics are checkpoint-bounded

A Save button suggests an immediate snapshot, but ML-Agents only provides the latest periodic checkpoint. The UI should clearly report the captured trainer step and whether it came from the current session. A future trainer-side command or lower checkpoint interval could narrow the gap, but very frequent checkpoints create I/O and ONNX-export hitches.

### 19.4 RL inference is operationally expensive

Inference launches Python and binds an Academy. It is appropriate for replaying a saved training run but not for a distributable inference product. An ONNX/Sentis path would remove the environment, port, startup delay, and external process for inference-only builds. It would also permit a true Unity-side RL policy adapter, though it should not be forced into IBrain unless observation normalization and recurrent state are represented correctly.

### 19.5 Global/static coupling

DataManager, GameSession, ParameterTuners, UITheme, bootstrapped singletons, and static events are convenient in Unity, but dependencies are partly implicit. Static event subscribers must be removed reliably, domain-reload behavior can surprise tests, and parallel simulations in one process would conflict. Dependency injection at the scene composition root or explicit service references would improve testability if the project grows.

### 19.6 Objective method purity

Flight School progression occurs inside step reward. This creates a hidden rule for inference and any future evaluator. Separating AdvanceEnvironment, CalculateStepReward, and terminal evaluation would make side effects visible and enable deterministic testing.

### 19.7 Interface mismatch for NEAT brains

IEvolvableBrain assumes direct copy and mutation operations, while NEAT performs both at genome/engine level. NeatBrain.Copy throws and Mutate does nothing. Splitting phenotype inference from fixed-genome mutation capabilities, or making engine ownership explicit, would better satisfy substitution principles.

### 19.8 Partially represented Dogfight mode

The enum and UI can imply a playable training objective that does not exist. Until combat training is complete, menu data should hide or clearly label it. Defaults can synthesize settings for missing pairs, but settings alone do not provide an objective or sensor.

### 19.9 Configuration and serialized-default drift

Unity Inspector values override C# field initializers. Runtime code already centralizes many reward defaults in DataManager, which is good, but UI assets and scene references still require synchronization. Architecture changes that add serialized fields should include asset migration or explicit runtime initialization.

SpawnRadius and SpawnFormation remain in universal settings, but the two implemented objective spawn paths use their own fixed setup rather than reading those settings. They should either be integrated into objective spawning or identified as reserved combat/future fields.

### 19.10 Writable root and platform assumptions

RL configuration and results are written beside the project or built player. Installing a build under a protected directory can fail even though Unity's persistent-data save path is writable. The system is also Windows-specific in its installer, taskkill, netstat, PowerShell, curl, tar, and executable discovery. Cross-platform support would require an explicit platform service boundary.

### 19.11 Recommended priorities

- Make trainer launch asynchronous and expose startup state.

- Decide whether RL inference remains trainer-based or gains an embedded ONNX/Sentis path.

- Clarify Save UI semantics around checkpoint age and captured step.

- Remove or rewire the legacy evolutionary controls to the staged tuner.

- Hide Dogfight until its objective/sensor contract is implemented, or complete the mode as a vertical slice.

- Separate objective progression side effects from reward calculation.

- Move live RL config/results to a guaranteed writable application-data root or document and validate the current requirement at startup.

- Add automated tests around settings cloning, track-keyed paths, engine capture/restore, sensor widths, and tuning hot/cold classification.

## 20. Implementation status and glossary

### 20.1 Implemented versus incomplete

| Capability | Status | Notes |
| --- | --- | --- |
| Max Altitude training | Implemented | All four AI types can be selected; basic sensor |
| Flight School training | Implemented | All four AI types can be selected; waypoint sensor and camera events |
| Fixed neuroevolution | Implemented | Full population save/load and champion inference |
| NEAT | Implemented | SharpNEAT adapter, elite guard, full-genome save/load |
| PPO/SAC training | Implemented with external dependency | Requires compatible Python environment and trainer connection |
| Runtime reward/hyperparameter UI | Implemented | Staging, defaults, hot/cold behavior |
| Network-shape UI | Implemented | Fixed NeuroEvo and PPO/SAC; disabled for NEAT |
| Telemetry bootstrap and widgets | Implemented | Data-driven Resources layout or scene-authored mode |
| Training inference | Implemented | Evolution in process; RL through trainer inference |
| Embedded RL inference | Not implemented | ONNX export exists through trainer, but no Sentis runtime path |
| Dogfight training objective | Not implemented | Supporting camera, selection, and weapons only |

### 20.2 Glossary

AI type: Concrete selection among FixedNeuroEvo, NEAT, PPO, and SAC. Mode: Objective family such as Max Altitude or Flight School; used for defaults and sensor selection. Track: Sanitized active scene name; durable key for settings, saves, checkpoints, and RL run IDs. Paradigm: Top-level training lifecycle strategy, evolutionary or RL. Engine: Evolutionary population algorithm below EvolutionaryParadigm. Brain: Unity-side callable policy implementing IBrain; PPO/SAC do not have one during current training/inference. Sensor: Component that builds a fixed-width vector observation for the active objective. Objective: Spawn, reward, terminal, breakdown, and tuning contract for a game mode. Hot change: Parameter change adopted while preserving learned state through save/load. Cold change: Structural change that requires a clean model/population rebuild. Checkpoint: Trainer-written .pt state used to resume or replay PPO/SAC. Snapshot: Read-only-ish presentation model shared from training to telemetry each frame. Inference mode: One-jet replay of a saved policy without learning or saving.

This report reflects the implementation audited on 13 July 2026. The companion Mermaid file is the detailed class-and-relationship model; this document is the narrative architecture reference.

## Section 1 furniture
- Header: AERIAL PLANE ATTACK  |  ARCHITECTURE REPORT
- Footer: Software Architecture Report	Page
