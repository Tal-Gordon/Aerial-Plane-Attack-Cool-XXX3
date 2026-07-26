# Aerial_Plane_Attack_Architecture_Report.docx

- Title: Aerial Plane Attack Cool XXX3 - Software Architecture Report
- Subject: Project architecture: game loop, layers, AI paradigms, objectives, persistence, and deployment
- Author: Aerial Plane Attack Cool XXX3 project team
- Sections: 1
- Inline images: 3

SOFTWARE ARCHITECTURE REPORT

Aerial Plane Attack
Cool XXX3

Learning-Driven Flight Simulation Architecture

Game loop, system layers, AI paradigms, objectives, persistence, and deployment

| Document focus: A concise project-submission architecture document. It concentrates on the shared simulation loop and the boundaries that allow Fixed NeuroEvo, NEAT, PPO, and SAC to train on the same aircraft and objectives. |
| --- |

Prepared: 16 July 2026

Students: [INSERT STUDENT NAMES AND IDs]

Project code: [INSERT PROJECT CODE]

Supervisor: [INSERT SUPERVISOR NAME]

## Document information

| Item | Value |
| --- | --- |
| Project | Aerial Plane Attack Cool XXX3 |
| Document type | Software architecture report |
| Scope baseline | Implemented runtime as of 16 July 2026 |
| Primary audience | Project supervisors, reviewers, developers, and AI researchers |
| Source basis | Current Assets/Scripts code, supplied full report, supplied Mermaid UML, and read-only Jira project APACX |

## Contents

Document information	2

Contents	2

1. Abstract	3

2. Introduction	3

3. Problem statement	3

4. Objectives	4

5. User stories and verification	5

6. Architectural overview	7

7. Core training game loop	9

8. AI paradigms	11

9. Objectives and environment	12

10. Persistence, inference, and runtime tuning	13

11. Typical user flows	14

12. Application screenshots	15

13. Simplified component model	17

14. Implementation baseline	17

15. Implementation status and architectural risks	18

16. Sources and traceability	19

## 1. Abstract

Aerial Plane Attack Cool XXX3 is a Unity 6 flight-simulation research application in which jets learn to fly through evolutionary algorithms or reinforcement learning. Four AI types - Fixed NeuroEvo, NEAT, PPO, and SAC - share the same aircraft physics, observation sensors, action space, and objective contracts. The architecture separates training orchestration from learning algorithms so each approach can be compared on consistent flight tasks.

The implemented objectives are Max Altitude and Flight School. Max Altitude rewards vertical gain while discouraging excessive control effort. Flight School trains independent jets to pass ordered hoops using dense navigation rewards and course-completion bonuses. A Dogfight selection/camera/weapons foundation exists, but combat training is not an implemented objective because combat observations, teams, rewards, and terminal rules are still absent.

The design centers on SimulationManager, ITrainingParadigm, IObjective, IEvolutionEngine, ISensor, JetAgent, JetMLAgent, and JetPhysics. Evolutionary training runs fully inside Unity. PPO and SAC use Unity ML-Agents with an external Python trainer, creating an explicit process and deployment boundary. Saves, inference, telemetry, and runtime parameter tuning are built around these same abstractions.

## 2. Introduction

Training an aircraft controller is not only a machine-learning problem. The system must keep physics, observations, actions, rewards, episode boundaries, population management, persistence, and user feedback synchronized. If each learning algorithm owned its own aircraft or task implementation, comparisons would be unreliable and new modes would become expensive to add.

This project solves that integration problem by treating the jet and its environment as shared infrastructure. An objective chooses the sensor and defines success; a training paradigm owns the learning lifecycle; and the simulation manager composes those parts for the track and AI selected by the user.

## 3. Problem statement

The project needs to support fundamentally different learning lifecycles without duplicating the simulation. Fixed-topology neuroevolution and NEAT evaluate whole generations inside Unity, while PPO and SAC run independent ML-Agents episodes and optimize policies in a separate Python process. The architecture must make these approaches interchangeable at the application level while preserving their correct internal behavior.

| Architectural challenge: Provide one fair flight environment and one user workflow while allowing batch evolution, topology evolution, and external reinforcement learning to retain distinct training, persistence, and inference mechanisms. |
| --- |

## 4. Objectives

