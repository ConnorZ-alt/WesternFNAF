using UnityEngine;

[DisallowMultipleComponent]
public class PlayerThrowableController : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform handAnchor;

    [Header("Input")]
    [SerializeField] private KeyCode pickKey  = KeyCode.F;
    [SerializeField] private KeyCode aimKey   = KeyCode.Mouse1;
    [SerializeField] private KeyCode throwKey = KeyCode.Mouse0;

    [Header("Pickup")]
    [SerializeField] private float pickupRadius = 1.6f;
    [SerializeField] private float pickupRayDistance = 2.5f;
    [SerializeField] private LayerMask dynamiteMask = ~0;

    [Header("Hold Pose")]
    [SerializeField] private Vector3 holdLocalPos   = new Vector3(0.25f, -0.2f, 0.5f);
    [SerializeField] private Vector3 holdLocalEuler = Vector3.zero;

    [Header("Throw Arc")]
    [SerializeField] private float arcTime = 0.8f;
    [SerializeField] private float maxThrowDistance = 15f;

    [Header("Optional Aim Zoom")]
    [SerializeField] private bool  useAimFov = true;
    [SerializeField] private float normalFov = 60f;
    [SerializeField] private float aimFov    = 45f;
    [SerializeField] private float fovLerp   = 10f;

    // The held item now uses the new projectile script only.
    private DynamiteProjectile heldDynamite;

    private bool IsHolding => heldDynamite != null;
    private bool IsAiming  => IsHolding && Input.GetKey(aimKey);

    public bool IsHoldingDynamite => IsHolding;
    
    [SerializeField] private float releaseForwardDistance = 0.8f;
    [SerializeField] private float releaseVerticalOffset = -0.05f;

    private CharacterController playerBody;

    private void Awake()
    {
        if (!playerCamera)
            playerCamera = Camera.main;
        
        playerBody = GetComponent<CharacterController>();
    }

    private void OnValidate()
    {
        pickupRadius = Mathf.Max(0f, pickupRadius);
        pickupRayDistance = Mathf.Max(0f, pickupRayDistance);
        arcTime = Mathf.Max(0.05f, arcTime);
        maxThrowDistance = Mathf.Max(0.1f, maxThrowDistance);
        fovLerp = Mathf.Max(0f, fovLerp);
    }

    private void Update()
    {
        if (SceneManagement.isPaused)
            return;

        HandlePickupInput();
        HandleThrowInput();
        UpdateAimFov();
    }

    // ----------------------------
    // Input handlers
    // ----------------------------

    private void HandlePickupInput()
    {
        if (!Input.GetKeyDown(pickKey))
            return;

        if (IsHolding)
            DropHeld();
        else
            TryPickup();
    }

    private void HandleThrowInput()
    {
        if (!IsHolding)
            return;

        // if (!IsAiming)
        //     return;

        if (Input.GetKeyDown(throwKey))
            ThrowFromAim();
    }

    // ----------------------------
    // Aim FOV
    // ----------------------------

    private void UpdateAimFov()
    {
        if (!useAimFov || !playerCamera)
            return;

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
        if (TryPickupRaycast())
            return;

        TryPickupOverlap();
    }

    private bool TryPickupRaycast()
    {
        if (!playerCamera)
            return false;

        Vector3 origin = playerCamera.transform.position;
        Vector3 dir = playerCamera.transform.forward;

        if (Physics.Raycast(origin, dir, out RaycastHit hitInfo, pickupRayDistance, dynamiteMask, QueryTriggerInteraction.Ignore))
        {
            DynamiteProjectile found = hitInfo.collider.GetComponentInParent<DynamiteProjectile>();
            if (found != null && !found.IsHeld)
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
        if (!playerCamera)
            return false;

        Collider[] overlapHits = Physics.OverlapSphere(
            transform.position,
            pickupRadius,
            dynamiteMask,
            QueryTriggerInteraction.Ignore
        );

        DynamiteProjectile bestCandidate = null;
        float bestDot = -1f;

        for (int i = 0; i < overlapHits.Length; i++)
        {
            Collider overlapCollider = overlapHits[i];
            if (!overlapCollider)
                continue;

            DynamiteProjectile candidate = overlapCollider.GetComponentInParent<DynamiteProjectile>();
            if (candidate == null || candidate.IsHeld)
                continue;

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

    private void Hold(DynamiteProjectile dynamite)
    {
        if (dynamite == null)
            return;

        Transform holdParent = handAnchor != null
            ? handAnchor
            : (playerCamera != null ? playerCamera.transform : transform);

        heldDynamite = dynamite;

        // Ignore collision with the player so the projectile does not instantly hit us on throw.
        if (playerBody != null)
            heldDynamite.Initialize(playerBody);

        heldDynamite.PickUp(
            holdParent,
            holdLocalPos,
            Quaternion.Euler(holdLocalEuler)
        );
    }

    private void DropHeld()
    {
        if (!IsHolding)
            return;

        // For now, "drop" is just a zero-velocity release using the projectile API.
        // This keeps Step 2 simple and fully moves the controller onto DynamiteProjectile.
        heldDynamite.Throw(Vector3.zero);

        Debug.Log("[PLAYER] Dropped dynamite.");

        ClearHeldReference();
    }

    // ----------------------------
    // Throw flow
    // ----------------------------

    private void ThrowFromAim()
    {
        if (!IsHolding || !playerCamera)
            return;

        Vector3 origin = playerCamera.transform.position;
        Vector3 dir = playerCamera.transform.forward;

        Vector3 targetPosition;
        if (Physics.Raycast(origin, dir, out RaycastHit hitInfo, maxThrowDistance, ~0, QueryTriggerInteraction.Ignore))
            targetPosition = hitInfo.point;
        else
            targetPosition = origin + dir * maxThrowDistance;

        Vector3 releasePosition = playerCamera.transform.position
                                  + playerCamera.transform.forward * releaseForwardDistance
                                  + Vector3.up * releaseVerticalOffset;

        // Move the projectile slightly out in front before throwing.
        heldDynamite.transform.SetPositionAndRotation(
            releasePosition,
            Quaternion.LookRotation(playerCamera.transform.forward, Vector3.up)
        );

        Vector3 initialVelocity = CalculateBallisticVelocity(
            releasePosition,
            targetPosition,
            arcTime,
            Physics.gravity.y
        );

        heldDynamite.Throw(initialVelocity);

        Debug.Log("[PLAYER] Threw dynamite toward " + targetPosition);

        ClearHeldReference();
    }

    private void ClearHeldReference()
    {
        heldDynamite = null;
    }

    private static Vector3 CalculateBallisticVelocity(
        Vector3 startPosition,
        Vector3 endPosition,
        float travelTimeSeconds,
        float gravityY)
    {
        Vector3 toTarget = endPosition - startPosition;

        Vector3 horizontal = new Vector3(toTarget.x, 0f, toTarget.z);

        float verticalVelocity =
            (toTarget.y - 0.5f * gravityY * travelTimeSeconds * travelTimeSeconds)
            / travelTimeSeconds;

        return (horizontal / travelTimeSeconds) + Vector3.up * verticalVelocity;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}