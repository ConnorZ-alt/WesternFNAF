using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class BulletProjectile : MonoBehaviour
{
    [Header("Bullet Movement")]
    [SerializeField] private float speed = 50f;

    [Header("Lifetime")]
    [SerializeField] private float lifetimeSeconds = 2f;

    private Rigidbody projectileRigidbody;
    private bool hasLaunched; // simple “state” so we only launch once

    private void Awake()
    {
        // Awake runs when the bullet object is created.
        // We grab the Rigidbody because we need it to move the bullet using physics.
        projectileRigidbody = GetComponent<Rigidbody>();

        if (!projectileRigidbody)
        {
            // This should never happen because of [RequireComponent],
            // but this is a safety net so the game doesn’t crash.
            Debug.LogError("[BulletProjectile] Missing Rigidbody. Disabling bullet.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        // OnEnable runs every time the bullet is turned on.
        // This is better than Start() if you ever use object pooling later.
        hasLaunched = false;

        LaunchForward();
        ScheduleSelfDestruct();
    }

    /// <summary>
    /// Pushes the bullet forward using its current forward direction.
    /// </summary>
    private void LaunchForward()
    {
        if (hasLaunched) return;
        hasLaunched = true;

        // We set velocity one time so the bullet flies forward.
        // "transform.forward" means “the direction the bullet is facing.”
        projectileRigidbody.linearVelocity = transform.forward * speed;
    }

    /// <summary>
    /// Deletes the bullet after a short time so the scene doesn’t fill up with bullets.
    /// </summary>
    private void ScheduleSelfDestruct()
    {
        // Cancel any previous destroy timers (important if pooled bullets are reused).
        CancelInvoke(nameof(DestroySelf));
        Invoke(nameof(DestroySelf), lifetimeSeconds);
    }

    private void DestroySelf()
    {
        Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // This runs when the bullet hits something solid.
        // For now, we just delete the bullet instantly on impact.
        DestroySelf();
    }
}