- Run Fixed NeuroEvo, NEAT, PPO, and SAC against the same aircraft and objective definitions.

- Keep the observe-decide-act-physics-reward loop consistent across learning paradigms.

- Allow objectives to select the observation sensor, spawn state, rewards, fitness calculation, and terminal rules.

- Persist experiment settings and complete resumable state per track and AI type.

- Expose live telemetry, agent selection, brain visualization, inference replay, and safe runtime tuning.

- Support Unity Editor runs and packaged Windows builds, including discovery or installation of the external RL environment.

- Make extension points explicit for future objectives, sensors, AI engines, and telemetry components.

## 5. User stories and verification

### 5.1 Requirements stories

The following stories summarize the user-visible architectural requirements. They are derived from the APACX Jira project and aligned with the implemented code baseline.

#### US-01. Choose the AI training algorithm [APACX-91]

As a simulation user, I want to choose Fixed NeuroEvo, NEAT, PPO, or SAC so that I can compare learning paradigms on the same objective.

#### US-02. Choose and revisit a track [APACX-92]

As a simulation user, I want distinct course scenes to keep independent settings and saves so that one Flight School track never overwrites another.

#### US-03. Maximize altitude efficiently [APACX-47]

As an AI researcher, I want a simple altitude-gain task so that algorithms can be compared on a continuous-control baseline.

#### US-04. Complete Flight School courses [APACX-63]

As an AI researcher, I want jets to navigate ordered hoops so that control quality, progress, speed, and generalization can be measured.

#### US-05. Train with topology evolution [APACX-67]

As an AI researcher, I want NEAT to evolve structure and weights so that it can be compared with fixed-topology neuroevolution.

#### US-06. Train with PPO or SAC [APACX-54]

As an AI researcher, I want ML-Agents PPO and SAC policies to use the same sensors, actions, physics, rewards, and terminal rules as evolution.

#### US-07. Monitor live progress [APACX-93]

As an AI researcher, I want generations, episodes, scores, population state, and elapsed time in one dashboard so that I can diagnose learning behavior.

#### US-08. Save, resume, and replay [APACX-95]

As an AI researcher, I want to preserve full training progress and replay a saved policy without learning so that long experiments remain useful.

#### US-09. Inspect the active agent [APACX-96]

As an AI researcher, I want to select a jet and visualize its network and live decisions so that learned behavior can be interpreted.

#### US-10. Tune safely at runtime [APACX-111]

As an AI researcher, I want staged reward, hyperparameter, and network-shape changes so that experiments do not corrupt learned state or manual saves.

#### US-11. Run RL from a Windows build [APACX-97]

As a project user, I want the compatible Python environment to be discoverable or installed automatically so that PPO and SAC can run outside the development machine.

### 5.2 Architecture verification scenarios

These four tests are intentionally architecture-level checks. They validate the boundaries that matter across multiple features rather than testing presentation details.

| Story | Given | When | Expected result |
| --- | --- | --- | --- |
| US-01 | A track scene with one objective and a valid jet prefab | Each AI type is selected before scene entry | SimulationManager constructs the matching paradigm; all modes use the objective-selected sensor and the shared JetPhysics action path. |
| US-04 | A Flight School course with ordered hoops | A jet crosses a hoop plane inside the configured radius | The objective awards passage, advances only that jet's target, retargets WaypointSensors, and continues until completion or another terminal condition. |
| US-08 | A saved run for one track and AI type | The user resumes or enters inference | The correct slot is loaded; evolution restores population/champion state, while RL stages the checkpoint and launches the trainer in resume or inference mode. |
| US-10 | Staged runtime parameter edits | The user commits hot and cold changes | Hot changes preserve trained state through protected save/load; cold structural changes rebuild a compatible fresh run and leave the manual save untouched. |

## 6. Architectural overview

The system has two composition roots. Unity scenes provide concrete content - objective, track objects, jet prefab, and cameras. SimulationManager provides runtime composition - settings, population, active sensor, paradigm, tuning services, persistence, and the snapshot read model used by the UI.

Figure 1. Unity owns the simulation; Python is required only for PPO/SAC training and RL inference.

| Key boundary: Evolutionary policies are live IBrain objects inside Unity. PPO/SAC policy and optimizer state live in the external trainer, so Unity controls the environment but not the learned model object. |
| --- |

