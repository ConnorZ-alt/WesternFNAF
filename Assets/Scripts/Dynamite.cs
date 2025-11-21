using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Dynamite : MonoBehaviour
{
    public enum DynamiteState { Flying, Stuck, Held }

    [Header("Fuse")]
    [Tooltip("Seconds before explosion (when ticking).")]
    [SerializeField] private float fuseSeconds = 3.5f;
    [Tooltip("If true, the fuse does not tick down while the player is holding it.")]
    [SerializeField] private bool pauseFuseWhileHeld = true;

    [Header("Blinking (emission)")]
    [SerializeField] private Gradient blinkColor;
    [SerializeField] private float blinkMinHertz = 2f;
    [SerializeField] private float blinkMaxHertz = 10f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float explosionForce  = 400f;
    [SerializeField] private float damage          = 999f;   // one-hit for testing
    [SerializeField] private LayerMask damageMask;
    [SerializeField] private GameObject explodeVfx;

    [Header("Sticking")]
    [Tooltip("Layers the dynamite is allowed to stick to (e.g., TrainFloor).")]
    [SerializeField] private LayerMask stickLayers;
    [Tooltip("If true, the first collision versus stickLayers glues the dynamite to that surface.")]
    [SerializeField] private bool stickOnFirstHit = true;

    // Components
    private Rigidbody rigidbodyComponent;
    private Collider  colliderComponent;
    private Renderer  rendererComponent;
    private Material  materialInstance;   // instanced material for emission blinking

    // State
    private DynamiteState currentState = DynamiteState.Flying;
    private bool fuseRunning = false;
    private float remainingFuseSeconds;
    private Coroutine fuseCoroutine;

    // Events
    public event Action OnExploded;

    // ----- Lifecycle -----
    void Awake()
    {
        rigidbodyComponent  = GetComponent<Rigidbody>();
        colliderComponent   = GetComponent<Collider>();
        rendererComponent   = GetComponentInChildren<Renderer>();

        if (rendererComponent != null)
        {
            // Get an instanced material so we can tweak _EmissionColor safely.
            materialInstance = rendererComponent.material;
            // For URP/HDRP enable emission keyword
            materialInstance.EnableKeyword("_EMISSION");
        }

        remainingFuseSeconds = fuseSeconds;
        currentState = DynamiteState.Flying;
    }

    /// Call this right after Instantiate to avoid colliding with the thrower.
    public void Initialize(Collider colliderToIgnore)
    {
        if (colliderToIgnore && colliderComponent)
            Physics.IgnoreCollision(colliderComponent, colliderToIgnore, true);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (currentState != DynamiteState.Flying) return;

        bool canStick = (stickLayers.value & (1 << collision.collider.gameObject.layer)) != 0;
        if (stickOnFirstHit && canStick)
            StickToSurface(collision);

        TryStartFuse();
    }

    // ----- Stick to deck -----
    void StickToSurface(Collision collision)
    {
        // Freeze in place
        rigidbodyComponent.isKinematic = true;
        rigidbodyComponent.useGravity  = false;
        rigidbodyComponent.linearVelocity = Vector3.zero;
        rigidbodyComponent.angularVelocity = Vector3.zero;

        // Place just above the contact to avoid z-fighting and align normal
        var contactPoint = collision.contacts[0];
        transform.position = contactPoint.point + contactPoint.normal * 0.02f;
        transform.up = contactPoint.normal;

        // Parent to the hit object so it rides with the train
        transform.SetParent(collision.collider.transform, true);

        currentState = DynamiteState.Stuck;
    }

    // ----- Fuse control -----
    void TryStartFuse()
    {
        if (fuseCoroutine != null) return;
        fuseRunning = true;
        fuseCoroutine = StartCoroutine(FuseRoutine());
    }

    System.Collections.IEnumerator FuseRoutine()
    {
        // Ensure emission is on
        if (materialInstance) materialInstance.EnableKeyword("_EMISSION");

        while (remainingFuseSeconds > 0f)
        {
            // Pause ticking if held and pause is enabled
            if (pauseFuseWhileHeld && currentState == DynamiteState.Held)
            {
                BlinkVisuals(holdPhase: true);
                yield return null;
                continue;
            }

            if (fuseRunning)
            {
                remainingFuseSeconds -= Time.deltaTime;
                if (remainingFuseSeconds < 0f) remainingFuseSeconds = 0f;
            }

            BlinkVisuals(holdPhase: false);
            yield return null;
        }

        Explode();
    }

    void BlinkVisuals(bool holdPhase)
    {
        if (!materialInstance) return;
        float t = Mathf.InverseLerp(fuseSeconds, 0f, remainingFuseSeconds);
        float hertz = holdPhase ? blinkMinHertz : Mathf.Lerp(blinkMinHertz, blinkMaxHertz, t);
        float phase = Mathf.PingPong(Time.time * hertz, 1f);
        Color emissionColor = (blinkColor != null) ? blinkColor.Evaluate(phase) : Color.white * phase;
        materialInstance.SetColor("_EmissionColor", emissionColor);
    }

    // ----- Explosion -----
    void Explode()
    {
        Vector3 explosionPosition = transform.position;

        if (explodeVfx) Instantiate(explodeVfx, explosionPosition, Quaternion.identity);

        // 1) Physics overlap for normal colliders
        Collider[] overlapHits = Physics.OverlapSphere(explosionPosition, explosionRadius, damageMask, QueryTriggerInteraction.Ignore);
        foreach (var colliderHit in overlapHits)
        {
            if (colliderHit.attachedRigidbody)
                colliderHit.attachedRigidbody.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, 0.5f, ForceMode.Impulse);

            var damageable = colliderHit.GetComponentInParent<IDamageable>();
            if (damageable != null) damageable.TakeDamage(damage);
        }

        // 2) Fallback for CharacterController players that might not be in damageMask
        GameObject playerGameObject = GameObject.FindWithTag("Player");
        if (playerGameObject)
        {
            float distanceToPlayer = Vector3.Distance(explosionPosition, playerGameObject.transform.position);
            if (distanceToPlayer <= explosionRadius + 0.2f)
            {
                var playerDamageable = playerGameObject.GetComponentInParent<IDamageable>();
                if (playerDamageable != null) playerDamageable.TakeDamage(damage);
            }
        }

        OnExploded?.Invoke();
        Destroy(gameObject);
    }

    // ----- Hooks used by ThrowDynamite -----
    public void OnPickedUp()
    {
        currentState = DynamiteState.Held;

        // Stop motion, disable physics & collisions while held
        if (rigidbodyComponent)
        {
            rigidbodyComponent.isKinematic = true;
            rigidbodyComponent.useGravity  = false;
            rigidbodyComponent.linearVelocity = Vector3.zero;
            rigidbodyComponent.angularVelocity = Vector3.zero;
        }
        if (colliderComponent) colliderComponent.enabled = false;

        if (pauseFuseWhileHeld)
            fuseRunning = false;   // pause the countdown
    }

    public void OnThrown()
    {
        currentState = DynamiteState.Flying;

        if (colliderComponent) colliderComponent.enabled = true;
        if (rigidbodyComponent)
        {
            rigidbodyComponent.isKinematic = false;
            rigidbodyComponent.useGravity  = true;
        }

        // Ensure the fuse is running after we throw
        fuseRunning = true;
        if (fuseCoroutine == null) fuseCoroutine = StartCoroutine(FuseRoutine());
    }

    // ----- Debug -----
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
