using UnityEngine;

[DisallowMultipleComponent]
public class PlayerThrowableController : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform handAnchor;

    [Header("Input")]
    [SerializeField] private KeyCode pickKey  = KeyCode.F;
    [SerializeField] private KeyCode aimKey   = KeyCode.Mouse1; // hold to aim
    [SerializeField] private KeyCode throwKey = KeyCode.Mouse0; // click to throw

    [Header("Pickup")]
    [SerializeField] private float pickupRadius = 1.6f;           // a bit larger
    [SerializeField] private float pickupRayDistance = 2.5f;      // ray grab distance
    [SerializeField] private LayerMask dynamiteMask = ~0;         // include your Dynamite layer

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

    private Dynamite held;
    private Rigidbody heldRigidbody;

    public bool IsHoldingDynamite => held != null;
    
    void Awake()
    {
        if (!playerCamera) playerCamera = Camera.main;
    }

    void Update()
    {
        if (SceneManagement.isPaused) return;

        // Pickup / drop
        if (Input.GetKeyDown(pickKey))
        {
            if (held) DropHeld();
            else TryPickup();
        }

        // Throw when aiming
        if (held && Input.GetKey(aimKey) && Input.GetKeyDown(throwKey))
        {
            ThrowFromAim();
        }

        // Maintain hand pose
        if (held && handAnchor)
        {
            held.transform.SetParent(handAnchor, false);
            held.transform.localPosition = holdLocalPos;
            held.transform.localRotation = Quaternion.Euler(holdLocalEuler);
        }

        // Aim zoom
        if (useAimFov && playerCamera)
        {
            float targetFieldOfView = (held && Input.GetKey(aimKey)) ? aimFov : normalFov;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFieldOfView, Time.deltaTime * fovLerp);
        }
    }

    // ---- pickup flow ----
    void TryPickup()
    {
        // 1) Raycast from camera for precise pickup
        if (TryPickupRaycast()) return;

        // 2) Fallback: sphere overlap around player
        TryPickupOverlap();
    }

    bool TryPickupRaycast()
    {
        if (!playerCamera) return false;

        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
                            out RaycastHit raycastHit, pickupRayDistance, dynamiteMask, QueryTriggerInteraction.Ignore))
        {
            var dynamiteHit = raycastHit.collider.GetComponentInParent<Dynamite>();
            if (dynamiteHit != null)
            {
                held = dynamiteHit;
                heldRigidbody = held.GetComponent<Rigidbody>();
                held.OnPickedUp();
                Debug.Log("[PLAYER] Picked up (ray): " + held.name);
                return true;
            }
        }
        return false;
    }

    bool TryPickupOverlap()
    {
        Collider[] overlapHits = Physics.OverlapSphere(transform.position, pickupRadius, dynamiteMask, QueryTriggerInteraction.Ignore);
        Dynamite bestCandidate = null;
        float bestScreenCenterDot = -1f;

        foreach (var overlapCollider in overlapHits)
        {
            if (!overlapCollider) continue;
            var dynamiteCandidate = overlapCollider.GetComponentInParent<Dynamite>();
            if (!dynamiteCandidate) continue;

            // prefer center-of-screen
            Vector3 directionToDynamite = (dynamiteCandidate.transform.position - playerCamera.transform.position).normalized;
            float screenCenterDot = Vector3.Dot(playerCamera.transform.forward, directionToDynamite);
            if (screenCenterDot > bestScreenCenterDot)
            {
                bestScreenCenterDot = screenCenterDot;
                bestCandidate = dynamiteCandidate;
            }
        }

        if (bestCandidate)
        {
            held = bestCandidate;
            heldRigidbody = held.GetComponent<Rigidbody>();
            held.OnPickedUp();
            Debug.Log("[PLAYER] Picked up (overlap): " + held.name);
            return true;
        }
        return false;
    }

    void DropHeld()
    {
        if (!held) return;

        if (heldRigidbody)
        {
            heldRigidbody.isKinematic = false;
            heldRigidbody.useGravity  = true;
        }

        held.transform.SetParent(null, true);
        held.OnThrown(); // resume fuse
        held = null;
        heldRigidbody = null;
    }

    void ThrowFromAim()
    {
        if (!held || !playerCamera) return;

        Vector3 targetPosition;
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
                            out RaycastHit raycastHit, maxThrowDistance, ~0, QueryTriggerInteraction.Ignore))
            targetPosition = raycastHit.point;
        else
            targetPosition = playerCamera.transform.position + playerCamera.transform.forward * maxThrowDistance;

        Vector3 startPosition = held.transform.position;
        Vector3 initialVelocity = CalculateBallisticVelocity(startPosition, targetPosition, arcTime, Physics.gravity.y);

        held.transform.SetParent(null, true);
        if (heldRigidbody)
        {
            heldRigidbody.isKinematic = false;
            heldRigidbody.useGravity  = true;
            heldRigidbody.linearVelocity = initialVelocity; // important
        }

        held.OnThrown();
        Debug.Log("[PLAYER] Threw dynamite toward " + targetPosition);
        held = null;
        heldRigidbody = null;
    }

    static Vector3 CalculateBallisticVelocity(Vector3 startPosition, Vector3 endPosition, float travelTimeSeconds, float gravityY)
    {
        Vector3 vectorToTarget   = endPosition - startPosition;
        Vector3 horizontalVector = new Vector3(vectorToTarget.x, 0f, vectorToTarget.z);
        float verticalVelocity   = (vectorToTarget.y - 0.5f * gravityY * travelTimeSeconds * travelTimeSeconds) / travelTimeSeconds;
        return (horizontalVector / travelTimeSeconds) + Vector3.up * verticalVelocity;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
