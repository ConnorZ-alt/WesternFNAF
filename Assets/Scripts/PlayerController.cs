using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Camera playerCamera;

    public float walkSpeed = 6f;
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;
    public float defaultHeight = 2f;
    public float crouchHeight = 1.3f;
    public float crouchSpeed = 3f;
    public float crouchCameraOffset = 0.3f;

    private Vector3 currentMoveDirection = Vector3.zero;
    private float pitchAngle = 0f;
    private CharacterController characterController;
    private Vector3 cameraDefaultLocalPosition;
    private Vector3 controllerDefaultCenter;

    private bool canMove = true;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        cameraDefaultLocalPosition = playerCamera.transform.localPosition;
        controllerDefaultCenter = characterController.center;

        characterController.skinWidth = 0.06f;
        characterController.stepOffset = 0.3f;
        characterController.slopeLimit = 50f;
        characterController.enableOverlapRecovery = true;
    }

    void Update()
    {
        if (SceneManagement.isPaused)
        {
            canMove = false;
            currentMoveDirection = Vector3.zero;
            return;
        }
        else
        {
            canMove = true;
        }

        Vector3 worldForwardDirection = transform.TransformDirection(Vector3.forward);
        Vector3 worldRightDirection   = transform.TransformDirection(Vector3.right);

        // bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float forwardAxisSpeed = canMove ? (walkSpeed) * Input.GetAxis("Vertical") : 0f;
        float rightAxisSpeed   = canMove ? (walkSpeed) * Input.GetAxis("Horizontal") : 0f;

        currentMoveDirection = (worldForwardDirection * forwardAxisSpeed) + (worldRightDirection * rightAxisSpeed);
        currentMoveDirection.y = 0f;

        if (Input.GetKey(KeyCode.LeftShift) && canMove)
        {
            // CROUCH
            characterController.height = crouchHeight;
            characterController.center = new Vector3(controllerDefaultCenter.x, crouchHeight * 0.5f, controllerDefaultCenter.z);
            walkSpeed = crouchSpeed;

            Vector3 crouchCameraLocalPosition = cameraDefaultLocalPosition - new Vector3(0f, crouchCameraOffset, 0f);
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, crouchCameraLocalPosition, Time.deltaTime * 8f);
        }
        else
        {
            // STAND
            characterController.height = defaultHeight;
            characterController.center = new Vector3(controllerDefaultCenter.x, defaultHeight * 0.5f, controllerDefaultCenter.z);

            walkSpeed = 6f; // keep original walk speed

            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, cameraDefaultLocalPosition, Time.deltaTime * 8f);
        }

        characterController.Move(currentMoveDirection * Time.deltaTime);

        if (canMove)
        {
            pitchAngle += -Input.GetAxis("Mouse Y") * lookSpeed;
            pitchAngle = Mathf.Clamp(pitchAngle, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(pitchAngle, 0, 0);

            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }
    }
}
