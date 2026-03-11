using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Stats")]
    public float muzzleVelocity = 800f; // Fast, but not instant
    public float lifeTime = 2f; // Bullets despawn quickly to save memory
    public float damage = 10f;

    private void Start()
    {
        Rigidbody rb = GetComponent<Rigidbody>();

        // Make the bullet light and unaffected by gravity (optional, but standard for airplane MGs)
        rb.mass = 0.1f;
        rb.useGravity = false;

        // Apply the forward speed immediately
        rb.linearVelocity += transform.forward * muzzleVelocity;

        // Destroy after a set time so they don't fly forever
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Add impact particle effects here later

        // Destroy the bullet the moment it hits anything
        Destroy(gameObject);
    }
}