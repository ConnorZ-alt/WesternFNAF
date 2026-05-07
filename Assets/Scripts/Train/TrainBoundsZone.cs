using UnityEngine;

/// <summary>
/// TrainBoundsZone
/// This is a "zone" the player can stand in (car deck or bridge).
/// It tells the player:
/// 1) what bounds collider to clamp to
/// 2) what train motion source to use (so turns work correctly)
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class TrainBoundsZone : MonoBehaviour
{
    [Header("Clamp Bounds")]
    [Tooltip("The bounds collider used to clamp the player. Usually this same BoxCollider.")]
    public BoxCollider bounds;

    [Header("Motion Source")]
    [Tooltip("Which TrainPathFollower should the player 'ride' while inside this zone?")]
    public TrainPathFollower motionSource;

    private void Reset()
    {
        bounds = GetComponent<BoxCollider>();
        bounds.isTrigger = true;

        // Nice default: try to find a TrainPathFollower in the parent (car root)
        motionSource = GetComponentInParent<TrainPathFollower>();
    }

    private void Awake()
    {
        if (!bounds) bounds = GetComponent<BoxCollider>();
        bounds.isTrigger = true;

        // If not set in Inspector, try to auto-find it.
        if (!motionSource)
            motionSource = GetComponentInParent<TrainPathFollower>();
    }
}


// using UnityEngine;
//
// [DisallowMultipleComponent]
//
// // public TrainPathFollower motionSource;
//
// public class TrainBoundsZone : MonoBehaviour
// {
//     [Tooltip("The bounds collider used to clamp the player.")]
//     public BoxCollider bounds;
//
//     private void Reset()
//     {
//         bounds = GetComponent<BoxCollider>();
//         if (bounds) bounds.isTrigger = true;
//     }
//
//     private void Awake()
//     {
//         if (!bounds) bounds = GetComponent<BoxCollider>();
//         if (bounds) bounds.isTrigger = true;
//     }
// }