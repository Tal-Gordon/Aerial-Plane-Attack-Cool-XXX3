using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class Missile : MonoBehaviour
{
    [Header("Missile Physics")]
    public float thrust = 1500f; // High thrust to outrun the plane
    public float lifeTime = 5f;  // Self-destruct timer if it misses

    [Header("Effects & Components")]
    public GameObject explosionPrefab; // When we have an explosion particle system, we'll drag it here
    private TrailRenderer trail;
    private Rigidbody rb;
    private Collider myCollider;
    private MeshRenderer myMesh;

    [Header("Combat Settings")]
    public float damage = 50f;

    // Track if the missile is already 'dead' to prevent multiple collisions
    private bool isExploding = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        trail = GetComponent<TrailRenderer>();
        myCollider = GetComponentInChildren<Collider>();
        myMesh = GetComponentInChildren<MeshRenderer>(); // Assumes mesh is on self or first child

        rb.mass = 1f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;

        // Note: We don't use Destroy(gameObject, lifeTime) here because 
        // we need the trail to fade out if it expires too.
        Invoke("Explode", lifeTime);
    }

    // We will call this from the plane so the missile doesn't start at 0 mph
    public void InheritVelocity(Vector3 planeVelocity)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.linearVelocity = planeVelocity;
    }

    private void FixedUpdate()
    {
        // The rocket motor constantly pushes it forward (if we haven't exploded yet)
        if (!isExploding)
        {
            rb.AddRelativeForce(Vector3.forward * thrust);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Prevents hitting multiple objects in the same physics frame
        if (isExploding) return;

        CancelInvoke("Explode"); // Stop the lifetime timer
        Explode();
    }

    private void Explode()
    {
        if (isExploding) return;
        isExploding = true;

        // Spawn Explosion visual/audio
        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, transform.rotation);
        }

        // Deal Damage
        /*
        Health targetHealth = collision.gameObject.GetComponent<Health>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(damage);
        }
        */

        // Turn off the "Missile" parts instantly
        if (myMesh != null) myMesh.enabled = false;
        if (myCollider != null) myCollider.enabled = false;

        // Stop movement but don't delete the rigidbody yet, just in case
        rb.isKinematic = true;

        // Start the cleanup process in the background
        StartCoroutine(CleanupMissileAfterTrail());
    }

    private IEnumerator CleanupMissileAfterTrail()
    {
        if (trail != null)
        {
            // Stop generating *new* smoke, but let existing smoke fade
            trail.emitting = false;

            // Wait until the trail's total 'Time' has passed (e.g., if Time is 2.0s, we wait 2.0s)
            // We add 0.1s extra buffer just to be clean.
            yield return new WaitForSeconds(trail.time + 0.1f);
        }

        // Finally, destroy the actual GameObject now that the trail is gone
        Destroy(gameObject);
    }
}