### 6.1 System layers

Figure 2. Dependency direction moves from interaction and orchestration toward stable training, agent, and infrastructure contracts.

- Presentation: Main menu, pause/settings, telemetry windows, graphs, selected-agent statistics, brain rendering, cameras, and runtime controls.

- Application orchestration: SimulationManager composes the run; GameSession carries the menu choice; SimulationSnapshot isolates the UI from concrete paradigms.

- Training paradigms: ITrainingParadigm standardizes initialize, tick, snapshots, persistence, inference, and disposal while preserving different learning lifecycles.

- Learning engines: IEvolutionEngine hosts fixed NeuroEvo or SharpNEAT. RLParadigm integrates ML-Agents and TrainerProcessLauncher for PPO/SAC.

- Agent and environment: JetAgent/JetMLAgent, ISensor, IObjective, JetPhysics, tracks, and optional weapons form the reusable simulation plant.

- Infrastructure: DataManager, JSON, engine state, trainer checkpoints, YAML generation, process launch, and Windows environment packaging.

## 7. Core training game loop

Unity's fixed timestep is the common clock. SimulationManager calls the active paradigm once per FixedUpdate. The paradigm reads objective state, while each jet or ML-Agents component participates in observation, action, and physics updates. This creates one shared control cycle with different generation or episode boundaries.

Figure 3. Shared fixed-step control loop and the two distinct training boundaries.

### 7.1 Step-by-step runtime sequence

- Resolve the scene objective and derive the track identity from the active scene.

- Load track settings, apply the selected AI type, spawn the population, and enable exactly the sensor required by the objective.

- Create EvolutionaryParadigm or RLParadigm; the former creates an evolution engine, while the latter prepares ML-Agents and the trainer process.

- Collect normalized observations from BasicFlightSensors or WaypointSensors.

- Evaluate an in-process brain or request actions from the ML-Agents policy.

- Apply pitch, roll, yaw, and throttle to the shared JetPhysics component.

- Ask the objective for step reward and terminal state; objective progression may also retarget the active waypoint sensor.

- Close a life. Evolution waits for the entire population before evolving; RL ends and resets each episode independently.

- Publish a stable SimulationSnapshot for telemetry and accept user commands such as save, load, inference, or tuning commit.

### 7.2 Shared observation and action pipeline

| Stage | Contract | Implemented behavior |
| --- | --- | --- |
| Observe | ISensor | Basic flight produces 12 values. Waypoint sensing extends this to 19 values with target direction, distance, and orientation. |
| Decide | IBrain or ML-Agents policy | Fixed NeuroEvo and NEAT evaluate inside Unity. PPO/SAC actions arrive through JetMLAgent from the trainer communicator. |
| Act | JetAgent / JetMLAgent | The first four continuous outputs are pitch, roll, yaw, and throttle. Optional weapon outputs are infrastructure only. |
| Simulate | JetPhysics | One Rigidbody-based aerodynamic plant applies thrust, lift, drag, control torque, damping, and stability for every controller. |
| Evaluate | IObjective | Spawn state, dense reward, final fitness, terminal detection, reward breakdown, sensor type, and tuning descriptors. |

## 8. AI paradigms

The training interface deliberately standardizes application lifecycle rather than forcing all algorithms into one learning loop. This preserves correct behavior for generation-based evolution and episode-based reinforcement learning.

| AI type | Policy and training | Boundary | Persistence / inference |
| --- | --- | --- | --- |
| Fixed NeuroEvo | Dense feed-forward brain; tournament selection, elitism, copying, and additive mutation. | Whole population finishes before the next generation. | JSON population plus champion; one champion jet runs in-process. |
| NEAT | SharpNEAT genomes evolve weights and topology; decoded outputs are remapped from (0,1) to (-1,1). | Whole population finishes; scores are stamped onto genomes before one evolution step. | Complete-genome XML inside save JSON; champion phenotype runs in-process. |
| PPO | Unity ML-Agents Agent bridge with an external Python PPO trainer and generated YAML. | Agents end and respawn independently; trainer updates the shared policy. | Checkpoint directory plus JSON marker; trainer launches with --resume --inference. |
| SAC | Same Unity bridge with off-policy SAC settings and replay-buffer behavior owned by Python. | Independent episodes; policy updates occur in the external trainer. | Checkpoint directory plus JSON marker; trainer launches with --resume --inference. |

