using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(StayOnTrainBounds))]
[RequireComponent(typeof(PlayerTrainMotion))]
public class PlayerTrainBoundsSwitcher : MonoBehaviour
{
    [SerializeField] private float switchCooldownSeconds = 0.15f;

    private StayOnTrainBounds clamp;
    private PlayerTrainMotion motion;

    private TrainBoundsZone currentZone;
    private float nextAllowedSwitchTime;

    private void Awake()
    {
        clamp = GetComponent<StayOnTrainBounds>();
        motion = GetComponent<PlayerTrainMotion>();
    }

    private void OnTriggerEnter(Collider other)
    {
        var zone = other.GetComponent<TrainBoundsZone>();
        if (!zone || !zone.bounds) return;

        if (Time.time < nextAllowedSwitchTime) return;
        if (zone == currentZone) return;

        currentZone = zone;
        nextAllowedSwitchTime = Time.time + switchCooldownSeconds;

        clamp.SetBounds(zone.bounds);
        motion.SetMotionSource(zone.motionSource);
    }
}