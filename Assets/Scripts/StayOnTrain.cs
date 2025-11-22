using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class StayOnTrainBounds : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Train/PlayerBounds BoxCollider (isTrigger). If left empty, we will try to auto-find it.")]
    [SerializeField] private BoxCollider trainBounds;

    [Header("Behaviour")]
    [Tooltip("Extra gap away from the walls, added on top of CharacterController radius + skin.")]
    [SerializeField] private float edgePaddingMeters = 0.05f;

    [Tooltip("Minimum delta (meters) before we actually snap the player to the clamp position.")]
    [SerializeField] private float snapEpsilonMeters = 0.001f;

    [Tooltip("Clamp Y to deck? If true: Y = bounds.min.y + deckYOffsetMeters.")]
    [SerializeField] private bool clampYToDeck = false;

    [SerializeField] private float deckYOffsetMeters = 0f;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private Color gizmoAreaColor = new Color(0f, 0.6f, 1f, 0.08f);
    [SerializeField] private Color gizmoEdgeColor = new Color(0f, 0.6f, 1f, 0.35f);

    private CharacterController characterController;

    // -------- Unity --------
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (!trainBounds)
            TryAutoFindBounds();

        if (!trainBounds)
        {
            Debug.LogError("[StayOnTrainBounds] Please assign the Train/PlayerBounds BoxCollider.");
            enabled = false;
            return;
        }

        if (!trainBounds.isTrigger)
            Debug.LogWarning("[StayOnTrainBounds] PlayerBounds BoxCollider should be set to IsTrigger = true.");

        // Heads-up if the bounds object is scaled oddly (still works, but easier if scale = 1,1,1)
        Vector3 ls = trainBounds.transform.localScale;
        if (Mathf.Abs(ls.x - 1f) > 0.001f || Mathf.Abs(ls.y - 1f) > 0.001f || Mathf.Abs(ls.z - 1f) > 0.001f)
            Debug.Log("[StayOnTrainBounds] PlayerBounds has non-1 scale. It’s supported, but prefer size on the BoxCollider over Transform scale.");
    }

    private void LateUpdate()
    {
        if (!trainBounds) return;

        // Convert player position into the bounds' LOCAL space to respect rotation.
        Transform boundsTransform = trainBounds.transform;
        Vector3 localPosition = boundsTransform.InverseTransformPoint(transform.position);

        // BoxCollider center/size are in LOCAL coordinates.
        Vector3 localCenter = trainBounds.center;
        Vector3 localSize   = trainBounds.size;

        // Padding = controller radius + skin + extra margin
        float controllerRadius = characterController ? characterController.radius    : 0.3f;
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

        if (needsSnap || clampYToDeck)
        {
            Vector3 clampedLocal = new Vector3(clampedX, localPosition.y, clampedZ);
            Vector3 targetWorld = boundsTransform.TransformPoint(clampedLocal);

            if (clampYToDeck)
            {
                // Use world-space bounds for clean deck Y
                float deckY = trainBounds.bounds.min.y + deckYOffsetMeters;
                targetWorld.y = deckY;
            }

            bool controllerWasEnabled = characterController.enabled;
            if (controllerWasEnabled) characterController.enabled = false;
            transform.position = targetWorld;
            if (controllerWasEnabled) characterController.enabled = true;
        }
    }

    // -------- Helpers --------
    private void TryAutoFindBounds()
    {
        // 1) Same parent subtree (common case)
        var candidates = GetComponentsInParent<Transform>(true);
        foreach (var t in candidates)
        {
            var bc = t.GetComponentInChildren<BoxCollider>(true);
            if (bc && bc.name.Contains("PlayerBounds"))
            {
                trainBounds = bc;
                return;
            }
        }

        // 2) Any BoxCollider named like PlayerBounds
        foreach (var bc in FindObjectsOfType<BoxCollider>(true))
        {
            if (bc.name.Contains("PlayerBounds"))
            {
                trainBounds = bc;
                return;
            }
        }

        // 3) Last resort: first trigger BoxCollider in scene
        foreach (var bc in FindObjectsOfType<BoxCollider>(true))
        {
            if (bc.isTrigger)
            {
                trainBounds = bc;
                return;
            }
        }
    }

    /// <summary>Call this to apply the clamp immediately (e.g., after teleport).</summary>
    public void SnapNow()
    {
        LateUpdate();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos || !trainBounds) return;

        // Draw the *padded* clamp rectangle at current deck height in world space.
        Transform bt = trainBounds.transform;
        Vector3 c = trainBounds.center;
        Vector3 s = trainBounds.size;

        // Build the 4 local corners with padding, then transform to world.
        float controllerRadius = characterController ? characterController.radius    : 0.3f;
        float controllerSkin   = characterController ? characterController.skinWidth : 0.06f;
        float pad = controllerRadius + controllerSkin + edgePaddingMeters;

        float halfX = s.x * 0.5f - pad;
        float halfZ = s.z * 0.5f - pad;
        float yLocal = c.y;

        Vector3[] localCorners =
        {
            new Vector3(c.x - halfX, yLocal, c.z - halfZ),
            new Vector3(c.x + halfX, yLocal, c.z - halfZ),
            new Vector3(c.x + halfX, yLocal, c.z + halfZ),
            new Vector3(c.x - halfX, yLocal, c.z + halfZ),
        };

        Vector3[] worldCorners = new Vector3[4];
        for (int i = 0; i < 4; i++) worldCorners[i] = bt.TransformPoint(localCorners[i]);

        // Fill-ish
        Gizmos.color = gizmoAreaColor;
        Gizmos.DrawLine(worldCorners[0], worldCorners[1]);
        Gizmos.DrawLine(worldCorners[1], worldCorners[2]);
        Gizmos.DrawLine(worldCorners[2], worldCorners[3]);
        Gizmos.DrawLine(worldCorners[3], worldCorners[0]);

        // Edges
        Gizmos.color = gizmoEdgeColor;
        for (int i = 0; i < 4; i++)
            Gizmos.DrawSphere(worldCorners[i], 0.05f);
    }
}
