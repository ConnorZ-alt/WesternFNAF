using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class DynamiteProjectile : MonoBehaviour
{
    // Dynamite can only be in ONE state at a time.
    // That helps us avoid bugs like “it’s held but also flying” type of weirdness.
    public enum DynamiteState
    {
        Flying, // moving through the air
        Stuck,  // glued to something (like the train floor)
        Held    // attached to player’s hand (not ticking if pause enabled)
    }

    [Header("Fuse")]
    [Tooltip("Seconds before explosion once fuse is started.")]
    [SerializeField] private float fuseSeconds = 3.5f;

    [Tooltip("If true, the fuse timer pauses while the dynamite is held.")]
    [SerializeField] private bool pauseFuseWhileHeld = true;

    [Header("Blinking (emission)")]
    [SerializeField] private Gradient blinkColor;
    [SerializeField] private float blinkMinHertz = 2f;
    [SerializeField] private float blinkMaxHertz = 10f;

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float explosionForce = 400f;
    [SerializeField] private float damage = 999f; // one-hit for testing
    [SerializeField] private LayerMask damageMask;
    [SerializeField] private GameObject explosionVfxObject;

    [Header("Stick")]
    [Tooltip("Layers the dynamite is allowed to stick to (ex: TrainFloor).")]
    [SerializeField] private LayerMask stickLayers;

    [Tooltip("If true, the first collision on a stickable layer glues the dynamite in place.")]
    [SerializeField] private bool stickOnFirstHit = true;

    // Components (cached so we don’t call GetComponent all the time)
    private Rigidbody rb;
    private Collider col;
    private Renderer rend;
    private Material materialInstance; // instanced material so blinking doesn’t affect other objects

    // State + fuse tracking
    private DynamiteState currentState = DynamiteState.Flying;

    private bool fuseStarted = false;
    private float remainingFuseSeconds;
    private Coroutine fuseCoroutine;

    // “Held” transform info
    private Transform heldParentTransform;
    private Vector3 heldLocalPosition = Vector3.zero;
    private Quaternion heldLocalRotation = Quaternion.identity;

    // Observer pattern idea:
    // Other scripts can listen for explosions without this class needing to know about them.
    public event Action OnExploded;
    
    private void Awake()
    {
        // Awake runs when the object is created.
        // We grab references and set starting values.

        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rend = GetComponentInChildren<Renderer>();

        // Create an instanced material so we can safely change emission color.
        if (rend != null)
        {
            materialInstance = rend.material;
            materialInstance.EnableKeyword("_EMISSION");
        }

        remainingFuseSeconds = fuseSeconds;
        SetState(DynamiteState.Flying);
    }

    /// <summary>
    /// Call right after Instantiate so this dynamite does not collide with the thrower immediately.
    /// </summary>
    public void Initialize(Collider colliderToIgnore)
    {
        if (colliderToIgnore != null && col != null)
            Physics.IgnoreCollision(col, colliderToIgnore, true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only react to collisions if we are actually flying.
        if (currentState != DynamiteState.Flying)
            return;

        // Check if the other object is in our stickable layers.
        int otherLayer = collision.collider.gameObject.layer;
        bool canStick = (stickLayers.value & (1 << otherLayer)) != 0;

        if (stickOnFirstHit && canStick)
        {
            StickToSurface(collision);
        }

        // Once we hit something, the fuse should start (or continue).
        TryStartFuse();
    }

    private void SetState(DynamiteState newState)
    {
        // Central place to change state so we don’t forget to set physics/collider correctly.
        currentState = newState;

        switch (currentState)
        {
            case DynamiteState.Flying:
                // Flying means physics is on and collider is enabled.
                if (col) col.enabled = true;
                if (rb)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                }
                break;

            case DynamiteState.Stuck:
                // Stuck means it does not move and it stays attached to the surface.
                if (col) col.enabled = true;
                if (rb)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                break;

            case DynamiteState.Held:
                // Held means it does not bump into the player and it’s locked to the hand.
                if (col) col.enabled = false;
                if (rb)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                break;
        }
    }

    private void StickToSurface(Collision collision)
    {
        // “Glue” the dynamite to the surface so it rides with the train.
        SetState(DynamiteState.Stuck);

        // Put it slightly above the contact so it doesn’t clip into the floor.
        ContactPoint contact = collision.contacts[0];
        transform.position = contact.point + contact.normal * 0.02f;
        transform.up = contact.normal;

        // Parent to the hit object so it moves with that object (like the train deck).
        transform.SetParent(collision.collider.transform, true);
    }

    // ----------------------------
    // Fuse control
    // ----------------------------

    private void TryStartFuse()
    {
        // This starts the fuse coroutine if it hasn’t started yet.
        if (fuseCoroutine != null)
            return;

        fuseStarted = true;
        fuseCoroutine = StartCoroutine(FuseRoutine());
    }

    private IEnumerator FuseRoutine()
    {
        // This is the countdown loop. It runs until remainingFuseSeconds hits 0.

        if (materialInstance != null)
            materialInstance.EnableKeyword("_EMISSION");

        while (remainingFuseSeconds > 0f)
        {
            // If we are held and we’re allowed to pause, don’t reduce the timer.
            if (pauseFuseWhileHeld && currentState == DynamiteState.Held)
            {
                BlinkVisuals(holdPhase: true);
                yield return null;
                continue;
            }

            // Tick down the timer normally.
            remainingFuseSeconds -= Time.deltaTime;
            if (remainingFuseSeconds < 0f)
                remainingFuseSeconds = 0f;

            BlinkVisuals(holdPhase: false);
            yield return null;
        }

        Explode();
    }

    private void BlinkVisuals(bool holdPhase)
    {
        // This makes the dynamite glow/blink so the player can “feel” the fuse.

        if (materialInstance == null)
            return;

        // When time is close to zero, blinking gets faster.
        float t = Mathf.InverseLerp(fuseSeconds, 0f, remainingFuseSeconds);
        float hertz = holdPhase ? blinkMinHertz : Mathf.Lerp(blinkMinHertz, blinkMaxHertz, t);

        // PingPong makes a value go 0 → 1 → 0 → 1 repeatedly.
        float phase = Mathf.PingPong(Time.time * hertz, 1f);

        Color emission = (blinkColor != null) ? blinkColor.Evaluate(phase) : (Color.white * phase);
        materialInstance.SetColor("_EmissionColor", emission);
    }

    // ----------------------------
    // Explosion
    // ----------------------------

    private void Explode()
    {
        // This does the explosion: VFX, push stuff, damage stuff, then destroy itself.

        Vector3 explosionPosition = transform.position;

        if (explosionVfxObject)
            Instantiate(explosionVfxObject, explosionPosition, Quaternion.identity);

        // 1) OverlapSphere finds all colliders in a radius.
        Collider[] hits = Physics.OverlapSphere(
            explosionPosition,
            explosionRadius,
            damageMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in hits)
        {
            // Push rigidbodies for “boom” feeling.
            if (hit.attachedRigidbody)
            {
                hit.attachedRigidbody.AddExplosionForce(
                    explosionForce,
                    explosionPosition,
                    explosionRadius,
                    0.5f,
                    ForceMode.Impulse
                );
            }

            // Damage anything that supports IDamageable.
            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(damage);
        }

        // 2) Fallback for CharacterController players (sometimes not in OverlapSphere results)
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            float dist = Vector3.Distance(explosionPosition, playerObj.transform.position);
            if (dist <= explosionRadius + 0.2f)
            {
                IDamageable playerDamageable = playerObj.GetComponentInParent<IDamageable>();
                if (playerDamageable != null)
                    playerDamageable.TakeDamage(damage);
            }
        }

        OnExploded?.Invoke();
        Destroy(gameObject);
    }

    // ----------------------------
    // Pickup / Throw API (Command-like actions)
    // ----------------------------

    public bool IsHeld => currentState == DynamiteState.Held;

    public void PickUp(Transform handTransform, Vector3 localPosition, Quaternion localRotation)
    {
        // This “attaches” the dynamite to the player’s hand and stops physics.

        heldParentTransform = handTransform;
        heldLocalPosition = localPosition;
        heldLocalRotation = localRotation;

        SetState(DynamiteState.Held);

        transform.SetParent(heldParentTransform, false);
        transform.localPosition = heldLocalPosition;
        transform.localRotation = heldLocalRotation;

        // If fuse already started, it will pause naturally in the coroutine (if pauseFuseWhileHeld is true).
    }

    public void Throw(Vector3 initialVelocity)
    {
        // This “releases” the dynamite and gives it a starting velocity.

        transform.SetParent(null, true);

        SetState(DynamiteState.Flying);

        // Unity uses rb.velocity (not linearVelocity).
        if (rb != null)
            rb.linearVelocity = initialVelocity;

        // If fuse never started yet, start it now.
        // (If it already started, it keeps going from remainingFuseSeconds.)
        if (!fuseStarted)
            TryStartFuse();
    }

    // ----------------------------
    // Debug gizmos
    // ----------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
