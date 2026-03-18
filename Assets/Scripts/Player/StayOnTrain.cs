using UnityEngine;

/// <summary>
/// StayOnTrainBounds
/// This script keeps the player from walking off the train.
/// It takes the player's position and "clamps" it inside a BoxCollider (the PlayerBounds).
/// If the player goes past the edge, we gently snap them back inside.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class StayOnTrainBounds : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Train/PlayerBounds BoxCollider (isTrigger). If left empty, we will try to auto-find it.")]
    [SerializeField] private BoxCollider trainBounds;

    [Header("Behavior")]
    [Tooltip("Extra space from the walls. This is added on top of the CharacterController's radius/skin.")]
    [SerializeField] private float edgePaddingMeters = 0.05f;

    [Tooltip("How far outside the bounds we have to be before we snap (tiny values prevent jitter).")]
    [SerializeField] private float snapEpsilonMeters = 0.001f;

    [Tooltip("If true, we also force the player to stay at deck height (Y value).")]
    [SerializeField] private bool clampYToDeck = false;

    [Tooltip("Extra Y offset above the deck if clampYToDeck is enabled.")]
    [SerializeField] private float deckYOffsetMeters = 0f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    // NOTE: In Unity, serialized Colors are fine, but we keep them private.
    [SerializeField] private Color gizmoAreaColor = new Color(0f, 0.6f, 1f, 0.08f);
    [SerializeField] private Color gizmoEdgeColor = new Color(0f, 0.6f, 1f, 0.35f);

    // Cached component so we don't call GetComponent() every frame.
    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        // If the bounds collider wasn't assigned in the Inspector,
        // we try to find it automatically.
        if (!trainBounds)
            TryAutoFindBounds();

        // If we still don't have bounds, we can't do our job.
        if (!trainBounds)
        {
            Debug.LogError("[StayOnTrainBounds] Please assign the Train/PlayerBounds BoxCollider.");
            enabled = false;
            return;
        }

        // This is just a friendly warning. The code still works even if it's not a trigger.
        if (!trainBounds.isTrigger)
            Debug.LogWarning("[StayOnTrainBounds] PlayerBounds BoxCollider should usually be IsTrigger = true.");

        // Heads-up if the bounds object is scaled oddly.
        // (It still works, but it's easier to manage if scale is 1,1,1 and you adjust BoxCollider size instead.)
        Vector3 localScale = trainBounds.transform.localScale;
        bool hasNonOneScale =
            Mathf.Abs(localScale.x - 1f) > 0.001f ||
            Mathf.Abs(localScale.y - 1f) > 0.001f ||
            Mathf.Abs(localScale.z - 1f) > 0.001f;

        if (hasNonOneScale)
        {
            Debug.Log("[StayOnTrainBounds] PlayerBounds has non-1 scale. Supported, but prefer adjusting BoxCollider.size instead of Transform scale.");
        }
    }

    private void LateUpdate()
    {
        if (!trainBounds) return;

        // We run in LateUpdate so movement scripts (like PlayerController) can move first,
        // and THEN we clamp at the end of the frame.
        ClampInsideBounds();
    }

    // ---------------- Core logic ----------------

    /// <summary>
    /// Clamps the player's position inside the train bounds.
    /// We do the math in the bounds' LOCAL space so rotations are respected.
    /// </summary>
    private void ClampInsideBounds()
    {
        Transform boundsTransform = trainBounds.transform;

        // Convert player position to LOCAL space of the bounds object.
        // This makes the clamp work even if the train/bounds are rotated.
        Vector3 localPosition = boundsTransform.InverseTransformPoint(transform.position);

        // BoxCollider center/size are LOCAL-space values.
        Vector3 localCenter = trainBounds.center;
        Vector3 localSize = trainBounds.size;

        // Padding helps keep the player's capsule from clipping through the walls.
        float controllerRadius = characterController ? characterController.radius : 0.3f;
        float controllerSkin = characterController ? characterController.skinWidth : 0.06f;
        float collisionMargin = controllerRadius + controllerSkin + edgePaddingMeters;

        float halfX = localSize.x * 0.5f;
        float halfZ = localSize.z * 0.5f;

        // Clamp range inside the box (minus the margin).
        float minX = localCenter.x - halfX + collisionMargin;
        float maxX = localCenter.x + halfX - collisionMargin;
        float minZ = localCenter.z - halfZ + collisionMargin;
        float maxZ = localCenter.z + halfZ - collisionMargin;

        float clampedX = Mathf.Clamp(localPosition.x, minX, maxX);
        float clampedZ = Mathf.Clamp(localPosition.z, minZ, maxZ);

        // Only snap if we are meaningfully outside (prevents jitter).
        bool needsSnap =
            Mathf.Abs(localPosition.x - clampedX) > snapEpsilonMeters ||
            Mathf.Abs(localPosition.z - clampedZ) > snapEpsilonMeters;

        // If we're inside already and we're not clamping Y, we can do nothing.
        if (!needsSnap && !clampYToDeck) return;

        // Build the clamped local position (keep Y the same unless we clamp to deck).
        Vector3 clampedLocal = new Vector3(clampedX, localPosition.y, clampedZ);

        // Convert back to world space.
        Vector3 targetWorldPosition = boundsTransform.TransformPoint(clampedLocal);

        // Optional: force Y to deck height (world-space clean value).
        if (clampYToDeck)
        {
            float deckY = trainBounds.bounds.min.y + deckYOffsetMeters;
            targetWorldPosition.y = deckY;
        }

        // IMPORTANT: Disabling the CharacterController briefly prevents it from "fighting" the position snap.
        bool controllerWasEnabled = characterController.enabled;
        if (controllerWasEnabled) characterController.enabled = false;

        transform.position = targetWorldPosition;

        if (controllerWasEnabled) characterController.enabled = true;
    }

    // ---------------- Helpers ----------------

    /// <summary>
    /// Tries to find a bounds BoxCollider automatically.
    /// This is a backup plan, but the best practice is still dragging the correct BoxCollider in the Inspector.
    /// </summary>
    private void TryAutoFindBounds()
    {
        // 1) Look in the parent chain (common case).
        Transform[] parentChain = GetComponentsInParent<Transform>(true);
        foreach (Transform t in parentChain)
        {
            BoxCollider bc = t.GetComponentInChildren<BoxCollider>(true);
            if (bc && bc.name.Contains("PlayerBounds"))
            {
                trainBounds = bc;
                return;
            }
        }

        // 2) Look anywhere in the scene for something named like "PlayerBounds".
        foreach (BoxCollider bc in FindObjectsOfType<BoxCollider>(true))
        {
            if (bc.name.Contains("PlayerBounds"))
            {
                trainBounds = bc;
                return;
            }
        }

        // 3) Last resort: pick the first trigger BoxCollider in the scene.
        // This is risky, but better than nothing if the scene is simple.
        foreach (BoxCollider bc in FindObjectsOfType<BoxCollider>(true))
        {
            if (bc.isTrigger)
            {
                trainBounds = bc;
                return;
            }
        }
    }

    /// <summary>
    /// Call this if you teleport the player and want to clamp instantly.
    /// </summary>
    public void SnapNow()
    {
        // We reuse the same logic as LateUpdate.
        ClampInsideBounds();
    }
    
    // CHATGPT HELPER FOR BRIDGE/TRAIN TRANSITION BOUNDS
    public void SetBounds(BoxCollider newBounds)
    {
        if (!newBounds) return;
        trainBounds = newBounds;
    }

    // ---------------- Debug gizmos ----------------

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !trainBounds) return;

        // Draw the padded "allowed area" corners in world space.
        // This helps you see what the clamp area really is.
        Transform bt = trainBounds.transform;
        Vector3 center = trainBounds.center;
        Vector3 size = trainBounds.size;

        // NOTE: characterController might be null in edit mode. We'll use safe defaults.
        float controllerRadius = characterController ? characterController.radius : 0.3f;
        float controllerSkin = characterController ? characterController.skinWidth : 0.06f;
        float pad = controllerRadius + controllerSkin + edgePaddingMeters;

        float halfX = size.x * 0.5f - pad;
        float halfZ = size.z * 0.5f - pad;
        float yLocal = center.y;

        // Local corners (a simple rectangle on the XZ plane).
        Vector3[] localCorners =
        {
            new Vector3(center.x - halfX, yLocal, center.z - halfZ),
            new Vector3(center.x + halfX, yLocal, center.z - halfZ),
            new Vector3(center.x + halfX, yLocal, center.z + halfZ),
            new Vector3(center.x - halfX, yLocal, center.z + halfZ),
        };

        // Convert to world corners.
        Vector3[] worldCorners = new Vector3[4];
        for (int i = 0; i < 4; i++)
            worldCorners[i] = bt.TransformPoint(localCorners[i]);

        // Draw outline (filled-ish).
        Gizmos.color = gizmoAreaColor;
        Gizmos.DrawLine(worldCorners[0], worldCorners[1]);
        Gizmos.DrawLine(worldCorners[1], worldCorners[2]);
        Gizmos.DrawLine(worldCorners[2], worldCorners[3]);
        Gizmos.DrawLine(worldCorners[3], worldCorners[0]);

        // Draw corner points.
        Gizmos.color = gizmoEdgeColor;
        for (int i = 0; i < 4; i++)
            Gizmos.DrawSphere(worldCorners[i], 0.05f);
    }
}
