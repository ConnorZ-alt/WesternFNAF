using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TrainController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float accelTime = 5f;

    [Header("Coal")]
    [SerializeField] private float drainPerSecond = 0.05f;
    [SerializeField] private float slowThreshold = 0.20f;
    [SerializeField] private float coalAmount = 0.20f;

    [Header("Path Follower (REQUIRED for path movement)")]
    [SerializeField] private TrainPathFollower pathFollower;

    [Header("UI")]
    [SerializeField] private Slider coalSlider;

    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float coal = 1f;
    [SerializeField] private float currentSpeed = 0f;

    public Vector3 FrameDelta { get; private set; } = Vector3.zero;
    private Vector3 lastTrainPosition;

    public event Action<float> CoalChanged;
    public event Action<float> SpeedChanged;
    public event Action GoalReached;

    private Action onGoalReachedCommand;
    private bool reachedGoal;

    private void Awake()
    {
        if (!pathFollower) pathFollower = GetComponent<TrainPathFollower>();

        if (!pathFollower)
        {
            Debug.LogError("[TrainController] Missing TrainPathFollower reference.", this);
            enabled = false;
            return;
        }

        onGoalReachedCommand = DefaultShowResultsOnGoalReached;
    }

    private void OnEnable()
    {
        if (pathFollower != null)
            pathFollower.OnReachedPathEnd += HandleReachedPathEnd;
    }

    private void OnDisable()
    {
        if (pathFollower != null)
            pathFollower.OnReachedPathEnd -= HandleReachedPathEnd;
    }

    private void Start()
    {
        UpdateCoalUIAndNotify();

        pathFollower.SetSpeed(currentSpeed);

        lastTrainPosition = transform.position;
    }

    private void Update()
    {
        if (reachedGoal) return;
        if (SceneManagement.isPaused) return;

        DrainCoalOverTime();
        UpdateSpeedOverTime();

        pathFollower.SetSpeed(currentSpeed);
    }

    private void FixedUpdate()
    {
        if (SceneManagement.isPaused)
        {
            FrameDelta = Vector3.zero;
            lastTrainPosition = transform.position;
            return;
        }

        Vector3 current = transform.position;
        FrameDelta = current - lastTrainPosition;

        if (FrameDelta.sqrMagnitude < 0.0000001f)
            FrameDelta = Vector3.zero;

        lastTrainPosition = current;
    }

    // ---------------- Coal ----------------
    private void DrainCoalOverTime()
    {
        if (coal <= 0f) return;

        float before = coal;
        coal = Mathf.Max(0f, coal - drainPerSecond * Time.deltaTime);

        if (!Mathf.Approximately(before, coal))
            UpdateCoalUIAndNotify();
    }

    public void AddCoal(float amount)
    {
        float before = coal;
        coal = Mathf.Clamp01(coal + amount);

        if (!Mathf.Approximately(before, coal))
            UpdateCoalUIAndNotify();
    }

    public float GetCoal() => coal;
    public float GetCoalAmount() => coalAmount;

    // ---------------- Speed ----------------
    private void UpdateSpeedOverTime()
    {
        float targetSpeed = CalculateTargetSpeed();

        float accelRate = (maxSpeed / Mathf.Max(0.01f, accelTime));
        float beforeSpeed = currentSpeed;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

        if (!Mathf.Approximately(beforeSpeed, currentSpeed))
            SpeedChanged?.Invoke(currentSpeed);
    }

    private float CalculateTargetSpeed()
    {
        if (coal <= 0f) return 0f;

        if (coal < slowThreshold)
        {
            float normalized = coal / Mathf.Max(0.0001f, slowThreshold);
            return Mathf.Lerp(0f, maxSpeed * 0.4f, normalized);
        }

        return maxSpeed;
    }

    // ---------------- Finish ----------------
    private void HandleReachedPathEnd()
    {
        if (reachedGoal) return;

        reachedGoal = true;
        currentSpeed = 0f;

        if (pathFollower != null)
            pathFollower.SetSpeed(0f);

        GoalReached?.Invoke();
        onGoalReachedCommand?.Invoke();
    }

    private void DefaultShowResultsOnGoalReached()
    {
        var sceneManagement = FindObjectOfType<SceneManagement>();
        if (sceneManagement != null)
            sceneManagement.OnShowResults();
        else
            Debug.LogError($"[{nameof(TrainController)}] SceneManagement not found in scene.", this);

        enabled = false;
    }

    public void SetOnGoalReachedCommand(Action command) => onGoalReachedCommand = command;

    // ---------------- UI ----------------
    private void UpdateCoalUIAndNotify()
    {
        if (coalSlider != null)
            coalSlider.value = coal;

        CoalChanged?.Invoke(coal);
    }
}