### 8.1 Evolutionary training

EvolutionaryParadigm assigns one brain per jet, accumulates rewards until terminal, records final fitness, disables completed jets, and waits until all agents finish. It then gathers scores, records generation statistics, asks the selected IEvolutionEngine for the next population, reassigns brains, and respawns the batch.

ClassicNeuroEvoEngine preserves elites and an all-time champion while producing mutated offspring from an immutable parent pool. NeatEngine adapts the same synchronous Unity batch to SharpNEAT's genome, decoder, speciation, and evolution structures. Negative raw fitness is shifted before it reaches SharpNEAT because the library requires nonnegative scores.

### 8.2 Reinforcement learning

RLParadigm configures BehaviorParameters and DecisionRequester before enabling JetMLAgent, generates algorithm-specific YAML, launches the Python trainer, and coordinates episode statistics and cleanup. The configuration order prevents agents from registering with an invalid zero-action behavior specification.

The Python trainer owns policy weights, optimizer state, checkpoints, TensorBoard data, and ONNX export. Unity owns the environment and rewards. Trainer replacement disables agents, terminates the old process tree, and recycles the Academy before a new connection is established.

## 9. Objectives and environment

IObjective is the main environment contract. It identifies the game mode and required sensor, creates starting state, computes step reward and final fitness, detects terminal conditions, reports reward breakdown, and exposes selected reward parameters for staged tuning.

| Objective | Sensor | Reward model | Terminal conditions | Status |
| --- | --- | --- | --- | --- |
| Max Altitude | Basic flight (12) | Height gain minus squared control-effort penalty. | Crash or shared time limit. | Implemented for all four AI types |
| Flight School | Waypoint (19) | Distance progress, alignment shaping, nonpositive look-at term, hoop bonus, effort and backward-drift penalties, plus time bonus. | Crash, total time, time since last hoop, course completion, or missed hoop plane. | Implemented across multiple track scenes |
| Dogfight | Combat sensor not present | Teams, damage ownership, weapon rewards, and combat shaping are not defined. | Combat terminal contract not present. | Planned; camera, selection, and weapon support only |

### 9.1 Max Altitude

Jets begin with a consistent position and forward velocity. Each step measures vertical displacement since the previous step and subtracts newly accumulated control effort. Final fitness uses total altitude gained minus total effort penalty. The design provides a compact baseline for comparing convergence and control smoothness.

### 9.2 Flight School

Each scene supplies ordered hoop transforms. Every jet tracks its own target index, previous distance, plane-crossing position, control effort, and time of last hoop. WaypointSensors points to that jet's current hoop. Passing inside the hoop advances and retargets only that jet; crossing the hoop plane outside the ring marks the jet as crashed.

GetStepReward also performs progression side effects. Evolutionary inference must therefore call it even when the numeric reward is discarded. A future objective API could separate environment progression from reward calculation, but the current inference loop correctly preserves the behavior.

## 10. Persistence, inference, and runtime tuning

### 10.1 Track identity and save model

The sanitized active scene name is the track. Mode selects baked defaults and the sensor family; track selects user settings and durable saves. Training metadata is stored per (track, AI type), preventing multiple Flight School scenes from overwriting each other.

| Data | Location / representation | Owner |
| --- | --- | --- |
| Settings | Application.persistentDataPath/GameData/<Track>/settings.json | DataManager |
| Training metadata | GameData/<Track>/save_<AIType>.json | DataManager + active paradigm |
| Fixed NeuroEvo state | Flattened population weights, champion, score, and generation in JSON | ClassicNeuroEvoEngine |
| NEAT state | Complete population and champion genome XML wrapped in JSON | NeatEngine |
| RL live results | results/<Track>_<AIType>/ | Python ML-Agents trainer |
| RL stable snapshot | GameData/<Track>/rl_checkpoint_<AIType>/ | RLParadigm |

### 10.2 Save, load, and inference

A full load is a controlled teardown and rebuild. The manager disposes the active paradigm, rebuilds the correctly sized population and sensors, initializes a compatible fresh paradigm, and then restores the saved state. Evolutionary modes overwrite fresh brains with saved populations. RL stages a saved checkpoint back to the live results directory and launches the trainer with --resume.

