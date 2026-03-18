using UnityEngine;

/// <summary>
/// TrainCarFollower
/// This makes a train car follow behind the leader car on the same path.
/// It does NOT pick its own path progress. It copies the leader and stays behind by a gap.
/// </summary>
[DisallowMultipleComponent]
public class TrainCarFollower : MonoBehaviour
{
    [Header("Leader")]
    [SerializeField] private TrainPathFollower leader;

    [Header("Follow Settings")]
    [Tooltip("How far behind the leader we should be (in meters if using Distance units).")]
    [SerializeField] private float followDistanceBehind = 6f;

    [Tooltip("How fast we catch up if we get too far behind.")]
    [SerializeField] private float catchUpLerp = 10f;

    private TrainPathFollower myFollower;

    private void Awake()
    {
        myFollower = GetComponent<TrainPathFollower>();
        if (!myFollower)
        {
            Debug.LogError("[TrainCarFollower] Missing TrainPathFollower on this car.", this);
            enabled = false;
            return;
        }

        if (!leader)
        {
            Debug.LogError("[TrainCarFollower] Leader not assigned.", this);
            enabled = false;
            return;
        }
    }
    
    private void Start()
    {
        float leaderU = leader.GetCurrentUnits();
        myFollower.SetPathUnits(Mathf.Max(0f, leaderU - followDistanceBehind));
    }

    private void FixedUpdate()
    {
        if (SceneManagement.isPaused) return;

        // Leader’s current position along the path
        float leaderU = leader.GetCurrentUnits();

        // Target position behind the leader
        float targetU = leaderU - followDistanceBehind;

        // If your path is looped, we should wrap around instead of going negative.
        // If not looped, clamp at 0.
        targetU = Mathf.Max(0f, targetU);

        // Smoothly approach the target so it doesn’t jitter
        float currentU = myFollower.GetCurrentUnits();
        float newU = Mathf.Lerp(currentU, targetU, Time.fixedDeltaTime * catchUpLerp);

        myFollower.SetCurrentUnitsNoSnap(newU);
    }
}