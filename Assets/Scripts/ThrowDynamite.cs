using UnityEngine;

[DisallowMultipleComponent]
public class ThrowDynamite : MonoBehaviour
{
    [Header("Pickup")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float holdDistance = 0.7f;
    [SerializeField] private float holdHeightOffset = -0.1f;

    [Header("Throw")]
    [SerializeField] private float arcTime = 0.8f; // time to target
    [SerializeField] private float maxThrowDistance = 12f;

    private Rigidbody rigidbodyComponent;
    private bool isHeld;
    private Transform holderCamera;
    private Transform holderHandAnchor; // optional anchor

    void Awake()
    {
        rigidbodyComponent = GetComponent<Rigidbody>();
    }

    public void PickUp(Transform cameraTransform, Transform optionalHandAnchor = null)
    {
        holderCamera     = cameraTransform;
        holderHandAnchor = optionalHandAnchor;
        isHeld = true;

        if (rigidbodyComponent)
        {
            rigidbodyComponent.isKinematic = true;
            rigidbodyComponent.detectCollisions = false;
        }

        var dynamiteComponent = GetComponent<Dynamite>();
        if (dynamiteComponent) dynamiteComponent.OnPickedUp();
    }

    public void Drop()
    {
        isHeld = false;

        if (rigidbodyComponent)
        {
            rigidbodyComponent.isKinematic = false;
            rigidbodyComponent.detectCollisions = true;
        }

        holderCamera = null;
        holderHandAnchor = null;
    }

    public void ThrowAt(Vector3 worldTargetPosition)
    {
        isHeld = false;

        if (rigidbodyComponent)
        {
            rigidbodyComponent.isKinematic = false;
            rigidbodyComponent.detectCollisions = true;

            Vector3 startPosition = transform.position;
            Vector3 initialVelocity =
                CalculateBallisticVelocity(startPosition, worldTargetPosition, arcTime, Physics.gravity.y);

            rigidbodyComponent.linearVelocity = initialVelocity;
        }

        var dynamiteComponent = GetComponent<Dynamite>();
        if (dynamiteComponent) dynamiteComponent.OnThrown();

        holderCamera = null;
        holderHandAnchor = null;
    }

    void LateUpdate()
    {
        if (!isHeld || !holderCamera) return;

        // stick in front of camera (simple hold)
        Vector3 aimDirection    = holderCamera.forward;
        Vector3 desiredPosition = holderCamera.position + aimDirection * holdDistance + Vector3.up * holdHeightOffset;

        transform.position = desiredPosition;
        transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
    }

    static Vector3 CalculateBallisticVelocity(Vector3 startPosition, Vector3 endPosition, float travelTimeSeconds, float gravityY)
    {
        Vector3 toTargetVector  = endPosition - startPosition;
        Vector3 toTargetXZPlane = new Vector3(toTargetVector.x, 0f, toTargetVector.z);

        float verticalDelta     = toTargetVector.y;
        float horizontalDistance = toTargetXZPlane.magnitude;

        float verticalVelocity   = (verticalDelta - 0.5f * gravityY * travelTimeSeconds * travelTimeSeconds) / travelTimeSeconds;
        Vector3 horizontalVelocity = toTargetXZPlane / travelTimeSeconds;

        return horizontalVelocity + Vector3.up * verticalVelocity;
    }
}
