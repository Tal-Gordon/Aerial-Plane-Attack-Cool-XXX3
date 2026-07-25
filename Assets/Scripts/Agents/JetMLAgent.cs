using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using UnityEngine;

[RequireComponent(typeof(JetAgent))]
public class JetMLAgent : Agent
{
    private JetAgent jetAgent;
    private JetPhysics physics;
    private WeaponSystem weapons;
    private ISensor flightSensor;
    private IObjective objective;
    private RLParadigm paradigm;
    private int agentIndex;
    private int totalPopulation;

    private bool wasSwitching = false;
    private bool challengeMode;
    private bool challengeRaceActive;

    // The continuous actions from the most recent decision, copied out for the brain
    // visualizer. The PPO/SAC policy itself lives in the external Python trainer and
    // is unreachable in-process, so the RL visualizer can only show the network's
    // inputs (observations) and these outputs. Null until the first action arrives.
    private float[] lastActions;
    public float[] LastActions => lastActions;
    public bool HasReceivedAction { get; private set; }

    /// <summary>
    /// In a saved-run challenge the policy gets one life. Terminal states stop the
    /// agent in place instead of calling EndEpisode (which would auto-respawn it).
    /// </summary>
    public void SetChallengeMode(bool enabled)
    {
        challengeMode = enabled;
        challengeRaceActive = !enabled;
    }

    /// <summary>Keeps the policy registered during countdown while suppressing actions.</summary>
    public void SetChallengeRaceActive(bool active) => challengeRaceActive = active;

    public void Inject(IObjective objective, RLParadigm paradigm, int agentIndex, int totalPopulation)
    {
        this.objective = objective;
        this.paradigm = paradigm;
        this.agentIndex = agentIndex;
        this.totalPopulation = totalPopulation;
    }

    public override void Initialize()
    {
        jetAgent = GetComponent<JetAgent>();
        physics = GetComponent<JetPhysics>();
        TryGetComponent(out weapons);
        foreach (var s in GetComponents<ISensor>())
        {
            if (s is MonoBehaviour mb && mb.enabled)
            {
                flightSensor = s;
                break;
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        if (paradigm != null && CompletedEpisodes > 0)
            paradigm.RecordEpisodeEnd(agentIndex, jetAgent.CurrentFitness);

        jetAgent.ResetAgent();
        wasSwitching = false;
        objective.SetStartingState(jetAgent, agentIndex, totalPopulation);
    }

    public override void CollectObservations(Unity.MLAgents.Sensors.VectorSensor sensor)
    {
        float[] obs = flightSensor.GetObservationData();
        for (int i = 0; i < obs.Length; i++)
        {
            sensor.AddObservation(obs[i]);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (jetAgent.HasCrashed) return;
        HasReceivedAction = true;
        if (challengeMode && !challengeRaceActive) return;

        var cont = actions.ContinuousActions;

        // Snapshot the actions for the brain visualizer (the policy is a black box
        // running in the trainer; these outputs are all we can show).
        if (lastActions == null || lastActions.Length != cont.Length)
            lastActions = new float[cont.Length];
        for (int i = 0; i < cont.Length; i++)
            lastActions[i] = cont[i];

        float pitch = cont[0];
        float roll = cont[1];
        float yaw = cont[2];
        float throttle = (cont[3] + 1f) / 2f;

        jetAgent.TotalControlEffort += (pitch * pitch) + (roll * roll) + (yaw * yaw);

        physics.ApplyControlInputs(pitch, roll, yaw, throttle);

        if (weapons != null && cont.Length >= 6)
        {
            if (cont[4] > 0.5f)
                weapons.Fire();

            bool wantToSwitch = cont[5] > 0.5f;
            if (wantToSwitch && !wasSwitching)
                weapons.SwitchWeapon();
            wasSwitching = wantToSwitch;
        }

        float reward = objective.GetStepReward(jetAgent);
        jetAgent.CurrentFitness += reward;
        AddReward(reward);

        if (objective.CheckTerminalState(jetAgent))
        {
            if (jetAgent.HasCrashed)
            {
                AddReward(-5000f);
                jetAgent.CurrentFitness -= 5000f;
            }

            if (challengeMode)
            {
                enabled = false;
                return;
            }

            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // TODO: Wire up InputSystem actions for manual testing
    }
}
