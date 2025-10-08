using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Camera playerCamera;
    public float walkSpeed = 6f;
    // public float runSpeed = 12f;
    // public float jumpPower = 7f;
    // public float gravity = 10f;
    public float lookSpeed = 2f;
    public float lookXLimit = 45f;
    public float defaultHeight = 2f;
    public float crouchHeight = 1.3f;
    public float crouchSpeed = 3f;
    public float crouchCameraOffset = 0.3f;

    private Vector3 moveDirection = Vector3.zero;
    private float rotationX = 0;
    private CharacterController characterController;
    private Vector3 cameraDefaultPos;
    private Vector3 defaultCenter;

    private bool canMove = true;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        cameraDefaultPos = playerCamera.transform.localPosition;
        defaultCenter = characterController.center;
    }

    void Update()
    {
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);

        // bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float curSpeedX = canMove ? (walkSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (walkSpeed) * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        // if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        // {
        //     moveDirection.y = jumpPower;
        // }
        // else
        // {
        //     moveDirection.y = movementDirectionY;
        // }

        // if (!characterController.isGrounded)
        // {
        //     moveDirection.y -= gravity * Time.deltaTime;
        // }

        if (moveDirection.z != 0 || moveDirection.x != 0)
        {
            print("breakiug");
        }

        if (Input.GetKey(KeyCode.LeftShift) && canMove)
        {
            
            characterController.height = crouchHeight / 2;
            characterController.center = new Vector3(defaultCenter.x, -1 * (crouchHeight / 2f), defaultCenter.z);
            walkSpeed = crouchSpeed;
            //playerCamera.transform.position.y = 1f;
            // runSpeed = crouchSpeed;

            Vector3 crouchCamPos = cameraDefaultPos - new Vector3(0, crouchCameraOffset, 0);
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, crouchCamPos, Time.deltaTime * 8f);
        }
        else
        {
            characterController.height = defaultHeight;
            
            characterController.center = new Vector3(defaultCenter.x, 0, defaultCenter.z);
            //characterController.center = defaultCenter; 
            
            walkSpeed = 6f;
            // runSpeed = 12f;
            
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, cameraDefaultPos, Time.deltaTime * 8f);
        }

        characterController.Move(moveDirection * Time.deltaTime);

        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }
    }
}