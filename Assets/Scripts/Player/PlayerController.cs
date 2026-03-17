using UnityEngine;

/// <summary>
/// PlayerController (
/// CharacterController version)
/// This script lets the player:
/// - Walk around using WASD (Horizontal/Vertical axes)
/// - Look around using the mouse
/// - Stop moving/looking when the game is paused
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6f;

    [Header("Look")]
    [SerializeField] private float lookSpeed = 2f;
    [SerializeField] private float lookXLimit = 45f;

    [Header("Controller Shape (standing)")]
    [SerializeField] private float defaultHeight = 2f;
    
    [SerializeField] private float groundCheckDistance = 1.2f;
    [SerializeField] private LayerMask movingSurfaceMask = ~0; // you can narrow this later
    // private MovingSurface currentSurface;

    // ---------------- CROUCH SETTINGS (DISABLED) ----------------
    // [Header("Crouch (DISABLED)")]
    // [SerializeField] private float crouchHeight = 1.3f;
    // [SerializeField] private float crouchSpeed = 3f;
    // [SerializeField] private float crouchCameraOffset = 0.3f;
    // -----------------------------------------------------------
    
    // Cached components/state
    private CharacterController characterController;
    private Vector3 moveWorldDirection = Vector3.zero; // where we want to move this frame (world space)
    private float pitchAngle = 0f;                     // camera up/down angle
    
    [SerializeField] private TrainController train;
        
    // [SerializeField] private TrainPathFollower train;

    // Defaults (so we can restore them)
    private Vector3 cameraDefaultLocalPosition;
    private Vector3 controllerDefaultCenter;

    private float bufferedX = 0;
    private float bufferedPitchAngle = 0;

    // Simple flag so we can freeze movement during pause
    private bool canMove = true;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        // If the camera isn't assigned, try to find one (saves you from a null reference crash).
        if (!playerCamera) playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera)
            cameraDefaultLocalPosition = playerCamera.transform.localPosition;

        controllerDefaultCenter = characterController.center;
        
        if (!train) train = FindObjectOfType<TrainController>();
    }

    private void Start()
    {
        // Lock the cursor so mouse-look feels like a normal FPS.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // These settings help CharacterController feel nicer on slopes/steps.
        characterController.skinWidth = 0.06f;
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 50f;
        characterController.enableOverlapRecovery = true;

        // Make sure we start standing.
        ApplyStandingShapeAndCamera();
    }

    private void Update()
    {
        // If the game is paused, freeze movement + looking.
        UpdatePauseState();
        if (!canMove) return;
        BufferLook();

        // ---------------- CROUCH (DISABLED) ----------------
        // HandleCrouch();
        // ---------------------------------------------------
    }
    private void FixedUpdate()
    {
        if (!canMove) return;
        // 1) Read movement inputs (WASD) and move the CharacterController.
        HandleMovement(); 
        
        // 2) Read mouse input and rotate camera/player.
        HandleLook();
    }

    /// <summary>
    /// If the game is paused, we stop the player from moving or looking.
    /// This keeps gameplay from continuing under the pause menu.
    /// </summary>
    private void UpdatePauseState()
    {
        if (SceneManagement.isPaused)
        {
            canMove = false;
            moveWorldDirection = Vector3.zero;
            return;
        }

        canMove = true;
    }

    /// <summary>
    /// Gets WASD input, turns it into a world direction, and moves the player.
    /// </summary>
    private void HandleMovement()
    {
        Vector3 worldForward = transform.forward;
        Vector3 worldRight = transform.right;

        float forwardInput = Input.GetAxis("Vertical");
        float rightInput = Input.GetAxis("Horizontal");

        float forwardSpeed = walkSpeed * forwardInput;
        float rightSpeed = walkSpeed * rightInput;

        moveWorldDirection = (worldForward * forwardSpeed) + (worldRight * rightSpeed);
        moveWorldDirection.y = 0f;
        
        // Actually move the player this frame.
        Vector3 trainDelta = (train != null) ? train.FrameDelta : Vector3.zero;
        characterController.Move(moveWorldDirection * Time.fixedDeltaTime + trainDelta);

        Vector3 playerMove = moveWorldDirection * Time.fixedDeltaTime;

        Vector3 surfaceDelta = GetSurfaceDelta();
        Vector3 surfaceRotMove = GetSurfaceRotationMove();

        characterController.Move(playerMove + surfaceDelta + surfaceRotMove);

    }
    
    /// <summary>
    /// Rotates the camera up/down (pitch) and rotates the player left/right (yaw).
    /// </summary>
    private void HandleLook()
    {
        if (!playerCamera) return;

        // Mouse Y makes the camera look up/down.
        pitchAngle += -bufferedPitchAngle * lookSpeed * 2;
        pitchAngle = Mathf.Clamp(pitchAngle, -lookXLimit, lookXLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(pitchAngle, 0f, 0f);

        // Mouse X rotates the whole player left/right.
        float yaw = bufferedX * lookSpeed * 2;
        transform.localRotation *= Quaternion.Euler(0f, yaw, 0f);
        bufferedX = 0;
        bufferedPitchAngle = 0;
    }

    private void BufferLook()
    {
        bufferedX += Input.GetAxis("Mouse X");
        bufferedPitchAngle += Input.GetAxis("Mouse Y");
    }

    /// <summary>
    /// Forces the CharacterController + camera to the standing configuration.
    /// This is used on Start so we don't accidentally spawn crouched.
    /// </summary>
    private void ApplyStandingShapeAndCamera()
    {
        characterController.height = defaultHeight;
        characterController.center = new Vector3(
            controllerDefaultCenter.x,
            defaultHeight * 0.5f,
            controllerDefaultCenter.z
        );

        // Reset speed to your normal walking speed.
        walkSpeed = 6f;

        if (playerCamera)
            playerCamera.transform.localPosition = cameraDefaultLocalPosition;
    }
    
    private Vector3 GetSurfaceDelta()
    {
        // Raycast down to see what we are standing on
        Vector3 origin = transform.position + Vector3.up * 0.2f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance, movingSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            var surface = hit.collider.GetComponentInParent<MovingSurface>();
            if (surface != null)
                return surface.FrameDelta;
        }

        return Vector3.zero;
    }
    
    private Vector3 GetSurfaceRotationMove()
    {
        Vector3 origin = transform.position + Vector3.up * 0.2f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundCheckDistance, movingSurfaceMask, QueryTriggerInteraction.Ignore))
        {
            var surface = hit.collider.GetComponentInParent<MovingSurface>();
            if (surface == null) return Vector3.zero;

            // Rotate the player's offset around the surface pivot
            Vector3 pivot = surface.transform.position;

            Vector3 offset = transform.position - pivot;
            offset = surface.RotationDelta * offset;

            Vector3 rotatedPos = pivot + offset;

            // We return the delta we need to apply this frame
            return rotatedPos - transform.position;
        }

        return Vector3.zero;
    }

    // ---------------- CROUCH LOGIC (DISABLED) ----------------
    // /// <summary>
    // /// This used to let the player crouch by holding LeftShift.
    // /// Per your new rule, crouching is disabled, so this whole method is commented out.
    // /// </summary>
    // private void HandleCrouch()
    // {
    //     if (Input.GetKey(KeyCode.LeftShift))
    //     {
    //         // CROUCH
    //         characterController.height = crouchHeight;
    //         characterController.center = new Vector3(controllerDefaultCenter.x, crouchHeight * 0.5f, controllerDefaultCenter.z);
    //         walkSpeed = crouchSpeed;
    //
    //         Vector3 crouchCameraLocalPosition = cameraDefaultLocalPosition - new Vector3(0f, crouchCameraOffset, 0f);
    //         playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, crouchCameraLocalPosition, Time.deltaTime * 8f);
    //     }
    //     else
    //     {
    //         // STAND
    //         characterController.height = defaultHeight;
    //         characterController.center = new Vector3(controllerDefaultCenter.x, defaultHeight * 0.5f, controllerDefaultCenter.z);
    //         walkSpeed = 6f;
    //         playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, cameraDefaultLocalPosition, Time.deltaTime * 8f);
    //     }
    // }
    // ---------------------------------------------------------
}
