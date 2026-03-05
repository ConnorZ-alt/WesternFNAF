using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCoalThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [SerializeField] private Transform holdPoint;
    // This is where the coal sits when you are holding it (like your hand area).

    [SerializeField] private GameObject coalPrefab;
    // The coal object we spawn when we pick up coal.

    [SerializeField] private ItemController revolverItemController;
    // We use this to block the gun for one frame so Mouse0 doesn’t shoot AND throw coal.

    [SerializeField] private PlayerThrowableController playerThrowableController;
    // This is your dynamite handler. If you're holding dynamite, coal does nothing.

    [SerializeField] private LayerMask trainFloorMask;

    [Header("Input")]
    [SerializeField] private KeyCode pickUpKey = KeyCode.F;
    [SerializeField] private KeyCode throwKey  = KeyCode.Mouse0;
    [SerializeField] private KeyCode aimKey    = KeyCode.Mouse1;

    [SerializeField] private bool coalAimUsesToggle = true;
    // If true: right-click toggles aim ON/OFF.
    // If false: you must hold right-click to aim.

    [SerializeField] private float throwDurationSeconds = 0.95f;
    // How long we pretend the throw "takes" for the ballistic math (longer = softer arc).

    [SerializeField] private CoalReceiver fallbackReceiver;
    // Optional: if we can't find a good aim target, we can throw toward this receiver.

    [Header("Throw Tuning")]
    [SerializeField] private float minFlatDistance = 3.0f;
    // This prevents tiny baby throws that barely leave your hand.

    [SerializeField] private float aimRayDistance = 60f;
    // How far the crosshair ray checks for a target.

    [Header("Coal Aim FOV")]
    [SerializeField] private bool useAimFovCoal = true;
    [SerializeField] private float normalFovCoal = 60f;
    [SerializeField] private float aimFovCoal = 45f;
    [SerializeField] private float fovLerpSpeedCoal = 10f;

    [Header("UX")]
    [SerializeField] private bool requireAimToThrow = true;
    // If true: you must be aiming (coal aim) before you can throw.
    
    // Other scripts can listen to these events (UI, sound, achievements, etc.)
    public event Action<GameObject> CoalPickedUp;
    public event Action<Vector3, Vector3> CoalThrown; // start position, target position
    public event Action<bool> CoalAimChanged;          // aiming on/off
    
    private TrainPathFollower train;
    
    private Rigidbody flyingCoalRb;
    
    // These are the main actions we do. By default, they call the normal methods.
    // But later, you can swap them out without rewriting this whole script.
    private Action pickupCommand;
    private Action throwCommand;
    
    private bool isInsideCoalSource;
    private GameObject heldCoalGameObject;

    // Coal aim is separate from gun aim
    private bool coalAimActive;

    // Public read-only info for other scripts
    public bool IsHoldingCoal => heldCoalGameObject != null;
    public bool IsAimingCoal  => IsHoldingCoal && coalAimActive;

    private void Awake()
    {
        // Awake runs when the object loads.
        // We use this to find missing references and set default behaviors.

        EnsureReferences();

        pickupCommand = PickUpCoal;
        throwCommand  = ThrowCoal;
    }
    
    private void FixedUpdate()
    {
        if (train == null || flyingCoalRb == null)
            return;

        // Apply angular velocity from train rotation
        Quaternion deltaRot = train.RotationDelta;
        float angle;
        Vector3 axis;
        deltaRot.ToAngleAxis(out angle, out axis);

        if (angle > 0.01f)
            flyingCoalRb.angularVelocity = axis * angle / Time.fixedDeltaTime;
    }

    private void Update()
    {
        // Update runs every frame.
        // This is where we check input and update the held coal position.

        if (SceneManagement.isPaused)
            return;

        // If holding dynamite, ignore coal controls this frame.
        if (playerThrowableController != null && playerThrowableController.IsHoldingDynamite)
            return;

        UpdateCoalAimInput();
        HandlePickupInput();
        HandleThrowInput();
        SnapHeldCoalToHoldPoint();
        UpdateCoalAimFov();
    }

    // ----------------------------
    // Input Handling (clean and separated)
    // ----------------------------

    private void UpdateCoalAimInput()
    {
        // This decides if coal aim is ON or OFF.
        // If you’re not holding coal, aim should always turn off.

        if (!IsHoldingCoal)
        {
            SetCoalAim(false);
            return;
        }

        bool newAimState = coalAimActive;

        if (coalAimUsesToggle)
        {
            if (Input.GetKeyDown(aimKey))
                newAimState = !coalAimActive;
        }
        else
        {
            newAimState = Input.GetKey(aimKey);
        }

        SetCoalAim(newAimState);
    }

    private void HandlePickupInput()
    {
        // If you are standing in a coal source area and you press F, pick up coal.

        if (!isInsideCoalSource)
            return;

        if (IsHoldingCoal)
            return;
        
        if (IsAimingCoal)
            return;

        if (Input.GetKeyDown(pickUpKey))
            pickupCommand?.Invoke();
    }

    private void HandleThrowInput()
    {
        // If you are holding coal and click Mouse0, try to throw it.

        if (!IsHoldingCoal)
            return;

        
            

        bool aimingNow = IsAimingCoal;

        // If we require aiming, you can’t throw unless you are aiming.
        if (requireAimToThrow && !aimingNow)
            return;

        // Block gun BEFORE throwing so Mouse0 doesn't also fire the revolver this frame.
        if (revolverItemController != null)
            revolverItemController.SetExternalShootBlock(true);
        
        if (Input.GetKeyDown(throwKey))
            throwCommand?.Invoke();

        // Clear the block next frame.
        if (revolverItemController != null)
            StartCoroutine(ClearShootBlockNextFrame());
    }

    private void PickUpCoal()
    {
        // This spawns a coal prefab and "holds" it in the player's hand area.

        if (coalPrefab == null || holdPoint == null)
        {
            Debug.LogWarning($"[{nameof(PlayerCoalThrower)}] Missing coalPrefab or holdPoint.", this);
            return;
        }

        heldCoalGameObject = Instantiate(coalPrefab, holdPoint.position, holdPoint.rotation, transform);

        // Freeze physics while it is held, so it doesn't fall or bounce in your hand.
        if (heldCoalGameObject.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // If using hold-to-aim, mirror current RMB state when you pick up.
        // If toggle mode, aim stays OFF until you press aimKey.
        if (!coalAimUsesToggle)
            SetCoalAim(Input.GetKey(aimKey));
        else
            SetCoalAim(false);

        CoalPickedUp?.Invoke(heldCoalGameObject);
    }

    private void ThrowCoal()
    {
        // This turns physics back on and launches the coal toward a chosen target.

        if (heldCoalGameObject == null)
            return;

        if (playerCamera == null)
        {
            Debug.LogWarning($"[{nameof(PlayerCoalThrower)}] No playerCamera set, can't throw.", this);
            return;
        }

        if (!heldCoalGameObject.TryGetComponent<Rigidbody>(out var rb))
        {
            Debug.LogWarning($"[{nameof(PlayerCoalThrower)}] Held coal has no Rigidbody.", this);
            ClearHeldCoalState();
            return;
        }

        // Turn physics back on.
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        heldCoalGameObject.transform.SetParent(null); // unparent for physics
        rb.isKinematic = false;
        rb.useGravity = true;
        // Keep a reference to apply train rotation
        if (rb != null)
            flyingCoalRb = rb;
        train = GetComponentInParent<TrainPathFollower>();

        Vector3 inheritedVelocity = Vector3.zero;

        if (train != null)
        {
            inheritedVelocity = train.FrameDelta / Time.fixedDeltaTime;
        }

        Vector3 start = heldCoalGameObject.transform.position;
        Vector3 target = ChooseThrowTarget(start);

        target = EnforceMinimumFlatDistance(start, target);

        float t = Mathf.Clamp(throwDurationSeconds, 0.5f, 1.1f);
        Vector3 v0 = CalculateBallisticVelocity(start, target, t);
        // Combine train motion + throw
        rb.linearVelocity = v0 + inheritedVelocity;
        rb.angularVelocity = UnityEngine.Random.insideUnitSphere * 6f;

        Debug.DrawLine(start, target, Color.yellow, 1.25f);

        CoalThrown?.Invoke(start, target);

        // Clear our held/aim state so we aren't "holding" nothing.
        ClearHeldCoalState();
    }

    // ----------------------------
    // Target Selection (kept separate so ThrowCoal is easier to read)
    // ----------------------------

    private Vector3 ChooseThrowTarget(Vector3 start)
    {
        // This picks where we want the coal to land.
        // Priority:
        // 1) If crosshair hits a CoalReceiver, aim at its center.
        // 2) Otherwise, aim at TrainFloor below the aim point.
        // 3) Otherwise, use fallbackReceiver if one exists.
        // 4) Otherwise, use a reasonable default in front of the camera.

        Vector3 camPos = playerCamera.transform.position;
        Vector3 camDir = playerCamera.transform.forward;

        // Default target: a little in front of the camera.
        Vector3 target = camPos + camDir * 8f;

        bool haveTarget = false;

        // 1) Raycast from the camera forward to see what we're aiming at.
        if (Physics.Raycast(camPos, camDir, out RaycastHit aimHit, aimRayDistance, ~0, QueryTriggerInteraction.Collide))
        {
            CoalReceiver aimedReceiver = aimHit.collider.GetComponentInParent<CoalReceiver>();
            if (aimedReceiver != null)
            {
                // Aim at the receiver center (slightly up so it feels nicer).
                Vector3 center = aimHit.collider.bounds.center;
                target = center + Vector3.up * 0.03f;
                haveTarget = true;
            }
            else
            {
                // 2) If we didn't hit a receiver, try dropping down to the train floor.
                if (Physics.Raycast(
                        aimHit.point + Vector3.up * 3f,
                        Vector3.down,
                        out RaycastHit floorHit,
                        8f,
                        trainFloorMask,
                        QueryTriggerInteraction.Ignore))
                {
                    target = floorHit.point + Vector3.up * 0.02f;
                    haveTarget = true;
                }
            }
        }

        // 3) Fallback receiver if nothing else worked.
        if (!haveTarget && fallbackReceiver != null)
        {
            Collider col = fallbackReceiver.GetComponent<Collider>();
            Vector3 center = col != null ? col.bounds.center : fallbackReceiver.transform.position;
            target = center + Vector3.up * 0.03f;
        }

        return target;
    }

    private Vector3 EnforceMinimumFlatDistance(Vector3 start, Vector3 target)
    {
        // This keeps the throw from being too tiny.
        // We ignore height and only check the flat ground distance.

        Vector3 flatTarget = target;
        flatTarget.y = start.y;

        float flatDist = Vector3.Distance(start, flatTarget);
        if (flatDist >= minFlatDistance)
            return target;

        // Push the target forward so the throw has a minimum distance.
        return start + (playerCamera.transform.forward.normalized * minFlatDistance) + Vector3.up * 0.02f;
    }

    private static Vector3 CalculateBallisticVelocity(Vector3 start, Vector3 end, float time)
    {
        // Ensure minimum time to avoid insanely large velocities
        time = Mathf.Max(time, 0.1f);

        Vector3 displacement = end - start;

        // Split horizontal and vertical
        Vector3 horizontal = new Vector3(displacement.x, 0f, displacement.z);
        float horizontalDistance = horizontal.magnitude;

        // Unity's gravity is negative, so we take Physics.gravity.y directly
        float g = Physics.gravity.y;

        // Vertical velocity needed to reach the target height in 'time' seconds
        float vy = (displacement.y - 0.5f * g * time * time) / time;

        // Horizontal velocity is just distance / time
        Vector3 vxz = horizontal / time;

        Vector3 velocity = vxz + Vector3.up * vy;

        // Optional: clamp max velocity to avoid crazy throws
        float maxVelocity = 20f; // tweak as needed
        if (velocity.magnitude > maxVelocity)
            velocity = velocity.normalized * maxVelocity;

        return velocity;
    }

    // ----------------------------
    // Visual / Holding Helpers
    // ----------------------------

    private void SnapHeldCoalToHoldPoint()
    {
        // If we are holding coal, keep it snapped to the hold point every frame.
        // This stops it from drifting away.

        if (!IsHoldingCoal || holdPoint == null)
            return;

        heldCoalGameObject.transform.position = holdPoint.position;
        heldCoalGameObject.transform.rotation = holdPoint.rotation;
    }

    private void UpdateCoalAimFov()
    {
        // This zooms the camera in/out when aiming coal.
        // It is separate from gun aiming.

        if (!useAimFovCoal || playerCamera == null)
            return;

        float targetFov = IsAimingCoal ? aimFovCoal : normalFovCoal;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, Time.deltaTime * fovLerpSpeedCoal);
    }

    private void SetCoalAim(bool aiming)
    {
        // This safely changes aim state and only fires the event if it actually changed.

        if (coalAimActive == aiming)
            return;

        coalAimActive = aiming;
        CoalAimChanged?.Invoke(coalAimActive);
    }

    private void ClearHeldCoalState()
    {
        // This clears our references after throwing or if something goes wrong.

        heldCoalGameObject = null;
        SetCoalAim(false);
    }

    // ----------------------------
    // Utility
    // ----------------------------

    private void EnsureReferences()
    {
        // This tries to auto-fill references if they weren't set in the Inspector.
        // It helps prevent "missing reference" bugs.

        if (playerCamera == null)
        {
            if (revolverItemController != null)
                playerCamera = revolverItemController.GetComponentInParent<Camera>();

            if (playerCamera == null)
                playerCamera = Camera.main;
        }

        if (playerThrowableController == null)
            playerThrowableController = FindObjectOfType<PlayerThrowableController>();
    }

    private IEnumerator ClearShootBlockNextFrame()
    {
        // Wait one frame so the gun doesn’t read this same Mouse0 press.
        yield return null;

        if (revolverItemController != null)
            revolverItemController.SetExternalShootBlock(false);
    }

    // ----------------------------
    // Coal Source Trigger Detection
    // ----------------------------

    private void OnTriggerEnter(Collider other)
    {
        // If we walked into a CoalSource zone, we can pick up coal.
        if (other != null && other.GetComponent<CoalSource>() != null)
            isInsideCoalSource = true;
    }

    private void OnTriggerExit(Collider other)
    {
        // If we left the CoalSource zone, we cannot pick up coal anymore.
        if (other != null && other.GetComponent<CoalSource>() != null)
            isInsideCoalSource = false;
    }
}