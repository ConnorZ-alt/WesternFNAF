using UnityEngine;

[DisallowMultipleComponent]
public class TrainCarZone : MonoBehaviour
{
    [SerializeField] private TrainCarDynamiteTargets carTargets;

    private void Reset()
    {
        if (carTargets == null)
            carTargets = GetComponentInParent<TrainCarDynamiteTargets>();
    }

    private void OnTriggerStay(Collider other)
    {
        PlayerCarTracker tracker = other.GetComponent<PlayerCarTracker>();
        if (tracker == null)
            tracker = other.GetComponentInParent<PlayerCarTracker>();

        if (tracker != null)
            tracker.SetCurrentCar(carTargets);
    }
}