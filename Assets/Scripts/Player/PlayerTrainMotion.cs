using UnityEngine;

[DisallowMultipleComponent]
public class PlayerTrainMotion : MonoBehaviour
{
    [Tooltip("Current platform motion source (set by zones).")]
    public TrainPathFollower MotionSource { get; private set; }

    public void SetMotionSource(TrainPathFollower newSource)
    {
        MotionSource = newSource;
    }
}