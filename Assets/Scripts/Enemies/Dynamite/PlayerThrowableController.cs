using UnityEngine;

[DisallowMultipleComponent]
public class PlayerThrowableController : MonoBehaviour
{
    // This script’s job is simple:
    // - Let the player pick up dynamite.
    // - Let the player aim (right mouse).
    // - Let the player throw (left mouse).
    // - Keep the dynamite snapped to the player’s hand while holding it.

    [Header("Links")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform handAnchor;

    [Header("Input")]
    [SerializeField] private KeyCode pickKey  = KeyCode.F;
    [SerializeField] private KeyCode aimKey   = KeyCode.Mouse1; // hold to aim
    [SerializeField] private KeyCode throwKey = KeyCode.Mouse0; // click to throw

    [Header("Pickup")]
    [SerializeField] private float pickupRadius = 1.6f;      // sphere search size around player
    [SerializeField] private float pickupRayDistance = 2.5f; // ray search distance (more precise)
    [SerializeField] private LayerMask dynamiteMask = ~0;    // which layers count as dynamite targets

    [Header("Hold Pose")]
    [SerializeField] private Vector3 holdLocalPos   = new Vector3(0.25f, -0.2f, 0.5f);
    [SerializeField] private Vector3 holdLocalEuler = Vector3.zero;

    [Header("Throw Arc")]
    [SerializeField] private float arcTime = 0.8f;           // how long the throw takes (seconds)
    [SerializeField] private float maxThrowDistance = 15f;   // aim ray max distance

    [Header("Optional Aim Zoom")]
    [SerializeField] private bool  useAimFov = true;
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float aimFov    = 45f;
    [SerializeField] private float fovLerp   = 10f;

    // “Held item” references
    private Dynamite heldDynamite;
    private Rigidbody heldRigidbody;

    // State helpers (State pattern vibe)
    private bool IsHolding => heldDynamite != null;
    private bool IsAiming  => IsHolding && Input.GetKey(aimKey);

    public bool IsHoldingDynamite => IsHolding;

    private void Awake()
    {
        // Grab camera if playerCamera is not set in the Inspector.
        if (!playerCamera) playerCamera = Camera.main;
    }

    private void Update()
    {
        // If the game is paused, do nothing.
        if (SceneManagement.isPaused) return;

        HandlePickupInput();
        HandleThrowInput();

        UpdateHeldPose();
        UpdateAimFov();
    }

    // ----------------------------
    // Input handlers
    // ----------------------------

    private void HandlePickupInput()
    {
        // Press F:
        // - If holding something → drop it.
        // - If not holding → try to pick something up.
        if (!Input.GetKeyDown(pickKey)) return;

        if (IsHolding) DropHeld();
        else TryPickup();
    }

    private void HandleThrowInput()
    {
        // Throw only if:
        // - we are holding dynamite,
        // - we are aiming (RMB),
        // - we clicked throw key (LMB)
        if (!IsHolding) return;
        if (!IsAiming) return;

        if (Input.GetKeyDown(throwKey))
            ThrowFromAim();
    }

    // ----------------------------
    // Hold pose + FOV
    // ----------------------------

    private void UpdateHeldPose()
    {
        // Keep dynamite snapped to the player’s hand every frame.
        // This makes it look like it is “attached”.
        if (!IsHolding || !handAnchor) return;

        heldDynamite.transform.SetParent(handAnchor, false);
        heldDynamite.transform.localPosition = holdLocalPos;
        heldDynamite.transform.localRotation = Quaternion.Euler(holdLocalEuler);
    }

    private void UpdateAimFov()
    {
        // Optional: zoom camera in while aiming.
        if (!useAimFov || !playerCamera) return;

        float targetFieldOfView = IsAiming ? aimFov : normalFov;
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFieldOfView,
            Time.deltaTime * fovLerp
        );
    }

    // ----------------------------
    // Pickup flow
    // ----------------------------

    private void TryPickup()
    {
        // First try: raycast (feels precise because it matches where you look).
        if (TryPickupRaycast()) return;

        // Second try: overlap sphere (fallback if ray doesn’t hit).
        TryPickupOverlap();
    }

    private bool TryPickupRaycast()
    {
        if (!playerCamera) return false;

        Vector3 origin = playerCamera.transform.position;
        Vector3 dir    = playerCamera.transform.forward;

        if (Physics.Raycast(origin, dir, out RaycastHit hitInfo, pickupRayDistance, dynamiteMask, QueryTriggerInteraction.Ignore))
        {
            Dynamite found = hitInfo.collider.GetComponentInParent<Dynamite>();
            if (found != null)
            {
                Hold(found);
                Debug.Log("[PLAYER] Picked up (ray): " + found.name);
                return true;
            }
        }

        return false;
    }

