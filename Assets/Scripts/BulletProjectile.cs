using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 50f;
    [SerializeField] private float lifetimeSeconds = 2f;

    private Rigidbody projectileRigidbody;

    void Awake()
    {
        projectileRigidbody = GetComponent<Rigidbody>();
    }

    void Start()
    {
        // Launch forward
        projectileRigidbody.linearVelocity = transform.forward * speed;

        // Delete after a short time so scene doesn't fill with bullets
        Destroy(gameObject, lifetimeSeconds);
    }

    // Optional: basic hit feedback
    private void OnCollisionEnter(Collision collision)
    {
        Destroy(gameObject);
    }
}