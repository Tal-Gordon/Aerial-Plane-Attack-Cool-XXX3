using UnityEngine;

/// <summary>Small live/statistical snapshot for one saved-run challenge participant.</summary>
public sealed class ChallengeRaceStats
{
    public int HoopsPassed;
    public int TotalHoops;
    public float ElapsedTime;
    public float CurrentSpeed;
    public float AverageSpeed;
    public float MaxSpeed;
    public float FinishSpeed;
    public bool Finished;
    public bool CompletedTrack;
    public bool Crashed;

    private float speedSum;
    private int speedSamples;

    public void Sample(JetAgent jet, FlightSchoolObjective objective, float elapsedTime)
    {
        if (jet == null || objective == null || Finished) return;

        Rigidbody rb = jet.GetComponent<Rigidbody>();
        CurrentSpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
        MaxSpeed = Mathf.Max(MaxSpeed, CurrentSpeed);
        speedSum += CurrentSpeed;
        speedSamples++;
        AverageSpeed = speedSamples > 0 ? speedSum / speedSamples : 0f;

        HoopsPassed = objective.GetPassedHoopCount(jet);
        TotalHoops = objective.WaypointCount;
        ElapsedTime = elapsedTime;
    }

    public void Finish(JetAgent jet, FlightSchoolObjective objective, float elapsedTime)
    {
        Sample(jet, objective, elapsedTime);
        Finished = true;
        FinishSpeed = CurrentSpeed;
        CompletedTrack = TotalHoops > 0 && HoopsPassed >= TotalHoops;
        Crashed = jet != null && jet.HasCrashed;
    }
}
