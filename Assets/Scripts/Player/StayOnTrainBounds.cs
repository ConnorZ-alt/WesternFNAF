using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class StayOnTrainBounds : MonoBehaviour
{
    [Header("Behaviour")]
    [Tooltip("Extra gap away from the walls, added on top of CharacterController radius + skin.")]
    [SerializeField] private float edgePaddingMeters = 0.05f;

    [Tooltip("Minimum delta (meters) before we actually snap the player to the clamp position.")]
    [SerializeField] private float snapEpsilonMeters = 0.001f;

    [Header("Which triggers count as train bounds")]
    [SerializeField] private LayerMask trainBoundsMask;

    private CharacterController characterController;

    // All train bounds volumes we are currently inside
    private readonly List<BoxCollider> insideBounds = new();

    // The one we clamp to this frame
    private BoxCollider activeBounds;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        // Safety default: if not set, treat "everything" as allowed.
        if (trainBoundsMask.value == 0) trainBoundsMask = ~0;
    }

    private void OnTriggerEnter(Collider other)
    {
        var bc = other as BoxCollider;
        if (!bc) return;

        // Only accept bounds on the TrainBounds layer (or mask)
        if (((1 << bc.gameObject.layer) & trainBoundsMask.value) == 0) return;

        if (!insideBounds.Contains(bc))
            insideBounds.Add(bc);
        
        // Debug.Log("[StayOnTrainBounds] Entered: " + other.name);
    }

    private void OnTriggerExit(Collider other)
    {
        var bc = other as BoxCollider;
        if (!bc) return;

        if (insideBounds.Remove(bc) && activeBounds == bc)
            activeBounds = null;
    }

    private void LateUpdate()
    {
        // If we aren't inside any bounds yet, DON'T clamp.
        // This prevents the “frame 0 teleport” chaos.
        if (insideBounds.Count == 0) return;

        activeBounds = ChooseBestBounds();
        if (!activeBounds) return;

        ClampToBounds(activeBounds);
    }

    private BoxCollider ChooseBestBounds()
    {
        // Cleanup destroyed
        for (int i = insideBounds.Count - 1; i >= 0; i--)
        {
            if (insideBounds[i] == null)
                insideBounds.RemoveAt(i);
        }

        if (insideBounds.Count == 0) return null;

        // Use a "feet point" for better results on bridges
        Vector3 feet = transform.position;
        feet.y = characterController ? (transform.position.y - characterController.height * 0.5f + 0.1f) : transform.position.y;

        // Choose the bounds whose *ClosestPoint* is closest to the player’s feet
        BoxCollider best = null;
        float bestDist = float.MaxValue;

        foreach (var bc in insideBounds)
        {
            Vector3 closest = bc.ClosestPoint(feet);
            float d = (closest - feet).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = bc;
            }
        }

        return best;
    }

    private void ClampToBounds(BoxCollider trainBounds)
    {
        Transform boundsTransform = trainBounds.transform;

        Vector3 localPosition = boundsTransform.InverseTransformPoint(transform.position);

        Vector3 localCenter = trainBounds.center;
        Vector3 localSize   = trainBounds.size;

        float controllerRadius = characterController ? characterController.radius : 0.3f;
        float controllerSkin   = characterController ? characterController.skinWidth : 0.06f;
        float collisionMargin  = controllerRadius + controllerSkin + edgePaddingMeters;

        float halfX = localSize.x * 0.5f;
        float halfZ = localSize.z * 0.5f;

        float minX = localCenter.x - halfX + collisionMargin;
        float maxX = localCenter.x + halfX - collisionMargin;
        float minZ = localCenter.z - halfZ + collisionMargin;
        float maxZ = localCenter.z + halfZ - collisionMargin;

        float clampedX = Mathf.Clamp(localPosition.x, minX, maxX);
        float clampedZ = Mathf.Clamp(localPosition.z, minZ, maxZ);

        bool needsSnap =
            Mathf.Abs(localPosition.x - clampedX) > snapEpsilonMeters ||
            Mathf.Abs(localPosition.z - clampedZ) > snapEpsilonMeters;

        if (!needsSnap) return;

        Vector3 clampedLocal = new Vector3(clampedX, localPosition.y, clampedZ);
        Vector3 targetWorld  = boundsTransform.TransformPoint(clampedLocal);

        // ✅ SAFER: push back in using CharacterController.Move instead of teleporting
        Vector3 correction = targetWorld - transform.position;
        correction.y = 0f;

        if (correction.sqrMagnitude > snapEpsilonMeters * snapEpsilonMeters)
        {
            characterController.Move(correction);
        }
    }
}