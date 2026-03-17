using UnityEngine;

/// <summary>
/// TrainCarBridge
/// This keeps a bridge stuck between two train cars.
/// The player can walk on it, but the cars won't collide with it (layer matrix handles that).
/// </summary>
[DisallowMultipleComponent]
public class TrainCarBridge : MonoBehaviour
{
    [Header("Anchors")]
    [SerializeField] private Transform backAnchor;   // back of the front car
    [SerializeField] private Transform frontAnchor;  // front of the back car

    [Header("Sizing")]
    [Tooltip("Extra thickness safety so small gaps don't appear.")]
    [SerializeField] private float extraLength = 0.1f;

    [Tooltip("How wide the bridge is.")]
    [SerializeField] private float width = 1.2f;

    [Tooltip("How thick the bridge collider is.")]
    [SerializeField] private float thickness = 0.2f;

    [Header("Optional")]
    [SerializeField] private bool lockUpVector = true;

    private BoxCollider box;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        if (!box) box = gameObject.AddComponent<BoxCollider>();
    }

    /// <summary>
    /// Call this once after you spawn/setup cars.
    /// </summary>
    public void SetAnchors(Transform backOfFrontCar, Transform frontOfBackCar)
    {
        backAnchor = backOfFrontCar;
        frontAnchor = frontOfBackCar;
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

        if (lockUpVector)
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        else
            transform.rotation = Quaternion.LookRotation(forward, backAnchor.up);

        // Resize collider + visuals so it always spans the gap
        float length = dist + extraLength;

        // BoxCollider uses local size
        box.size = new Vector3(width, thickness, length);

        // Keep collider centered
        box.center = Vector3.zero;
    }
}