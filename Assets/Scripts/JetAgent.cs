using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

[RequireComponent(typeof(JetPhysics))]
public class JetAgent : MonoBehaviour
{
    public IBrain Brain { get => currentBrain; set => currentBrain = value; }
    public ISensor Sensor 
    { 
        // Required for BrainVisualizerWidget.cs TryGetSensorData method
        // to avoid "fake null" errors when sensor is destroyed or disabled
        get 
        {
            var obj = currentSensor as MonoBehaviour;
            if (obj == null || !obj.enabled) 
            {
                return null;
            }
            return currentSensor;
        }
        set => currentSensor = value; 
    }
    public bool HasCrashed { get => hasCrashed; set => hasCrashed = value; }
    public float TimeAlive { get => timeAlive; set => timeAlive = value; }
    public Vector3 StartingPosition { get => startingPosition; set => startingPosition = value; }
    public float CurrentFitness { get => currentFitness; set => currentFitness = value; }
    public float TotalControlEffort { get => totalControlEffort; set => totalControlEffort = value; }

    private bool hasCrashed;
    private IBrain currentBrain;
    private ISensor currentSensor;
    private JetPhysics physics;
    private WeaponSystem weapons;
    private float timeAlive = 0f;
    private Vector3 startingPosition;
    private float currentFitness;
    private float totalControlEffort = 0f;

    // Used to prevent the AI from rapidly toggling weapons every physics frame
    // Technically, this avoids weapon cooldown
    private bool wasSwitching = false;

    public void Awake()
    {
        physics = GetComponent<JetPhysics>();
        TryGetComponent(out weapons);

        currentSensor = GetComponent<ISensor>();

        startingPosition = transform.position;
    }

    public void FixedUpdate()
    {
        if (hasCrashed) return;

        timeAlive += Time.fixedDeltaTime;

        if (currentBrain != null && currentSensor != null && physics != null)
        {
            // Gather observation data from the environment
            float[] observations = currentSensor.GetObservationData();

            // Ask the brain to process the observations and return control outputs
            float[] actions = currentBrain.GetControlOutputs(observations);

            // Apply the outputs to the physics component
            // Checking array length to prevent IndexOutOfRangeException 
            // expecting: [0] = pitch, [1] = roll, [2] = yaw, [3] = throttle
            if (actions != null && actions.Length >= 4)
            {
                float pitch = actions[0];
                float roll = actions[1];
                float yaw = actions[2];
                float throttle = (actions[3] + 1) / 2; // TODO do it the right way

                totalControlEffort += (pitch * pitch) + (roll * roll) + (yaw * yaw);

                physics.ApplyControlInputs(pitch, roll, yaw, throttle);

                if (weapons != null)
                {
                    // Action [4] -> Fire Weapon (e.g., > 0.5f means "pull trigger")
                    if (actions.Length >= 5 && actions[4] > 0.5f)
                    {
                        weapons.Fire();
                    }

                    // Action [5] -> Switch Weapon (e.g., > 0.5f means "press switch button")
                    if (actions.Length >= 6)
                    {
                        bool wantToSwitch = actions[5] > 0.5f;

                        // Only trigger the switch on the "keydown" equivalent, not while held
                        if (wantToSwitch && !wasSwitching)
                        {
                            weapons.SwitchWeapon();
                        }

                        wasSwitching = wantToSwitch;
                    }
                }
            }
            else
            {
                Debug.LogError("Brain did not return enough control outputs for flight.");
            }
        }
    }

    public void ResetAgent()
    {
        hasCrashed = false;
        timeAlive = 0f;
        currentFitness = 0f;
        totalControlEffort = 0f;
    }

    public void Copy(JetAgent agent)
    {
        hasCrashed = agent.hasCrashed;
        currentBrain = agent.currentBrain.Copy();
        currentSensor = agent.currentSensor = currentSensor;
        physics = agent.physics;
        weapons = agent.weapons;
        timeAlive = agent.timeAlive;
        startingPosition = agent.startingPosition;
        currentFitness = agent.currentFitness;
        totalControlEffort = agent.totalControlEffort ;
    }
}