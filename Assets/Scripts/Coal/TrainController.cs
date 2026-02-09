using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class TrainController : MonoBehaviour
{
    // ----------------------------
    // Movement Settings
    // ----------------------------

    [Header("Movement")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float accelTime = 5f;
    // accelTime is basically: "how many seconds until we reach max speed?"

    // ----------------------------
    // Coal Settings
    // ----------------------------

    [Header("Coal")]
    [SerializeField] private float drainPerSecond = 0.05f;
    // How fast coal drains every second while the train is running.

    [SerializeField] private float slowThreshold = 0.20f;
    // Below this amount of coal, the train starts slowing down.

    [SerializeField] private float coalAmount = 0.20f;
    // This is how much coal a "normal" piece gives (used by other scripts).

    // ----------------------------
    // Navigation / Goal Settings
    // ----------------------------

    [Header("Navigation")]
    [SerializeField] private Transform goal;
    [SerializeField] private float goalStopDistance = 2f;
    // When the train gets within this distance of the goal, it stops.

    // ----------------------------
    // UI
    // ----------------------------

    [Header("UI")]
    [SerializeField] private Slider coalSlider;

    // ----------------------------
    // Debug / Runtime Info (read-only in Inspector)
    // ----------------------------

    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float coal = 1f;
    // coal is always clamped between 0 and 1.

    [SerializeField] private float currentSpeed = 0f;
    // currentSpeed smoothly moves toward a target speed.
    
    // Other scripts can listen to these events without this class knowing about them.
    public event Action<float> CoalChanged;           // sends the new coal amount (0..1)
    public event Action<float> SpeedChanged;          // sends the new currentSpeed
    public event Action GoalReached;                  // fired once when we reach the goal
    
    // This lets us replace what happens when we reach the goal without editing movement code.
    private Action onGoalReachedCommand;
    
    private bool reachedGoal = false;

    // Physics movement
    private Rigidbody trainRb;

    // Cached direction updated in Update(), used in FixedUpdate()
    private Vector3 cachedDirection = Vector3.forward;

    // Optional: if you REALLY need this for a certain build, turn it on.
    [Header("Advanced")]
    [SerializeField] private bool forcePhysicsSyncEachFixedUpdate = false;

    private void Awake()
    {
        // Awake runs when the object loads.
        // We set up the Rigidbody and default goal behavior here.

        trainRb = GetComponent<Rigidbody>();
        if (trainRb == null)
            trainRb = gameObject.AddComponent<Rigidbody>();

        // We are moving with MovePosition / MoveRotation, so kinematic is correct here.
        trainRb.isKinematic = true;
        trainRb.interpolation = RigidbodyInterpolation.Interpolate;
        trainRb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // Default goal command: show results screen (if SceneManagement exists).
        onGoalReachedCommand = DefaultShowResultsOnGoalReached;
    }

    private void Start()
    {
        // Start runs after Awake.
        // We update UI once at the beginning so it matches the starting coal.
        UpdateCoalUIAndNotify();
    }

    private void Update()
    {
        // Update runs every frame.
        // We do "planning" here: drain coal, compute target speed, and cache direction.
        // Actual movement happens in FixedUpdate for better physics consistency.

        if (reachedGoal)
            return;

        DrainCoalOverTime();
        UpdateSpeedOverTime();
        CacheDirectionToGoal();
    }

    private void FixedUpdate()
    {
        // FixedUpdate runs on a fixed timer (good for physics).
        // This is where we actually move the train.

        if (reachedGoal)
            return;

        if (goal == null)
            return;

        MoveTrainWithPhysics();
        CheckGoalReached();
    }

    // ----------------------------
    // Coal Logic
    // ----------------------------

    private void DrainCoalOverTime()
    {
        // This slowly drains coal while time passes.
        // If coal is already 0, we stop draining.

        if (coal <= 0f)
            return;

        float before = coal;
        coal = Mathf.Max(0f, coal - drainPerSecond * Time.deltaTime);

        if (!Mathf.Approximately(before, coal))
            UpdateCoalUIAndNotify();
    }

    public void AddCoal(float amount)
    {
        // This is called by other scripts when the player delivers coal.
        // We clamp it so it always stays between 0 and 1.

        float before = coal;
        coal = Mathf.Clamp01(coal + amount);

        if (!Mathf.Approximately(before, coal))
            UpdateCoalUIAndNotify();
    }

    public float GetCoal() => coal;

    public float GetCoalAmount() => coalAmount;

    // ----------------------------
    // Speed / Movement Logic
    // ----------------------------

    private void UpdateSpeedOverTime()
    {
        // This chooses the target speed based on how much coal we have,
        // then smoothly moves currentSpeed toward that target.

        float targetSpeed = CalculateTargetSpeed();

        float accelRate = (maxSpeed / Mathf.Max(0.01f, accelTime));
        float beforeSpeed = currentSpeed;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

        if (!Mathf.Approximately(beforeSpeed, currentSpeed))
            SpeedChanged?.Invoke(currentSpeed);
    }

    private float CalculateTargetSpeed()
    {
        // This is the "rule" for how coal affects the train speed.
        // - If coal is 0: speed is 0
        // - If coal is low: speed ramps up from 0 to 40% max speed
        // - If coal is enough: speed is maxSpeed

        if (coal <= 0f)
            return 0f;

        if (coal < slowThreshold)
        {
            float normalized = coal / Mathf.Max(0.0001f, slowThreshold);
            return Mathf.Lerp(0f, maxSpeed * 0.4f, normalized);
        }

        return maxSpeed;
    }

    private void CacheDirectionToGoal()
    {
        // We calculate which direction the train should move to reach the goal.
        // We cache it here so FixedUpdate can use it without recalculating every time.

        if (goal == null)
            return;

        Vector3 toGoal = goal.position - trainRb.position;

        if (toGoal.sqrMagnitude > 0.01f)
            cachedDirection = toGoal.normalized;
        else
            cachedDirection = transform.forward;
    }

    private void MoveTrainWithPhysics()
    {
        // This moves the train using Rigidbody methods so physics stays happy.

        Vector3 dir = cachedDirection.sqrMagnitude > 0.0001f ? cachedDirection : transform.forward;

        Vector3 nextPos = trainRb.position + dir * currentSpeed * Time.fixedDeltaTime;
        trainRb.MovePosition(nextPos);

        // Rotate the train so it faces the direction it is moving.
        if (dir.sqrMagnitude > 0f)
        {
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            Quaternion nextRot = Quaternion.Slerp(trainRb.rotation, look, Time.fixedDeltaTime * 2f);
            trainRb.MoveRotation(nextRot);
        }

        if (forcePhysicsSyncEachFixedUpdate)
            Physics.SyncTransforms();
    }

    // ----------------------------
    // Goal Logic
    // ----------------------------

    private void CheckGoalReached()
    {
        // This checks if we got close enough to the goal to stop.

        float dist = Vector3.Distance(trainRb.position, goal.position);
        if (dist > goalStopDistance)
            return;

        reachedGoal = true;
        currentSpeed = 0f;

        // Let other scripts know we reached the goal (Observer).
        GoalReached?.Invoke();

        // Do the goal behavior (Command).
        onGoalReachedCommand?.Invoke();
    }

    private void DefaultShowResultsOnGoalReached()
    {
        // This is the default "goal reached" behavior:
        // find SceneManagement and show the results screen.

        var sceneManagement = FindObjectOfType<SceneManagement>();
        if (sceneManagement != null)
            sceneManagement.OnShowResults();
        else
            Debug.LogError($"[{nameof(TrainController)}] SceneManagement not found in scene.", this);

        // Optional: disable the script so it stops updating/moving.
        enabled = false;
    }

    // If you ever want to change what happens at the goal without editing this script:
    public void SetOnGoalReachedCommand(Action command) => onGoalReachedCommand = command;

    // ----------------------------
    // UI + Notifications
    // ----------------------------

    private void UpdateCoalUIAndNotify()
    {
        // This updates the slider UI and notifies listeners.
        // We keep it in one method so we don’t forget to do it.

        if (coalSlider != null)
            coalSlider.value = coal;

        CoalChanged?.Invoke(coal);
    }
}