Inference reduces the scene to one looping jet and disables learning and saving. Evolutionary modes load an in-process champion. PPO/SAC launch the external trainer with --resume --inference because the policy does not exist as an IBrain in Unity. RL save accuracy is checkpoint-bounded; saving does not force a new .pt checkpoint.

### 10.3 Hot and cold tuning

Reward parameters and scalar hyperparameters use staged ParameterTuner instances. Network shape has a separate controller because it is a variable-length list. Hot changes preserve learned state through a temporary save/load round trip that backs up and restores the user's manual save. Cold changes such as population size or network architecture rebuild from scratch because existing weights are structurally incompatible.

## 11. Typical user flows

### 11.1 Flow catalogue

#### Flow 1. Start a fresh experiment

Open main menu -> select track -> select AI type -> choose fresh start -> scene loads -> population and sensor are composed -> training begins -> telemetry displays the snapshot.

#### Flow 2. Resume an experiment

Select the same track and AI type -> choose Continue -> fresh runtime structure is created -> saved settings/objective values are applied -> paradigm restores population or trainer checkpoint -> training resumes.

#### Flow 3. Inspect learning

Select a jet -> UI stores the selected JetAgent -> snapshot includes the selection -> statistics and the appropriate brain renderer update topology, activations, observations, and actions.

#### Flow 4. Tune parameters

Edit values in the telemetry editor -> changes remain staged -> commit -> hot changes preserve training through protected save/load; cold changes warn and rebuild a compatible fresh run.

#### Flow 5. Replay inference

Save a run -> enter inference -> training is torn down -> one jet loads the champion or external RL policy -> the objective loops on terminal -> exit inference -> the saved training run is loaded again.

#### Flow 6. Run PPO/SAC in a build

Launch player -> bootstrap ensures --mlagents-port -> locate bundled or configured Python -> generate YAML -> start trainer -> ML-Agents connects -> engine settings are applied -> episode training begins.

## 12. Application screenshots

The final submission should replace the following markers with real captures from the current build. The requested views are chosen to demonstrate architecture and workflow rather than individual button implementation.

Figure 4. Main menu - track and AI selection

| [ INSERT APPLICATION SCREENSHOT HERE ] Capture guidance: Show the selected course/scene, the four AI choices, and the fresh/continue decision if a save exists. Architectural purpose: Demonstrates the application composition inputs that become GameSession state and determine the runtime paradigm and save slot. |
| --- |

Figure 5. Max Altitude live training

| [ INSERT APPLICATION SCREENSHOT HERE ] Capture guidance: Show a population of jets, altitude-focused scene context, generation or episode statistics, AI type, score, and elapsed time. Architectural purpose: Demonstrates one objective running through the common training and telemetry boundaries. |
| --- |

Figure 6. Flight School track and hoop progression

| [ INSERT APPLICATION SCREENSHOT HERE ] Capture guidance: Capture several jets at different progress points with visible hoops and, if possible, the auto-follow camera or selected agent. Architectural purpose: Demonstrates per-agent objective state, waypoint sensing, independent terminal behavior, and track content. |
| --- |

Figure 7. Telemetry and brain visualization

| [ INSERT APPLICATION SCREENSHOT HERE ] Capture guidance: Select a jet and show the runtime window with network topology/activations and selected-agent statistics. Architectural purpose: Demonstrates the snapshot-based presentation layer and the renderer strategy for different AI families. |
| --- |

Figure 8. Runtime tuning, save/load, and inference

| [ INSERT APPLICATION SCREENSHOT HERE ] Capture guidance: Show staged reward or model parameters plus the save/load and inference controls; capture any hot/cold warning if available. Architectural purpose: Demonstrates experiment management and the persistence/tuning consistency boundary. |
| --- |

## 13. Simplified component model

The supplied Mermaid UML inventories almost every implemented class. The simplified view below highlights only the dependencies that govern the core simulation and training loop.

