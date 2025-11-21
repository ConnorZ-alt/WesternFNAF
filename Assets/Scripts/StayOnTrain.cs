using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class StayOnTrainBounds : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoxCollider trainBounds;      // Drag Train/PlayerBounds here

    [Header("Margins")]
    [SerializeField] private float edgePaddingMeters = 0.05f;    // small safety gap from walls
    [SerializeField] private float teleportEpsilon   = 0.001f;   // minimum delta before we snap

    private CharacterController characterController;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (!trainBounds)
            Debug.LogError("[StayOnTrainBounds] Please assign the Train/PlayerBounds BoxCollider.");
    }

    void LateUpdate()
    {
        if (!trainBounds) return;

        // Convert player world position into the bounds’ local space
        Transform boundsTransform = trainBounds.transform;
        Vector3 localPosition = boundsTransform.InverseTransformPoint(transform.position);

        // BoxCollider’s local-space center/size
        Vector3 localCenter = trainBounds.center;
        Vector3 localSize   = trainBounds.size;

        // Half-sizes minus a margin = (radius + skin + padding)
        float controllerRadius   = characterController ? characterController.radius     : 0.3f;
        float controllerSkin     = characterController ? characterController.skinWidth  : 0.06f;
        float collisionMargin    = controllerRadius + controllerSkin + edgePaddingMeters;

        float minX = localCenter.x - localSize.x * 0.5f + collisionMargin;
        float maxX = localCenter.x + localSize.x * 0.5f - collisionMargin;
        float minZ = localCenter.z - localSize.z * 0.5f + collisionMargin;
        float maxZ = localCenter.z + localSize.z * 0.5f - collisionMargin;

        // Clamp only X/Z so Y stays controlled by your controller
        float clampedX = Mathf.Clamp(localPosition.x, minX, maxX);
        float clampedZ = Mathf.Clamp(localPosition.z, minZ, maxZ);

        bool needsSnap =
            Mathf.Abs(localPosition.x - clampedX) > teleportEpsilon ||
            Mathf.Abs(localPosition.z - clampedZ) > teleportEpsilon;

        if (needsSnap)
        {
            Vector3 clampedLocal = new Vector3(clampedX, localPosition.y, clampedZ);
            Vector3 targetWorldPosition = boundsTransform.TransformPoint(clampedLocal);

            bool controllerWasEnabled = characterController.enabled;
            if (controllerWasEnabled) characterController.enabled = false;
            transform.position = targetWorldPosition;
            if (controllerWasEnabled) characterController.enabled = true;
        }
    }
}
