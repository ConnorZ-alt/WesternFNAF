using UnityEngine;

public class BulletProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 50f;
    [SerializeField] private float lifeTime = 2f;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        // Launch forward
        rb.linearVelocity = transform.forward * speed;

        // Delete after a short time so scene doesn't fill with bullets
        Destroy(gameObject, lifeTime);
    }

    // Optional: basic hit feedback
    private void OnCollisionEnter(Collision other)
    {
        Destroy(gameObject);
    }
}