| Component | Owns | Collaborates with |
| --- | --- | --- |
| SimulationManager | Runtime composition, population, sensor selection, paradigm lifecycle, save/load, inference, tuning coordination | DataManager, IObjective, ITrainingParadigm, ParameterTuners, UIManager |
| ITrainingParadigm | Training loop, paradigm snapshot, paradigm-specific state and inference | EvolutionaryParadigm or RLParadigm |
| IEvolutionEngine | Brain population creation, evolution, champion, capture/restore | ClassicNeuroEvoEngine, NeatEngine, IEvolvableBrain |
| JetAgent | Shared jet learning state and in-process brain actions | IBrain, ISensor, JetPhysics, optional WeaponSystem |
| JetMLAgent | ML-Agents observations, actions, reward, and episode reset | JetAgent, ISensor, IObjective, RLParadigm |
| IObjective | Spawn, reward, fitness, terminal conditions, sensor requirement, tuning values | MaxAltitudeObjective, FlightSchoolObjective |
| ISensor | Normalized observation vector | BasicFlightSensors, WaypointSensors |
| DataManager | Defaults, track settings, save JSON, RL checkpoint snapshots | SimulationSettings, TrainingSaveData, paradigms |
| UIManager / SimulationSnapshot | Read-only telemetry boundary and command forwarding | Widgets, brain renderers, cameras, SimulationManager |

| Full UML: Use UML_mermaid.mermaid for class-level detail. This report intentionally omits most UI widget and helper-class relationships to keep the architecture readable at submission scale. |
| --- |

## 14. Implementation baseline

| Area | Technology | Architectural role |
| --- | --- | --- |
| Engine | Unity 6000.3.10f1 | Scene lifecycle, GameObjects, physics, UI, and Windows build runtime |
| Language | C# | Simulation, algorithms, adapters, persistence, and presentation |
| Reinforcement learning | Unity ML-Agents 4.0.3 | Agent lifecycle, observations/actions, behavior specification, and communicator |
| Python trainer | mlagents 1.1.0 / Python 3.10.12 | PPO/SAC optimization, checkpoints, TensorBoard, and ONNX export |
| NEAT | SharpNEAT 2.4.4 | Genome representation, decoding, speciation, and structural evolution |
| Serialization | Newtonsoft JSON for Unity | Settings, training metadata, and evolutionary state wrappers |
| UI | Unity UI + TextMesh Pro | Menu, runtime-built telemetry, modal feedback, and network visualization |
| Packaging | Conda/conda-pack, PowerShell, GitHub Release asset | Portable ML-Agents environment for thin or self-contained Windows builds |

## 15. Implementation status and architectural risks

### 15.1 Current status

| Status | Capability |
| --- | --- |
| Implemented | Max Altitude and Flight School with all four AI selections |
| Implemented | Full evolutionary population save/load and champion inference |
| Implemented with external dependency | PPO/SAC training, checkpoint resume, and trainer-based inference |
| Implemented | Track-keyed saves, telemetry, agent selection, brain visualization, hot/cold tuning, and network-shape editing |
| Not implemented | Dogfight objective and combat sensor; embedded ONNX/Sentis inference |

### 15.2 Principal risks and limitations

- External RL dependency: PPO/SAC training and inference require a compatible multi-gigabyte Python environment and a successful ML-Agents connection.

- Checkpoint-bounded RL saves: progress after the most recent trainer checkpoint is not captured, and no policy can be restored before the first .pt file exists.

- Startup responsiveness: trainer launch and port-wait behavior can block the Unity main thread and appear as a temporarily unresponsive Windows player.

- Partial Dogfight representation: menu, camera, selection, and weapon code can suggest a complete mode although the objective and sensor contracts are absent.

- Hidden global coupling: static bootstraps and service locators simplify Unity scene wiring but make isolated testing and lifecycle reasoning harder.

- Objective side effects: FlightSchoolObjective.GetStepReward also advances environment state, so callers must invoke it during inference even when reward is discarded.

- Configuration drift: Inspector-serialized values can override edited C# defaults; resets or migrations are required when defaults change.

| Submission completion: Replace the cover metadata markers and the five screenshot boxes before exporting the final PDF requested by the course template. All architecture statements are written against the implemented July 2026 baseline. |
| --- |

## Section 1 furniture
- Header: AERIAL PLANE ATTACK COOL XXX3  |  SOFTWARE ARCHITECTURE
- Footer: Architecture report  |  16 July 2026	Page 1
