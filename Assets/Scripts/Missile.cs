using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Missile : MonoBehaviour
{
    [Header("Missile Physics")]
    public float thrust = 1500f; // High thrust to outrun the plane
    public float lifeTime = 5f;  // Self-destruct timer if it misses

    [Header("Effects")]
    public GameObject explosionPrefab; // Drag an explosion particle system here
    public float damage = 50f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // A missile should be light compared to the plane (mass 10)
        rb.mass = 1f;

        // Low drag so it slices through the air
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;

        // Destroy the missile after a few seconds to prevent memory leaks in the scene
        Destroy(gameObject, lifeTime);
    }

    // We will call this from the plane so the missile doesn't start at 0 mph
    public void InheritVelocity(Vector3 planeVelocity)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.linearVelocity = planeVelocity;
    }

    private void FixedUpdate()
    {
        // The rocket motor constantly pushes it forward
        rb.AddRelativeForce(Vector3.forward * thrust);
    }

    private void OnCollisionEnter(Collision collision)
    {
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

        // Destroy the missile immediately upon impact
        Debug.Log(collision.gameObject.name);
        Destroy(gameObject);
    }
}