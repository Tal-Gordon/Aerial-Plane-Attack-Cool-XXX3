using UnityEngine;

public class Waypoint : MonoBehaviour
{
    [Tooltip("The next waypoint in the sequence. Leave null if this is the end")]
    public Waypoint Next;

    [Tooltip("The previous waypoint in the sequence. Leave null if this is the start")]
    public Waypoint Previous;

    // Optional: Draw a line in the editor to visualize the linked list
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.5f);

        if (Next != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, Next.transform.position);
        }
    }
}