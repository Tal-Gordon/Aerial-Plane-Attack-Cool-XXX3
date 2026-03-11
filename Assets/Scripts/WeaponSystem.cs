using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class WeaponSystem : MonoBehaviour
{
    public enum WeaponMode { MachineGun, Missile }

    [Header("Current Status")]
    public WeaponMode currentWeapon = WeaponMode.MachineGun;

    [Header("Machine Gun Settings")]
    public Transform gunMuzzle; // Where the bullets come from
    public float machineGunRange = 1000f;
    public float fireRate = 0.1f; // Seconds between bullets
    private float nextFireTime;

    [Header("Missile Settings")]
    public GameObject missilePrefab;
    public Transform[] missileHardpoints; // Where missiles are attached under the wings
    public float missileCooldown = 1.5f;
    private float nextMissileTime;
    private int currentHardpointIndex = 0;

    private Rigidbody planeRb;

    private void Awake()
    {
        // Grab the plane's rigidbody so we know how fast we are going
        planeRb = GetComponent<Rigidbody>();
    }

    public void SwitchWeapon()
    {
        currentWeapon = currentWeapon == WeaponMode.MachineGun ? WeaponMode.Missile : WeaponMode.MachineGun;
        Debug.Log("Switched weapon to: " + currentWeapon);
    }

    public void Fire()
    {
        if (currentWeapon == WeaponMode.MachineGun)
        {
            FireMachineGun();
        }
        else if (currentWeapon == WeaponMode.Missile)
        {
            FireMissile();
        }
    }

    public void FireMachineGun()
    {
        // Enforce fire rate
        if (Time.time < nextFireTime) return;
        nextFireTime = Time.time + fireRate;

        // Visuals/Audio would go here (e.g., muzzle flash particle system, bang sound effect)

        // Perform the Raycast (Hitscan)
        RaycastHit hit;
        if (Physics.Raycast(gunMuzzle.position, gunMuzzle.forward, out hit, machineGunRange))
        {
            Debug.Log("Machine Gun hit: " + hit.collider.name);
            // Example: hit.collider.GetComponent<Health>()?.TakeDamage(10);
        }
    }

    private void FireMissile()
    {
        // Enforce cooldown
        if (Time.time < nextMissileTime) return;
        nextMissileTime = Time.time + missileCooldown;

        if (missileHardpoints.Length == 0 || missilePrefab == null) return;

        // Get the current hardpoint (e.g., left wing, then right wing)
        Transform spawnPoint = missileHardpoints[currentHardpointIndex];

        // Spawn the missile
        GameObject spawnedMissile = Instantiate(missilePrefab, spawnPoint.position, spawnPoint.rotation);

        // Pass the plane's current velocity to the missile
        Missile missileScript = spawnedMissile.GetComponent<Missile>();
        if (missileScript != null && planeRb != null)
        {
            missileScript.InheritVelocity(planeRb.linearVelocity);
        }

        // Move to the next hardpoint for the next shot, looping back to 0
        currentHardpointIndex = (currentHardpointIndex + 1) % missileHardpoints.Length;
    }
}