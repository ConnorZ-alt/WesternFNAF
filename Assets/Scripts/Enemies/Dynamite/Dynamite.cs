using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class Dynamite : MonoBehaviour
{
    // ----------------------------
    // State Pattern idea
    // ----------------------------
    // The dynamite can only be in one state at a time.
    public enum DynamiteState
    {
        Flying, // moving through the air
        Stuck,  // glued to a surface
        Held    // in the player's hand (physics off)
    }

    // ----------------------------
    // Fuse Settings
    // ----------------------------

    [Header("Fuse")]
    [Tooltip("Seconds before explosion (when ticking).")]
    [SerializeField] private float fuseSeconds = 3.5f;

    [Tooltip("If true, the fuse does not tick down while the player is holding it.")]
    [SerializeField] private bool pauseFuseWhileHeld = true;

    // ----------------------------
    // Blinking (Emission)
    // ----------------------------

    [Header("Blinking (emission)")]
    [SerializeField] private Gradient blinkColor;
    [SerializeField] private float blinkMinHertz = 2f;
    [SerializeField] private float blinkMaxHertz = 10f;

    // ----------------------------
    // Explosion Settings
    // ----------------------------

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 3f;
    [SerializeField] private float explosionForce = 400f;
    [SerializeField] private float damage = 999f; // one-hit for testing
    [SerializeField] private LayerMask damageMask;
    [SerializeField] private GameObject explodeVfx;

    // ----------------------------
    // Sticking Settings
    // ----------------------------

    [Header("Sticking")]
    [Tooltip("Layers the dynamite is allowed to stick to (e.g., TrainFloor).")]
    [SerializeField] private LayerMask stickLayers;

    [Tooltip("If true, the first collision versus stickLayers glues the dynamite to that surface.")]
    [SerializeField] private bool stickOnFirstHit = true;

    // ----------------------------
    // Components
    // ----------------------------

    private Rigidbody rb;
    private Collider col;
    private Renderer rend;
    private Material materialInstance; // instanced material so emission changes don't affect the prefab

    // ----------------------------
    // State + Fuse Runtime Data
    // ----------------------------

    private DynamiteState currentState = DynamiteState.Flying;

    private float remainingFuseSeconds;
    private Coroutine fuseCoroutine;

    // This means "the fuse is allowed to count down right now".
    private bool fuseRunning = false;
    
    // Other scripts can listen to these without being hard-coded inside Dynamite.
    public event Action<DynamiteState> StateChanged;
    public event Action FuseStarted;
    public event Action<float> FuseTick;     // sends remaining fuse seconds
    public event Action Exploded;
    
    // This lets you replace the explode behavior without rewriting fuse/state code.
    private Action explodeCommand;

    // ----------------------------
    // Lifecycle
    // ----------------------------

    private void Awake()
    {
        // Awake runs when the object loads.
        // We grab components and set safe defaults.

        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        rend = GetComponentInChildren<Renderer>();

        SetupMaterialInstanceForBlinking();

        remainingFuseSeconds = fuseSeconds;
        SetState(DynamiteState.Flying, invokeEvent: false);

        // Default explode command uses your existing explode logic.
        explodeCommand = DefaultExplode;
    }

    private void SetupMaterialInstanceForBlinking()
    {
        // We create a unique material instance so changing emission won't change the prefab or other dynamites.
        if (rend == null)
            return;

        materialInstance = rend.material;

        // For URP/HDRP, emission often needs the keyword on.
        materialInstance.EnableKeyword("_EMISSION");
    }

    /// <summary>
    /// Call this right after Instantiate to avoid colliding with the thrower.
    /// </summary>
    public void Initialize(Collider colliderToIgnore)
    {
        // This tells Unity physics: "these two colliders should not hit each other".
        if (colliderToIgnore != null && col != null)
            Physics.IgnoreCollision(col, colliderToIgnore, true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // This runs when the dynamite collides with something solid.

        // Only flying dynamite reacts to collisions.
        if (currentState != DynamiteState.Flying)
            return;

        if (stickOnFirstHit && CanStickTo(collision.collider))
        {
            StickToSurface(collision);
        }

        TryStartFuse();
    }

    // ----------------------------
    // Sticking
    // ----------------------------

    private bool CanStickTo(Collider hitCollider)
    {
        // This checks if the collider's layer is one of the layers we can stick to.
        int hitLayerMask = 1 << hitCollider.gameObject.layer;
        return (stickLayers.value & hitLayerMask) != 0;
    }

    private void StickToSurface(Collision collision)
    {
        // This glues the dynamite to the surface it hit.
        // We turn physics off and parent it so it rides along with the train.

        if (collision.contactCount == 0)
            return;

        // Freeze in place
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Place just above contact point so it doesn't flicker inside the surface.
        ContactPoint contact = collision.contacts[0];
        transform.position = contact.point + contact.normal * 0.02f;

        // Point the dynamite "up" along the surface normal so it looks stuck.
        transform.up = contact.normal;

        // Parent to the object so it moves with it.
        transform.SetParent(collision.collider.transform, true);

        SetState(DynamiteState.Stuck);
    }

    // ----------------------------
    // Fuse Control
    // ----------------------------

    private void TryStartFuse()
    {
        // Start the fuse only once.
        if (fuseCoroutine != null)
            return;

        fuseRunning = true;
        FuseStarted?.Invoke();

        fuseCoroutine = StartCoroutine(FuseRoutine());
    }

    private IEnumerator FuseRoutine()
    {
        // This runs every frame until the fuse reaches 0, then we explode.

        // Ensure emission is on if we have a material.
        if (materialInstance != null)
            materialInstance.EnableKeyword("_EMISSION");

        while (remainingFuseSeconds > 0f)
        {
            // If the fuse should pause while held, do that here.
            if (pauseFuseWhileHeld && currentState == DynamiteState.Held)
            {
                // Blink slowly while held, to show it's still "alive".
                BlinkVisuals(holdPhase: true);
                FuseTick?.Invoke(remainingFuseSeconds);
                yield return null;
                continue;
            }

            if (fuseRunning)
            {
                remainingFuseSeconds -= Time.deltaTime;
                if (remainingFuseSeconds < 0f)
                    remainingFuseSeconds = 0f;
            }

            BlinkVisuals(holdPhase: false);
            FuseTick?.Invoke(remainingFuseSeconds);

            yield return null;
        }

        // When fuse hits zero, explode.
        explodeCommand?.Invoke();
    }

    private void BlinkVisuals(bool holdPhase)
    {
        // This makes the dynamite glow/blink faster as it gets closer to exploding.

        if (materialInstance == null)
            return;

        // t goes from 0 to 1 as we get closer to explosion.
        float t = Mathf.InverseLerp(fuseSeconds, 0f, remainingFuseSeconds);

        // When held, keep blinking slow. Otherwise speed up as fuse gets low.
        float hertz = holdPhase ? blinkMinHertz : Mathf.Lerp(blinkMinHertz, blinkMaxHertz, t);

        // phase bounces between 0 and 1 (like a blinking light)
        float phase = Mathf.PingPong(Time.time * hertz, 1f);

        // Choose emission color
        Color emissionColor = blinkColor != null
            ? blinkColor.Evaluate(phase)
            : (Color.white * phase);

        materialInstance.SetColor("_EmissionColor", emissionColor);
    }

    // ----------------------------
    // Explosion (Default Command)
    // ----------------------------

    private void DefaultExplode()
    {
        // This is the normal explosion behavior:
        // - spawn VFX
        // - apply explosion force
        // - deal damage
        // - fire event
        // - destroy this object

        Vector3 explosionPosition = transform.position;

        if (explodeVfx != null)
            Instantiate(explodeVfx, explosionPosition, Quaternion.identity);

        // 1) Overlap sphere for normal colliders
        Collider[] overlapHits = Physics.OverlapSphere(
            explosionPosition,
            explosionRadius,
            damageMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider colliderHit in overlapHits)
        {
            if (colliderHit.attachedRigidbody != null)
            {
                colliderHit.attachedRigidbody.AddExplosionForce(
                    explosionForce,
                    explosionPosition,
                    explosionRadius,
                    0.5f,
                    ForceMode.Impulse
                );
            }

            var damageable = colliderHit.GetComponentInParent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(damage);
        }

        // 2) Fallback for CharacterController players that might not be in damageMask
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            float dist = Vector3.Distance(explosionPosition, player.transform.position);
            if (dist <= explosionRadius + 0.2f)
            {
                var playerDamageable = player.GetComponentInParent<IDamageable>();
                if (playerDamageable != null)
                    playerDamageable.TakeDamage(damage);
            }
        }

        Exploded?.Invoke();
        Destroy(gameObject);
    }

    // Let other scripts replace explosion behavior if needed.
    public void SetExplodeCommand(Action command) => explodeCommand = command;

    // ----------------------------
    // Hooks used by ThrowDynamite (Held / Thrown)
    // ----------------------------

    public void OnPickedUp()
    {
        // This puts dynamite into the "Held" state.
        // We disable physics and collider so it doesn't bump into things in your hand.

        SetState(DynamiteState.Held);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (col != null)
            col.enabled = false;

        // If we pause while held, stop fuse countdown.
        if (pauseFuseWhileHeld)
            fuseRunning = false;
    }

    public void OnThrown()
    {
        // This puts dynamite back into the "Flying" state.
        // We turn physics back on and ensure the fuse is running.

        SetState(DynamiteState.Flying);

        if (col != null)
            col.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Make sure the fuse continues after throwing.
        fuseRunning = true;

        if (fuseCoroutine == null)
            TryStartFuse();
    }

    // ----------------------------
    // State Helper
    // ----------------------------

    private void SetState(DynamiteState newState, bool invokeEvent = true)
    {
        // This changes state in one place.
        // It helps keep behavior consistent and easier to debug.

        if (currentState == newState)
            return;

        currentState = newState;

        if (invokeEvent)
            StateChanged?.Invoke(currentState);
    }

    // Optional helper if other scripts want to check state.
    public DynamiteState CurrentState => currentState;

    // ----------------------------
    // Debug Gizmo
    // ----------------------------

    private void OnDrawGizmosSelected()
    {
        // Draw the explosion radius in the editor so you can see the range.

        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}