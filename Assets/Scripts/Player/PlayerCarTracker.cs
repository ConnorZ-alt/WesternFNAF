using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCarTracker : MonoBehaviour
{
    public TrainCarDynamiteTargets CurrentCarTargets { get; private set; }

    public void SetCurrentCar(TrainCarDynamiteTargets newCarTargets)
    {
        if (newCarTargets == null)
            return;

        if (CurrentCarTargets == newCarTargets)
            return;

        CurrentCarTargets = newCarTargets;
        Debug.Log($"[PlayerCarTracker] Current car set to: {newCarTargets.name}", this);
    }
}