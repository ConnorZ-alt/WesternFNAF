using UnityEngine;

/// <summary>
/// MovingSurface
/// Put this on anything the player can stand on (train cars, bridges).
/// It records how much the object moved AND rotated each frame,
/// so the player can "stick" to it like a moving platform.
/// </summary>
[DisallowMultipleComponent]
public class MovingSurface : MonoBehaviour
{
    public Vector3 FrameDelta { get; private set; }
    public Quaternion RotationDelta { get; private set; }

    private Vector3 lastPos;
    private Quaternion lastRot;

    private void Start()
    {
        lastPos = transform.position;
        lastRot = transform.rotation;
        FrameDelta = Vector3.zero;
        RotationDelta = Quaternion.identity;
    }

    private void LateUpdate()
    {
        Vector3 nowPos = transform.position;
        Quaternion nowRot = transform.rotation;

        FrameDelta = nowPos - lastPos;
        RotationDelta = nowRot * Quaternion.Inverse(lastRot);

        lastPos = nowPos;
        lastRot = nowRot;
    }
}