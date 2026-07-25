using UnityEngine;

/// <summary>
/// Smooth spectator camera for Max Altitude. It follows the highest currently
/// active jet. Its X position and rotation stay fixed for a stable side view;
/// Y and Z follow the selected jet.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraControllerMaxAltitude : MonoBehaviour
{
    [Header("Side View")]
    [Tooltip("Horizontal distance from the jet spawn. The camera is placed on the jet's right side, looking toward its left side.")]
    [Min(0f)] [SerializeField] private float sideDistance = 3000f;

    [Header("Y/Z Follow")]
    [Tooltip("Vertical distance between the followed jet and the camera.")]
    [SerializeField] private float verticalOffset;
    [Tooltip("Z-axis distance between the followed jet and the camera.")]
    [SerializeField] private float depthOffset = 1000f;
    [Min(0f)] [SerializeField] private float followSharpness = 1.5f;

    [Header("Target Switching")]
    [Tooltip("Smoothing used while moving from one highest jet to another. Lower values make gentler swaps.")]
    [Min(0f)] [SerializeField] private float swapSharpness = 1.5f;
    [Tooltip("A new jet must be this much higher before the camera swaps. Prevents rapid switching between near-ties.")]
    [Min(0f)] [SerializeField] private float minimumHeightLead = 200f;
    [Tooltip("Distance from the new jet at which normal Follow Sharpness resumes.")]
    [Min(0f)] [SerializeField] private float swapSettleDistance = 0f;

    private Transform target;
    private SimulationManager simulationManager;
    private float sideAnchorX;
    private float fixedX;
    private Quaternion fixedRotation;
    private bool sideViewAnchored;
    private bool isSwappingTarget;
    private MaxAltitudeHeightTape heightTape;

    private void Awake()
    {
        // Fallback values until the first jet supplies the spawn point used to
        // establish the exact side-on framing.
        fixedX = transform.position.x;
        sideAnchorX = fixedX;
        fixedRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        simulationManager = FindFirstObjectByType<SimulationManager>();
    }

    private void OnEnable()
    {
        EvolutionaryParadigm.BestJetReady += FollowJet;
        if (heightTape != null) heightTape.SetVisible(true);
    }

    private void OnDisable()
    {
        EvolutionaryParadigm.BestJetReady -= FollowJet;
        if (heightTape != null) heightTape.SetVisible(false);
    }

    private void Start()
    {
        heightTape = MaxAltitudeHeightTape.Ensure(simulationManager);
        if (heightTape != null) heightTape.SetVisible(true);
    }

    public void FollowJet(JetAgent jet)
    {
        SetTarget(jet != null ? jet.transform : null);

        if (!sideViewAnchored && target != null)
        {
            // Unity's +X direction is the jet's right side at its Max Altitude
            // spawn orientation. Looking toward -X puts the right wing nearest the
            // camera, directly hiding the left wing behind it.
            sideAnchorX = target.position.x;
            fixedX = sideAnchorX + sideDistance;
            fixedRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            transform.SetPositionAndRotation(
                new Vector3(fixedX, transform.position.y, target.position.z + depthOffset),
                fixedRotation);
            sideViewAnchored = true;
        }
    }

    private void SetTarget(Transform newTarget)
    {
        if (newTarget == target) return;
        isSwappingTarget = target != null && newTarget != null;
        target = newTarget;
    }

    private void SelectHighestActiveJet()
    {
        if (simulationManager == null)
            simulationManager = FindFirstObjectByType<SimulationManager>();
        if (simulationManager == null) return;

        SimulationSnapshot snapshot = simulationManager.GetSnapshot();
        if (snapshot?.Population == null) return;

        JetAgent highest = null;
        float highestY = float.NegativeInfinity;
        foreach (JetAgent jet in snapshot.Population)
        {
            if (jet == null || !jet.gameObject.activeInHierarchy) continue;
            if (jet.transform.position.y <= highestY) continue;

            highest = jet;
            highestY = jet.transform.position.y;
        }

        if (highest == null || highest.transform == target) return;

        bool currentUnavailable = target == null || !target.gameObject.activeInHierarchy;
        if (currentUnavailable || highestY >= target.position.y + minimumHeightLead)
            SetTarget(highest.transform);
    }

    private void LateUpdate()
    {
        SelectHighestActiveJet();
        if (target == null || !target.gameObject.activeInHierarchy) return;

        float desiredY = target.position.y + verticalOffset;
        float desiredZ = target.position.z + depthOffset;
        float activeSharpness = isSwappingTarget ? swapSharpness : followSharpness;
        float followT = 1f - Mathf.Exp(-activeSharpness * Time.unscaledDeltaTime);
        float smoothedY = Mathf.Lerp(transform.position.y, desiredY, followT);
        float smoothedZ = Mathf.Lerp(transform.position.z, desiredZ, followT);

        // Re-evaluate this every frame so Side Distance can be tuned live in the
        // Inspector. It remains relative to the original spawn X, not the jet's
        // current X, so the camera still does not follow lateral movement.
        fixedX = sideAnchorX + sideDistance;

        transform.SetPositionAndRotation(
            new Vector3(fixedX, smoothedY, smoothedZ),
            fixedRotation);

        if (isSwappingTarget)
        {
            float yError = desiredY - smoothedY;
            float zError = desiredZ - smoothedZ;
            if (yError * yError + zError * zError <= swapSettleDistance * swapSettleDistance)
                isSwappingTarget = false;
        }
    }
}
