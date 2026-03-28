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
    private FixedJoint fixedJoint;

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

    private Collider lastStuckCollider;

    [Tooltip("If true, the first collision on a stickable layer glues the dynamite in place.")]
    [SerializeField] private bool stickOnFirstHit = true;

    // Components (cached so we don’t call GetComponent all the time)
    private Rigidbody rb;
    private Collider col;
    private Renderer rend;
    private Material materialInstance; // instanced material so blinking doesn’t affect other objects
    
    private TrainPathFollower inheritedTrain;
    private int inheritedFramesToSkip = 0;

    // State + fuse tracking
    private DynamiteState currentState = (DynamiteState)(-1);

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

        if (rend != null)
        {
            materialInstance = rend.material;
            materialInstance.EnableKeyword("_EMISSION");
        }
        else
        {
            Debug.LogWarning("DynamiteProjectile: No renderer found on prefab.");
        }        
        
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        remainingFuseSeconds = fuseSeconds;
        SetState(DynamiteState.Flying);
        rb.angularDamping = 3f;
        
    }

    /// <summary>
    /// Call right after Instantiate so this dynamite does not collide with the thrower immediately.
    /// </summary>
    public void Initialize(Collider colliderToIgnore)
    {
        if (colliderToIgnore != null && col != null)
            Physics.IgnoreCollision(col, colliderToIgnore, true);
    }
    
    private void FixedUpdate()
    {
        if (currentState != DynamiteState.Flying)
            return;

        if (rb == null || rb.isKinematic)
            return;

        if (inheritedTrain == null)
            return;

        if (inheritedFramesToSkip > 0)
        {
            inheritedFramesToSkip--;
            return;
        }

        Vector3 frameDelta = inheritedTrain.FrameDelta;
        frameDelta.y = 0f;

        rb.position += frameDelta;
        rb.linearVelocity = inheritedTrain.RotationDelta * rb.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only react to collisions if we are actually flying.
        if (currentState != DynamiteState.Flying)
            return;

        // Check if the other object is in our stickable layers.
        int otherLayer = collision.collider.gameObject.layer;
        bool canStick = (stickLayers.value & (1 << otherLayer)) != 0;
        
        Debug.Log($"[DynamiteProjectile] Hit {collision.collider.name} on layer {LayerMask.LayerToName(collision.collider.gameObject.layer)} normal={collision.contacts[0].normal}");
        
        if (stickOnFirstHit && canStick && collision.contactCount > 0)
        {
            Vector3 hitNormal = collision.contacts[0].normal;

            // Only stick to mostly upward-facing surfaces.
            if (Vector3.Dot(hitNormal, Vector3.up) > 0.35f)
                StickToSurface(collision);
        }

        // Once we hit something, the fuse should start (or continue).
        TryStartFuse();
    }

    private void SetState(DynamiteState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        switch (currentState)
        {
            case DynamiteState.Flying:
                if (col) col.enabled = true;

                if (rb)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                }
                break;

            case DynamiteState.Stuck:
                if (col) col.enabled = true;

                if (rb)
                {
                    // Stop movement BEFORE going kinematic.
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.useGravity = false;
                    rb.interpolation = RigidbodyInterpolation.None;
                    rb.isKinematic = true;
                }
                break;

            case DynamiteState.Held:
                if (col) col.enabled = false;

                if (rb)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.useGravity = false;
                    rb.interpolation = RigidbodyInterpolation.None;
                    rb.isKinematic = true;
                }
                break;
        }
    }

    private void StickToSurface(Collision collision)
    {
        if (currentState != DynamiteState.Flying)
            return;

        if (collision.contactCount == 0)
            return;

        ContactPoint contact = collision.contacts[0];

        // Make sure any old ignored floor collision is restored first.
        RestoreLastStuckCollision();

        float stickOffset = 0.05f;
        if (col != null)
            stickOffset = Mathf.Max(stickOffset, col.bounds.extents.y);

        Vector3 targetPos = contact.point + contact.normal * stickOffset;

        Vector3 projectedForward = Vector3.ProjectOnPlane(transform.forward, contact.normal);
        if (projectedForward.sqrMagnitude < 0.0001f)
            projectedForward = Vector3.forward;

        Quaternion targetRot = Quaternion.LookRotation(projectedForward.normalized, contact.normal);

        // Enter stuck state first.
        SetState(DynamiteState.Stuck);

        // Parent to the moving train floor/body so it follows the train.
        Transform stickParent = collision.rigidbody != null
            ? collision.rigidbody.transform
            : collision.transform;

        transform.SetParent(stickParent, true);
        transform.SetPositionAndRotation(targetPos, targetRot);

        // Remember this collider in case we need to restore collision later.
        lastStuckCollider = collision.collider;

        Physics.SyncTransforms();
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
    Vector3 explosionPosition = transform.position;

    if (explosionVfxObject)
        Instantiate(explosionVfxObject, explosionPosition, Quaternion.identity);

    Collider[] hits = Physics.OverlapSphere(explosionPosition, explosionRadius, damageMask, QueryTriggerInteraction.Ignore);

    // Prevent damaging the same target multiple times if it has multiple colliders.
    System.Collections.Generic.HashSet<IDamageable> damagedTargets = new();
    System.Collections.Generic.HashSet<Rigidbody> pushedBodies = new();

    bool playerAlreadyDamaged = false;

    foreach (Collider hit in hits)
    {
        if (!hit)
            continue;

        Rigidbody hitBody = hit.attachedRigidbody;
        if (hitBody != null && !pushedBodies.Contains(hitBody))
        {
            pushedBodies.Add(hitBody);

            hitBody.AddExplosionForce(
                explosionForce,
                explosionPosition,
                explosionRadius,
                0.5f,
                ForceMode.Impulse
            );
        }

        IDamageable damageable = hit.GetComponentInParent<IDamageable>();
        if (damageable != null && !damagedTargets.Contains(damageable))
        {
            damagedTargets.Add(damageable);
            damageable.TakeDamage(damage);

            if (hit.CompareTag("Player") || hit.GetComponentInParent<PlayerController>() != null)
                playerAlreadyDamaged = true;
        }
    }

    // Fallback for CharacterController players that might not appear in the overlap query.
    if (!playerAlreadyDamaged)
    {
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
        RestoreLastStuckCollision();

        heldParentTransform = handTransform;
        heldLocalPosition = localPosition;
        heldLocalRotation = localRotation;

        SetState(DynamiteState.Held);

        transform.SetParent(heldParentTransform, false);
        transform.localPosition = heldLocalPosition;
        transform.localRotation = heldLocalRotation;
    }

    public void Throw(Vector3 initialVelocity)
    {
        RestoreLastStuckCollision();

        transform.SetParent(null, true);

        SetState(DynamiteState.Flying);

        if (rb != null)
            rb.linearVelocity = initialVelocity;

        if (!fuseStarted)
            TryStartFuse();
    }
    
    public void SetInheritedTrain(TrainPathFollower train)
    {
        inheritedTrain = train;
        inheritedFramesToSkip = 2;
    }
    
    public void Drop()
    {
        RestoreLastStuckCollision();

        transform.SetParent(null, true);

        SetState(DynamiteState.Flying);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

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
    
    private void RestoreLastStuckCollision()
    {
        if (col != null && lastStuckCollider != null)
        {
            Physics.IgnoreCollision(col, lastStuckCollider, false);
            lastStuckCollider = null;
        }
    }
}
