using UnityEngine;

public class TrailHelper : MonoBehaviour
{
    private TrailRenderer trail;

    void Awake()
    {
        // Get the Trail Renderer component on your child object
        trail = GetComponentInChildren<TrailRenderer>();
    }

    void OnEnable()
    {
        // Clears the old trail so it doesn't draw a line from its last death point
        if (trail != null)
        {
            trail.Clear();
        }
    }
}