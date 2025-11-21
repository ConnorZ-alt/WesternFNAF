using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DynamiteProjectile : MonoBehaviour
{
    public enum DynamiteState { Flying, Stuck, Held }

    [Header("Fuse")]
    [SerializeField] private float   fuseSeconds = 3.5f;       // give the player time
    [SerializeField] private bool    pauseFuseWhileHeld = true;
    [SerializeField] private Gradient blinkColor;
    [SerializeField] private float   blinkMinHertz = 2f;
    [SerializeField] private float   blinkMaxHertz = 10f;

    [Header("Explosion")]
    [SerializeField] private float     explosionRadius = 3f;
    [SerializeField] private float     explosionForce = 400f;
    [SerializeField] private float     damage = 999f;           // one-hit for testing
    [SerializeField] private LayerMask damageMask;
    [SerializeField] private GameObject explosionVfxObject;

    [Header("Stick")]
    [SerializeField] private LayerMask stickLayers;             // include TrainFloor (+ Player if you want)
    [SerializeField] private bool      stickOnFirstHit = true;

    private Rigidbody        rigidbodyComponent;
    private Collider         colliderComponent;
    private Renderer         rendererComponent;
    private Material         materialInstance;

    private DynamiteState    currentState = DynamiteState.Flying;

    private bool             fuseStarted;
    private float            fuseStartTimeSeconds;
    private float            remainingFuseSeconds;
    private Coroutine        fuseCoroutine;

    private Transform        heldParentTransform;          // player hand when held
    private Vector3          heldLocalPosition = Vector3.zero;
    private Quaternion       heldLocalRotation = Quaternion.identity;
    
    public event System.Action OnExploded;

    void Awake()
    {
        rigidbodyComponent  = GetComponent<Rigidbody>();
        colliderComponent   = GetComponent<Collider>();
        rendererComponent   = GetComponentInChildren<Renderer>();
        if (rendererComponent) materialInstance = rendererComponent.material; // instanced
        remainingFuseSeconds = fuseSeconds;
    }

    /// Call immediately after Instantiate so we don't collide with the thrower (bandit).
    public void Initialize(Collider colliderToIgnore)
    {
        if (colliderToIgnore && colliderComponent)
            Physics.IgnoreCollision(colliderComponent, colliderToIgnore, true);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (currentState != DynamiteState.Flying) return;

        int otherLayer = collision.collider.gameObject.layer;
        bool isOnStickLayer = (stickLayers.value & (1 << otherLayer)) != 0;

        if (stickOnFirstHit && isOnStickLayer)
        {
            StickToSurface(collision);
        }

        // start (or keep) the fuse once we’ve landed
        TryStartFuse();
    }

    void StickToSurface(Collision collision)
    {
        // “Glue” to train so it rides along and stays still
        rigidbodyComponent.isKinematic = true;
        rigidbodyComponent.useGravity  = false;

        // Place just above the hit point and orient roughly with the surface normal
        var contactPoint = collision.contacts[0];
        transform.position = contactPoint.point + contactPoint.normal * 0.02f;
        transform.up       = contactPoint.normal;

        // Parent so we move with the train deck
        transform.SetParent(collision.collider.transform, true);

        currentState = DynamiteState.Stuck;
    }

    // ----- Fuse control -----
    void TryStartFuse()
    {
        if (fuseStarted && fuseCoroutine != null) return;
        fuseStarted = true;
        if (fuseCoroutine != null) StopCoroutine(fuseCoroutine);
        fuseCoroutine = StartCoroutine(FuseRoutine());
    }

    IEnumerator FuseRoutine()
    {
        float timeRemaining = remainingFuseSeconds; // continue from where we left off
        if (materialInstance) materialInstance.EnableKeyword("_EMISSION");

        while (timeRemaining > 0f)
        {
            // If we pause while held, just idle lights without ticking time down
            if (pauseFuseWhileHeld && currentState == DynamiteState.Held)
            {
                BlinkVisuals(holdPhase: true);
                yield return null;
                continue;
            }

            timeRemaining       -= Time.deltaTime;
            remainingFuseSeconds = timeRemaining; // keep for pause/resume
            BlinkVisuals(holdPhase: false);
            yield return null;
        }

        Explode();
    }

    void BlinkVisuals(bool holdPhase)
    {
        if (!materialInstance) return;

        // Speed up blinking as we approach zero (or pulse gently when held)
        float lerpT  = Mathf.InverseLerp(fuseSeconds, 0f, remainingFuseSeconds);
        float hertz  = holdPhase ? blinkMinHertz : Mathf.Lerp(blinkMinHertz, blinkMaxHertz, lerpT);
        float phase  = Mathf.PingPong(Time.time * hertz, 1f);
        Color glow   = (blinkColor != null) ? blinkColor.Evaluate(phase) : Color.white * phase;

        materialInstance.SetColor("_EmissionColor", glow);
    }

    private void Explode()
    {
        Vector3 explosionPosition = transform.position;

        if (explosionVfxObject) Instantiate(explosionVfxObject, explosionPosition, Quaternion.identity);

        // 1) Normal overlap for anything with a Collider on damageMask
        Collider[] overlapHits = Physics.OverlapSphere(explosionPosition, explosionRadius, damageMask, QueryTriggerInteraction.Ignore);
        foreach (var colliderHit in overlapHits)
        {
            // Push rigidbodies for juice
            if (colliderHit.attachedRigidbody)
                colliderHit.attachedRigidbody.AddExplosionForce(explosionForce, explosionPosition, explosionRadius, 0.5f, ForceMode.Impulse);

            // Damage if available
            var damageable = colliderHit.GetComponentInParent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(damage);
        }

        // 2) Fallback specifically for CharacterController players (no Collider returned)
        //    This covers the case where you forgot to add a trigger CapsuleCollider to the player.
        GameObject playerGameObject = GameObject.FindWithTag("Player"); // or hold a reference elsewhere
        if (playerGameObject)
        {
            // get a representative point for distance check
            Vector3 playerPosition    = playerGameObject.transform.position;
            float   distanceToPlayer  = Vector3.Distance(explosionPosition, playerPosition);
            if (distanceToPlayer <= explosionRadius + 0.2f) // small cushion
            {
                var playerDamageable = playerGameObject.GetComponentInParent<IDamageable>();
                if (playerDamageable != null)
                    playerDamageable.TakeDamage(damage);
            }
        }

        OnExploded?.Invoke();
        Destroy(gameObject);
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }

    // ----- Pickup / Throw API -----
    public bool IsHeld => currentState == DynamiteState.Held;

    public void PickUp(Transform handTransform, Vector3 localPosition, Quaternion localRotation)
    {
        heldParentTransform = handTransform;
        heldLocalPosition   = localPosition;
        heldLocalRotation   = localRotation;

        currentState = DynamiteState.Held;
        rigidbodyComponent.isKinematic = true;
        rigidbodyComponent.useGravity  = false;
        colliderComponent.enabled      = false; // avoid bumping the player

        transform.SetParent(heldParentTransform, false);
        transform.localPosition = heldLocalPosition;
        transform.localRotation = heldLocalRotation;
    }

    public void Throw(Vector3 initialVelocity)
    {
        // Unparent and resume physics
        transform.SetParent(null, true);
        colliderComponent.enabled    = true;
        rigidbodyComponent.isKinematic = false;
        rigidbodyComponent.useGravity  = true;
        rigidbodyComponent.linearVelocity = initialVelocity;

        currentState = DynamiteState.Flying;

        // If the fuse was paused, resume it
        if (!fuseStarted) TryStartFuse();
        // else it continues automatically from remainingFuseSeconds
    }
}
