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

        var cont = actions.ContinuousActions;

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
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // TODO: Wire up InputSystem actions for manual testing
    }
}
