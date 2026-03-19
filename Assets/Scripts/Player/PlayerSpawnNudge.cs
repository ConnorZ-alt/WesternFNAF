using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class PlayerSpawnNudge : MonoBehaviour
{
    [SerializeField] private float nudgeUpMeters = 0.5f;

    private void Start()
    {
        // If we spawned intersecting something, move up a bit.
        transform.position += Vector3.up * nudgeUpMeters;
    }
}