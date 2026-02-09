using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class ThrowDynamite : MonoBehaviour
{
    // This script lets ONE dynamite object be picked up, held, dropped, and thrown.
    // Think of it like: “this dynamite knows how to behave when a player is holding it.”
    private enum HoldState
    {
        Free, // not being held
        Held  // attached to player camera/hand
    }

    [Header("Hold (when picked up)")]
    [SerializeField] private float holdDistance = 0.7f;       // how far in front of camera
    [SerializeField] private float holdHeightOffset = -0.1f;  // small vertical adjustment

    [Header("Throw")]
    [SerializeField] private float arcTime = 0.8f; // “time to target” for ballistic throw

    // Cached components
    private Rigidbody rb;
    private Dynamite dynamite; // optional (if this object also has Dynamite)

    // Hold references
    private HoldState state = HoldState.Free;
    private Transform holderCameraTransform;
    private Transform holderHandAnchorTransform; // if you have a hand socket, use it

    private void Awake()
    {
        // Grab Rigidbody once so we don’t keep calling GetComponent.
        rb = GetComponent<Rigidbody>();
        dynamite = GetComponent<Dynamite>();
    }

    // ----------------------------
    // Public API (these are like “commands”)
    // ----------------------------

    /// <summary>
    /// Called when the player picks up this dynamite.
    /// We store who is holding it (camera + optional hand anchor),
    /// and we turn off physics so it doesn’t fall.
    /// </summary>
    public void PickUp(Transform cameraTransform, Transform optionalHandAnchor = null)
    {
        holderCameraTransform = cameraTransform;
        holderHandAnchorTransform = optionalHandAnchor;

        SetHoldState(HoldState.Held);

        // Tell the Dynamite script (if present) that it is being held.
        // This is important if your fuse pauses when held.
        if (dynamite != null)
            dynamite.OnPickedUp();
    }

    /// <summary>
    /// Drops the dynamite (lets it fall normally).
    /// </summary>
    public void Drop()
    {
        SetHoldState(HoldState.Free);

        // Clear who was holding it.
        holderCameraTransform = null;
        holderHandAnchorTransform = null;

        // We do NOT call OnThrown() here because “drop” is not really a throw.
        // If you want drop to resume fuse, you can change that later.
    }

    /// <summary>
    /// Throws the dynamite toward a world target position.
    /// This turns physics back on and sets the Rigidbody velocity.
    /// </summary>
    public void ThrowAt(Vector3 worldTargetPosition)
    {
        // If we don’t have a rigidbody for some reason, abort.
        if (rb == null)
        {
            SetHoldState(HoldState.Free);
            holderCameraTransform = null;
            holderHandAnchorTransform = null;
            return;
        }

        // Free state first (physics comes back on here).
        SetHoldState(HoldState.Free);

        // Calculate launch velocity that hits the target in arcTime seconds.
        Vector3 startPosition = transform.position;
        Vector3 initialVelocity = CalculateBallisticVelocity(
            startPosition,
            worldTargetPosition,
            arcTime,
            Physics.gravity.y
        );

        // IMPORTANT: Unity uses rb.velocity, not rb.linearVelocity.
        rb.linearVelocity = initialVelocity;

        // Tell the dynamite script it’s thrown so fuse can resume/start.
        if (dynamite != null)
            dynamite.OnThrown();

        // Clear holder refs.
        holderCameraTransform = null;
        holderHandAnchorTransform = null;
    }

    // ----------------------------
    // Hold behavior
    // ----------------------------

    private void LateUpdate()
    {
        // LateUpdate is nice for “follow the camera” because the camera moved already.
        if (state != HoldState.Held) return;

        // If we have a hand anchor, just snap to it.
        if (holderHandAnchorTransform != null)
        {
            transform.position = holderHandAnchorTransform.position;
            transform.rotation = holderHandAnchorTransform.rotation;
            return;
        }

        // Otherwise, do the simple “in front of camera” hold.
        if (holderCameraTransform == null) return;

        Vector3 aimDirection = holderCameraTransform.forward;
        Vector3 desiredPosition =
            holderCameraTransform.position +
            aimDirection * holdDistance +
            Vector3.up * holdHeightOffset;

        transform.position = desiredPosition;
        transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
    }

    // ----------------------------
    // State change helper
    // ----------------------------

    private void SetHoldState(HoldState newState)
    {
        state = newState;

        if (rb == null) return;

        if (state == HoldState.Held)
        {
            // Held means: no physics and no collisions.
            // This prevents the dynamite from bumping the player’s body.
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.detectCollisions = false;
        }
        else // Free
        {
            // Free means: normal physics again.
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;
        }
    }

    // ----------------------------
    // Math helper
    // ----------------------------

    private static Vector3 CalculateBallisticVelocity(
        Vector3 startPosition,
        Vector3 endPosition,
        float travelTimeSeconds,
        float gravityY
    )
    {
        // This math finds the velocity needed to reach the target in a certain time.
        // It is like: “throw it this fast so it lands there.”

        Vector3 toTarget = endPosition - startPosition;

        // Horizontal part (XZ plane)
        Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);

        // Vertical speed needed (accounts for gravity)
        float vy = (toTarget.y - 0.5f * gravityY * travelTimeSeconds * travelTimeSeconds) / travelTimeSeconds;

        // Horizontal velocity is just distance / time
        Vector3 vxz = toTargetXZ / travelTimeSeconds;

        return vxz + Vector3.up * vy;
    }
}
