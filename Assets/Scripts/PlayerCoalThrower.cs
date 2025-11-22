using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCoalThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Transform holdPoint;                 // empty under Main Camera (e.g., X=0.2, Y=-0.15, Z=0.6)
    [SerializeField] private GameObject coalPrefab;               // Coal prefab
    [SerializeField] private ItemController revolverItemController; // Revolver (ItemController)
    [SerializeField] private PlayerThrowableController playerThrowableController; // your dynamite handler
    [SerializeField] private LayerMask trainFloorMask;

    [Header("Input")]
    [SerializeField] private KeyCode pickUpKey  = KeyCode.F;      // pick up
    [SerializeField] private KeyCode throwKey   = KeyCode.Mouse0; // throw
    [SerializeField] private KeyCode aimKey     = KeyCode.Mouse1; // coal aim (independent from gun)
    [SerializeField] private bool   coalAimUsesToggle = true;     // toggle-to-aim for coal
    [SerializeField] private float  throwDurationSeconds = 0.95f; // slightly longer to avoid “short lobs”
    [SerializeField] private CoalReceiver fallbackReceiver;       // funnel trigger (optional)

    [Header("Throw Tuning")]
    [SerializeField] private float minFlatDistance = 3.0f;        // ensure not a tiny lob
    [SerializeField] private float aimRayDistance  = 60f;         // crosshair ray length

    [Header("Coal Aim FOV")]
    [SerializeField] private bool  useAimFovCoal     = true;
    [SerializeField] private float normalFovCoal     = 60f;
    [SerializeField] private float aimFovCoal        = 45f;
    [SerializeField] private float fovLerpSpeedCoal  = 10f;

    [Header("UX")]
    [SerializeField] private bool requireAimToThrow = true;       // must be aiming to throw

    private bool isInsideCoalSource = false;
    private GameObject heldCoalGameObject;

    // coal-specific aim state (independent of gun)
    private bool coalAimActive = false;

    public bool IsHoldingCoal => heldCoalGameObject != null;
    public bool IsAimingCoal  => IsHoldingCoal && coalAimActive;

    void Awake()
    {
        if (!playerCamera)
        {
            if (revolverItemController) playerCamera = revolverItemController.GetComponentInParent<Camera>();
            if (!playerCamera) playerCamera = Camera.main;
        }
        if (!playerThrowableController) playerThrowableController = FindObjectOfType<PlayerThrowableController>();
    }

    void Update()
    {
        if (SceneManagement.isPaused) return;

        // If holding dynamite, ignore coal this frame
        if (playerThrowableController && playerThrowableController.IsHoldingDynamite)
            return;

        // ----- Coal aim input (toggle or hold) -----
        if (IsHoldingCoal)
        {
            if (coalAimUsesToggle)
            {
                if (Input.GetKeyDown(aimKey)) coalAimActive = !coalAimActive;
            }
            else
            {
                coalAimActive = Input.GetKey(aimKey);
            }
        }
        else
        {
            coalAimActive = false; // drop aim when not holding coal
        }

        // ----- Pickup -----
        if (isInsideCoalSource && !IsHoldingCoal && Input.GetKeyDown(pickUpKey))
        {
            PickUpCoal();
        }

        // ----- Throw -----
        if (IsHoldingCoal && Input.GetKeyDown(throwKey))
        {
            bool isAimingNow = IsAimingCoal;

            if (!requireAimToThrow || isAimingNow)
            {
                // block gun BEFORE throwing so Mouse0 doesn’t also fire the revolver this frame
                if (revolverItemController) revolverItemController.SetExternalShootBlock(true);

                ThrowCoal();

                // Clear the block next frame
                if (revolverItemController) StartCoroutine(ClearShootBlockNextFrame());
            }
        }

        // Keep the held coal snapped to the hold point
        if (IsHoldingCoal)
        {
            heldCoalGameObject.transform.position = holdPoint.position;
            heldCoalGameObject.transform.rotation = holdPoint.rotation;
        }

        // ----- Coal aim FOV zoom -----
        if (useAimFovCoal && playerCamera)
        {
            float targetFov = IsAimingCoal ? aimFovCoal : normalFovCoal;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * fovLerpSpeedCoal);
        }
    }

    private void PickUpCoal()
    {
        heldCoalGameObject = Instantiate(coalPrefab, holdPoint.position, holdPoint.rotation);
        if (heldCoalGameObject.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true; // freeze while held
        }
        // If using hold-to-aim, mirror current RMB state on pickup; toggle mode stays false until pressed
        if (!coalAimUsesToggle) coalAimActive = Input.GetKey(aimKey);
    }

    private void ThrowCoal()
    {
        if (!heldCoalGameObject || !playerCamera) return;
        if (!heldCoalGameObject.TryGetComponent<Rigidbody>(out var rb)) return;

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Camera basis
        Vector3 camPos = playerCamera.transform.position;
        Vector3 camDir = playerCamera.transform.forward;

        // --- Target selection ---
        bool   haveTarget = false;
        Vector3 target    = camPos + camDir * 8f; // sane default

        // 1) CoalReceiver center if crosshair hits it
        if (Physics.Raycast(camPos, camDir, out RaycastHit aimHit, aimRayDistance, ~0, QueryTriggerInteraction.Collide))
        {
            var aimedReceiver = aimHit.collider.GetComponentInParent<CoalReceiver>();
            if (aimedReceiver)
            {
                var box = aimHit.collider as BoxCollider;
                Vector3 center = box ? box.bounds.center : aimHit.collider.bounds.center;
                target = center + Vector3.up * 0.03f;
                haveTarget = true;
            }
            else
            {
                // 2) Otherwise land on TrainFloor below the aim point
                if (Physics.Raycast(aimHit.point + Vector3.up * 3f, Vector3.down,
                                    out RaycastHit floorHit, 8f, trainFloorMask, QueryTriggerInteraction.Ignore))
                {
                    target = floorHit.point + Vector3.up * 0.02f;
                    haveTarget = true;
                }
            }
        }

        // 3) Fallback only if nothing else worked
        if (!haveTarget && fallbackReceiver)
        {
            var col = fallbackReceiver.GetComponent<Collider>();
            target = (col ? col.bounds.center : fallbackReceiver.transform.position) + Vector3.up * 0.03f;
            haveTarget = true;
        }

        // Ensure a minimum flat distance so we don’t do a micro-lob
        Vector3 start = heldCoalGameObject.transform.position;
        Vector3 flatTarget = target; flatTarget.y = start.y;
        float flatDist = Vector3.Distance(start, flatTarget);
        if (flatDist < minFlatDistance)
        {
            target = start + (playerCamera.transform.forward.normalized * minFlatDistance) + Vector3.up * 0.02f;
        }

        // Ballistic velocity
        float t = Mathf.Clamp(throwDurationSeconds, 0.5f, 1.1f);
        Vector3 v0 = CalculateBallisticVelocity(start, target, t, Physics.gravity.y);

        rb.linearVelocity  = v0;
        rb.angularVelocity = Random.insideUnitSphere * 6f;

        // clear held/aim state
        heldCoalGameObject = null;
        coalAimActive = false;

        Debug.DrawLine(start, target, Color.yellow, 1.25f);
    }

    private static Vector3 CalculateBallisticVelocity(Vector3 start, Vector3 end, float time, float gravityY)
    {
        Vector3 to = end - start;
        Vector3 xz = new Vector3(to.x, 0f, to.z);
        float vy = (to.y - 0.5f * gravityY * time * time) / time;
        return (xz / time) + Vector3.up * vy;
    }

    private IEnumerator ClearShootBlockNextFrame()
    {
        // Wait one frame so the gun doesn’t read this same Mouse0
        yield return null;
        if (revolverItemController) revolverItemController.SetExternalShootBlock(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<CoalSource>()) isInsideCoalSource = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<CoalSource>()) isInsideCoalSource = false;
    }
}
