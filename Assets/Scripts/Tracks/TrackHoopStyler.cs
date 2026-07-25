using Assets.Scripts.Sensors;
using UnityEngine;

/// <summary>
/// Colours a whole track's hoops: a hue ramp along the flight order so you can read how
/// far through the course a jet is from colour alone, and a distinct finish hoop. While a
/// jet is selected, every hoop except the one it is flying towards is dimmed.
///
/// Drop this on the same object as the <see cref="FlightSchoolObjective"/> — the hoops are
/// its children, in flight order (see that class's RebuildWaypointsFromChildren). Nothing
/// else needs wiring; a <see cref="HoopVisuals"/> is added to each hoop at startup.
///
/// The highlight dims rather than recolours on purpose: hue already means "position along
/// the track", and giving it a second meaning would make neither readable.
/// </summary>
[RequireComponent(typeof(FlightSchoolObjective))]
public class TrackHoopStyler : MonoBehaviour
{
    [Header("Sequence ramp")]
    [Tooltip("Colour of the first hoop; the ramp runs from here to Late Hoop Color.")]
    public Color earlyHoopColor = new Color(0.30f, 0.78f, 1.00f, 1f);   // sky blue
    [Tooltip("Colour approaching the end of the course.")]
    public Color lateHoopColor = new Color(0.85f, 0.35f, 0.85f, 1f);    // magenta
    [Tooltip("The last hoop, held apart from the ramp so the finish reads as a finish.")]
    public Color finishHoopColor = new Color(1.00f, 0.80f, 0.15f, 1f);  // gold

    [Tooltip("Emission multiplier. Above 1 blooms under post-processing; 1 just holds " +
             "the colour steady against any skybox.")]
    [Range(0.2f, 4f)] public float emission = 1.4f;

    [Header("Selected-jet highlight")]
    [Tooltip("How far hoops recede while a selected jet is targeting a different one. " +
             "1 disables the highlight entirely.")]
    [Range(0.05f, 1f)] public float dimmedAmount = 0.28f;

    private HoopVisuals[] hoops;
    private WaypointSensors trackedSensors;

    private void Start()
    {
        // Children in Hierarchy order — the same order the objective flies them in.
        int count = transform.childCount;
        hoops = new HoopVisuals[count];

        for (int i = 0; i < count; i++)
        {
            Transform hoop = transform.GetChild(i);
            var visuals = hoop.GetComponent<HoopVisuals>();
            if (visuals == null) visuals = hoop.gameObject.AddComponent<HoopVisuals>();
            hoops[i] = visuals;

            bool isFinish = i == count - 1;
            // Single-hoop tracks are all finish; otherwise ramp across the ones before it.
            float t = count > 2 ? i / (float)(count - 2) : 0f;
            visuals.SetSequenceColor(
                isFinish ? finishHoopColor : Color.Lerp(earlyHoopColor, lateHoopColor, t),
                emission);
        }
    }

    private void Update()
    {
        if (hoops == null || hoops.Length == 0) return;

        Transform target = CurrentTargetHoop();
        for (int i = 0; i < hoops.Length; i++)
        {
            if (hoops[i] == null) continue;
            // No selection (or the highlight is off) → every hoop at full colour.
            bool lit = target == null || hoops[i].transform == target;
            hoops[i].SetDimTarget(lit ? 1f : dimmedAmount);
        }
    }

    /// <summary>The hoop the selected jet is flying towards, or null when nothing is
    /// selected. Read from the jet's own sensor, so it always matches what the network
    /// is actually being told to aim at.</summary>
    private Transform CurrentTargetHoop()
    {
        if (dimmedAmount >= 1f || UIManager.Instance == null) return null;

        JetAgent selected = UIManager.Instance.Snapshot?.SelectedAgent;
        if (selected == null) { trackedSensors = null; return null; }

        if (trackedSensors == null || trackedSensors.gameObject != selected.gameObject)
            trackedSensors = selected.GetComponent<WaypointSensors>();

        return trackedSensors != null ? trackedSensors.currentWaypoint : null;
    }
}
