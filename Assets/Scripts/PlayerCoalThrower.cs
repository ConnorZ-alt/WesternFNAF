using System.Collections;
using UnityEngine;
// using UnityEngine.UIElements; // not used

[DisallowMultipleComponent]
public class PlayerCoalThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint;                 // empty under Main Camera (e.g., X=0.2, Y=-0.15, Z=0.6)
    [SerializeField] private GameObject coalPrefab;               // your Coal prefab
    [SerializeField] private ItemController revolverItemController; // drag your Revolver (ItemController) here
    [SerializeField] private PlayerThrowableController playerThrowableController; // add this
    [SerializeField] private LayerMask trainFloorMask;
    
    [Header("Input")]
    [SerializeField] private KeyCode pickUpKey  = KeyCode.F;      // F to pick up
    [SerializeField] private KeyCode throwKey   = KeyCode.Mouse0; // LMB to throw
    [SerializeField] private KeyCode aimKey     = KeyCode.Mouse1; // RMB to aim (fallback if gun missing)
    [SerializeField] private float throwDurationSeconds = 0.75f;
    [SerializeField] private CoalReceiver fallbackReceiver;
    
    [Header("Throw Tuning")]
    [SerializeField] private float forwardThrowForce = 12f;       // how far the coal goes (kept for future tuning)
    [SerializeField] private float upwardThrowBoost  = 4f;        // arc height  (kept for future tuning)

    [Header("UX")]
    [SerializeField] private bool requireAimToThrow = true;       // only throw while aiming

    private bool isInsideCoalSource = false;
    private GameObject heldCoalGameObject;
    
    public bool IsHoldingCoal => heldCoalGameObject != null;
    public bool IsAimingCoal => IsHoldingCoal && Input.GetKey(aimKey);

    void Awake()
    {
        if (!playerCamera)
        {
            // Try to auto-find the camera
            if (revolverItemController) playerCamera = revolverItemController.GetComponentInParent<Camera>();
            if (!playerCamera) playerCamera = Camera.main;
        }

        // Auto-wire the throwable controller if not assigned
        if (!playerThrowableController) playerThrowableController = FindObjectOfType<PlayerThrowableController>();
    }

    void Update()
    {
        // If game is paused, do nothing
        if (SceneManagement.isPaused) return;

        // If holding dynamite, ignore all coal inputs this frame
        if (playerThrowableController && playerThrowableController.IsHoldingDynamite)
            return;

        // Pick up coal (infinite) when standing in CoalSource
        if (isInsideCoalSource && heldCoalGameObject == null && Input.GetKeyDown(pickUpKey))
        {
            PickUpCoal();
        }

        // Throw if holding coal
        if (heldCoalGameObject != null && Input.GetKeyDown(throwKey))
        {
            bool isAimingNow = (revolverItemController && revolverItemController.IsAiming) || IsAimingCoal; // support both
            
            if (!requireAimToThrow || isAimingNow)
            {
                ThrowCoal();

                // Block gun shooting for this click so LMB does not also fire the gun
                if (revolverItemController)
                {
                    revolverItemController.SetExternalShootBlock(true);
                    StartCoroutine(ClearShootBlockNextFrame());
                }
            }
        }

        // Keep the held coal positioned at the hold point (no physics while held)
        if (heldCoalGameObject)
        {
            heldCoalGameObject.transform.position = holdPoint.position;
            heldCoalGameObject.transform.rotation = holdPoint.rotation;
        }
    }

    private void PickUpCoal()
    {
        heldCoalGameObject = Instantiate(coalPrefab, holdPoint.position, holdPoint.rotation);
        var rigidbody = heldCoalGameObject.GetComponent<Rigidbody>();
        if (rigidbody) rigidbody.isKinematic = true; // freeze while held
    }

    private void ThrowCoal()
    {
        if (!heldCoalGameObject || !playerCamera) return;

        var rigidbody = heldCoalGameObject.GetComponent<Rigidbody>();
        if (!rigidbody) return;

        rigidbody.isKinematic = false;
        rigidbody.useGravity = true;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Camera basis
        Vector3 cameraPosition = playerCamera.transform.position;
        Vector3 cameraForward  = playerCamera.transform.forward;

        // 1) Safe default so it is ALWAYS initialized
        Vector3 throwTargetWorld = cameraPosition + cameraForward * 6f;

        // 2) If crosshair is on a CoalReceiver, throw to its center
        if (Physics.Raycast(cameraPosition, cameraForward, out RaycastHit aimRaycastHit, 60f, ~0, QueryTriggerInteraction.Collide))
        {
            var aimedCoalReceiver = aimRaycastHit.collider.GetComponentInParent<CoalReceiver>();
            if (aimedCoalReceiver)
            {
                var boxCollider = aimRaycastHit.collider as BoxCollider;
                Vector3 receiverCenter = boxCollider ? boxCollider.bounds.center : aimRaycastHit.transform.position;
                throwTargetWorld = receiverCenter + Vector3.up * 0.03f;
            }
            else
            {
                // 3) Not a receiver—try to land on the train floor under the aim point
                if (Physics.Raycast(aimRaycastHit.point + Vector3.up * 2f, Vector3.down,
                                    out RaycastHit floorRaycastHit, 5f, trainFloorMask, QueryTriggerInteraction.Ignore))
                {
                    throwTargetWorld = floorRaycastHit.point + Vector3.up * 0.02f;
                }
            }
        }

        // 4) If we still did not find a great target, use the fallback receiver
        if (fallbackReceiver)
        {
            var collider = fallbackReceiver.GetComponent<Collider>();
            if (collider) throwTargetWorld = collider.bounds.center + Vector3.up * 0.03f;
            else          throwTargetWorld = fallbackReceiver.transform.position + Vector3.up * 0.03f;
        }

        // Compute ballistic and throw
        Vector3 startPosition = heldCoalGameObject.transform.position;
        Vector3 initialVelocity = CalculateBallisticVelocity(startPosition, throwTargetWorld, throwDurationSeconds, Physics.gravity.y);

        rigidbody.linearVelocity = initialVelocity;
        rigidbody.angularVelocity = Random.insideUnitSphere * 6f;

        heldCoalGameObject = null;

        Debug.DrawLine(startPosition, throwTargetWorld, Color.yellow, 1.0f);
    }

    private static Vector3 CalculateBallisticVelocity(Vector3 startWorldPosition, Vector3 endWorldPosition, float flightTimeSeconds, float gravityY)
    {
        Vector3 displacement = endWorldPosition - startWorldPosition;
        Vector3 horizontalDisplacement = new Vector3(displacement.x, 0f, displacement.z);
        float verticalDisplacement = displacement.y;

        float verticalVelocity = (verticalDisplacement - 0.5f * gravityY * flightTimeSeconds * flightTimeSeconds) / flightTimeSeconds;
        Vector3 horizontalVelocity = horizontalDisplacement / flightTimeSeconds;
        return horizontalVelocity + Vector3.up * verticalVelocity;
    }

    private IEnumerator ClearShootBlockNextFrame()
    {
        // Wait a frame so ItemController will not see this LMB
        yield return null;
        if (revolverItemController) revolverItemController.SetExternalShootBlock(false);
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        if (otherCollider.GetComponent<CoalSource>()) isInsideCoalSource = true;
    }

    private void OnTriggerExit(Collider otherCollider)
    {
        if (otherCollider.GetComponent<CoalSource>()) isInsideCoalSource = false;
    }
}