    private bool TryPickupOverlap()
    {
        // This finds dynamite near the player and chooses the one closest to the middle of the screen.
        Collider[] overlapHits = Physics.OverlapSphere(transform.position, pickupRadius, dynamiteMask, QueryTriggerInteraction.Ignore);

        Dynamite bestCandidate = null;
        float bestDot = -1f;

        for (int i = 0; i < overlapHits.Length; i++)
        {
            Collider overlapCollider = overlapHits[i];
            if (!overlapCollider) continue;

            Dynamite candidate = overlapCollider.GetComponentInParent<Dynamite>();
            if (!candidate) continue;

            // “Dot product” is basically “how aligned is this object with where the camera faces”.
            // Bigger dot = more centered on screen.
            Vector3 toCandidate = (candidate.transform.position - playerCamera.transform.position).normalized;
            float dot = Vector3.Dot(playerCamera.transform.forward, toCandidate);

            if (dot > bestDot)
            {
                bestDot = dot;
                bestCandidate = candidate;
            }
        }

        if (bestCandidate != null)
        {
            Hold(bestCandidate);
            Debug.Log("[PLAYER] Picked up (overlap): " + bestCandidate.name);
            return true;
        }

        return false;
    }

    private void Hold(Dynamite dynamite)
    {
        // This method is the “one place” where we start holding.
        // That keeps pickup code consistent.

        heldDynamite = dynamite;
        heldRigidbody = heldDynamite.GetComponent<Rigidbody>();

        // Tell the dynamite it is being held (it will disable physics/collider, etc.).
        heldDynamite.OnPickedUp();
    }

    private void DropHeld()
    {
        // This releases the dynamite without throwing it.
        // Basically: put it down and let physics work again.

        if (!IsHolding) return;

        heldDynamite.transform.SetParent(null, true);

        if (heldRigidbody)
        {
            heldRigidbody.isKinematic = false;
            heldRigidbody.useGravity = true;
        }

        // Tell dynamite it is no longer held (resume fuse behavior).
        heldDynamite.OnThrown();

        heldDynamite = null;
        heldRigidbody = null;
    }

    // ----------------------------
    // Throw flow
    // ----------------------------

    private void ThrowFromAim()
    {
        // This throws dynamite toward whatever the camera is aiming at.
        if (!IsHolding || !playerCamera) return;

        Vector3 origin = playerCamera.transform.position;
        Vector3 dir    = playerCamera.transform.forward;

        Vector3 targetPosition;
        if (Physics.Raycast(origin, dir, out RaycastHit hitInfo, maxThrowDistance, ~0, QueryTriggerInteraction.Ignore))
            targetPosition = hitInfo.point;
        else
            targetPosition = origin + dir * maxThrowDistance;

        Vector3 startPosition = heldDynamite.transform.position;
        Vector3 initialVelocity = CalculateBallisticVelocity(startPosition, targetPosition, arcTime, Physics.gravity.y);

        // Unparent so it is free again.
        heldDynamite.transform.SetParent(null, true);

        // Turn physics on and launch it.
        if (heldRigidbody)
        {
            heldRigidbody.isKinematic = false;
            heldRigidbody.useGravity = true;

            // IMPORTANT: Unity uses rb.velocity, not rb.linearVelocity.
            heldRigidbody.linearVelocity = initialVelocity;
        }

        // Tell dynamite it is thrown so it can start/resume fuse.
        heldDynamite.OnThrown();

        Debug.Log("[PLAYER] Threw dynamite toward " + targetPosition);

        heldDynamite = null;
        heldRigidbody = null;
    }

    private static Vector3 CalculateBallisticVelocity(Vector3 startPosition, Vector3 endPosition, float travelTimeSeconds, float gravityY)
    {
        // This math figures out the velocity needed to reach the target in a certain time.
        // It is like: “How fast do I need to throw it so it lands there?”

        Vector3 toTarget = endPosition - startPosition;

        // Horizontal part (XZ plane)
        Vector3 horizontal = new Vector3(toTarget.x, 0f, toTarget.z);

        // Vertical velocity needed (includes gravity)
        float verticalVelocity = (toTarget.y - 0.5f * gravityY * travelTimeSeconds * travelTimeSeconds) / travelTimeSeconds;

        return (horizontal / travelTimeSeconds) + Vector3.up * verticalVelocity;
    }

    private void OnDrawGizmosSelected()
    {
        // Shows the pickup radius in the editor so you can see how big it is.
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
