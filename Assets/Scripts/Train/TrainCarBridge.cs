using UnityEngine;

[DisallowMultipleComponent]
public class TrainCarBridge : MonoBehaviour
{
    [Header("Anchors")]
    [SerializeField] private Transform backAnchor;
    [SerializeField] private Transform frontAnchor;

    [Header("Sizing")]
    [SerializeField] private float extraLength = 0.1f;
    [SerializeField] private float width = 1.2f;
    [SerializeField] private float thickness = 0.2f;

    [Header("Optional")]
    [SerializeField] private bool lockUpVector = true;

    [Header("References")]
    [Tooltip("Solid collider on the ROOT that the player stands on (NOT trigger).")]
    [SerializeField] private BoxCollider walkCollider;
    
    [Header("Visual (optional)")]
    [SerializeField] private Transform bridgeVisual; // the cube child

    [Tooltip("The child object that has the trigger bounds + TrainBoundsZone (your BridgeBounds).")]
    [SerializeField] private TrainBoundsZone boundsZone;

    private BoxCollider zoneTrigger;

    private void Awake()
    {
        // 1) Walk collider (root)
        if (!walkCollider) walkCollider = GetComponent<BoxCollider>();
        if (walkCollider)
            walkCollider.isTrigger = false;

        // 2) Bounds zone is on child (BridgeBounds)
        if (!boundsZone) boundsZone = GetComponentInChildren<TrainBoundsZone>(true);

        if (boundsZone != null)
        {
            zoneTrigger = boundsZone.bounds;
            if (zoneTrigger != null)
                zoneTrigger.isTrigger = true;
        }
        else
        {
            Debug.LogError("[TrainCarBridge] Could not find TrainBoundsZone in children. Put it on BridgeBounds.", this);
        }
        
        if (!bridgeVisual)
        {
            var child = transform.Find("BridgeVisual");
            if (child) bridgeVisual = child;
        }
    }

    public void SetAnchors(Transform backOfFrontCar, Transform frontOfBackCar)
    {
        backAnchor = backOfFrontCar;
        frontAnchor = frontOfBackCar;

        // Assign motion source for the bridge zone
        if (boundsZone != null)
        {
            TrainPathFollower source = backAnchor ? backAnchor.GetComponentInParent<TrainPathFollower>() : null;
            if (!source && frontAnchor) source = frontAnchor.GetComponentInParent<TrainPathFollower>();

            boundsZone.motionSource = source;
        }
    }

    private void LateUpdate()
    {
        if (!backAnchor || !frontAnchor) return;

        Vector3 a = backAnchor.position;
        Vector3 b = frontAnchor.position;

        Vector3 mid = (a + b) * 0.5f;
        Vector3 forward = (b - a);
        float dist = forward.magnitude;

        if (dist < 0.001f) return;
        forward /= dist;

        transform.position = mid;
        Quaternion rot = lockUpVector
            ? Quaternion.LookRotation(forward, Vector3.up)
            : Quaternion.LookRotation(forward, backAnchor.up);
        Vector3 euler = rot.eulerAngles;
        euler.x = 0f;
        transform.rotation = Quaternion.Euler(euler);

        float length = dist + extraLength;
        
        // IMPORTANT: never scale the root, because that multiplies collider sizes.
        transform.localScale = Vector3.one;

        // Stretch the visible cube instead (so it matches the gap)
        if (bridgeVisual)
        {
            Vector3 s = bridgeVisual.localScale;
            bridgeVisual.localScale = new Vector3(s.x, s.y, length);
            bridgeVisual.localPosition = Vector3.zero;
            bridgeVisual.localRotation = Quaternion.identity;
        }

        // Resize walk collider (solid)
        if (walkCollider)
        {
            walkCollider.size = new Vector3(width, thickness, length);
            walkCollider.center = Vector3.zero;
        }

        // Resize zone trigger (bigger so the player doesn't lose it at seams)
        if (zoneTrigger)
        {
            float triggerLength = length + 0.8f;   // add overlap so it extends slightly onto both cars
            zoneTrigger.size = new Vector3(width + 0.2f, 2.0f, triggerLength);
            zoneTrigger.center = new Vector3(0f, 1.0f, 0f);
        }
    }
}