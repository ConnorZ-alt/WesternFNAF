using UnityEngine;
using UnityEngine.UI;
using System;

public class TrainController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float accelTime = 5f;

    [Header("Coal")]
    [SerializeField] private float drainPerSecond = 0.05f;
    [SerializeField] private float slowThreshold = .20f;
    [SerializeField] private float coalAmount = .20f;

    [Header("Navigation")]
    [SerializeField] private Transform goal;
    [SerializeField] private float goalStopDistance = 2f;

    [Header("UI")]
    [SerializeField] private Slider coalSlider;

    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float coal = 1f;
    [SerializeField] private float currentSpeed = 0f;

    public event Action<float> OnCoalChanged;

    private bool reachedGoal = false;   // <-- prevent double-trigger

    void Start()
    {
        UpdateCoalUI();
    }

    void Update()
    {
        // 1) Drain coal
        if (coal > 0f)
        {
            coal = Mathf.Max(0f, coal - drainPerSecond * Time.deltaTime);
            UpdateCoalUI();
        }

        // 2) Target speed
        float targetSpeed = 0f;
        if (coal <= 0f) targetSpeed = 0f;
        else if (coal < slowThreshold)
        {
            float normalizedCoalLerp = coal / slowThreshold;
            targetSpeed = Mathf.Lerp(0f, maxSpeed * 0.4f, normalizedCoalLerp);
        }
        else targetSpeed = maxSpeed;

        // 3) Smooth accel
        float accelRate = (maxSpeed / Mathf.Max(0.01f, accelTime));
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

        // 4) Move toward goal
        if (goal && !reachedGoal)
        {
            Vector3 toGoal = goal.position - transform.position;
            Vector3 direction = toGoal.sqrMagnitude > 0.01f ? toGoal.normalized : transform.forward;
            transform.position += direction * currentSpeed * Time.deltaTime;

            if (direction.sqrMagnitude > 0f)
            {
                Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 2f);
            }

            // 5) Reached goal → show Results scene
            if (toGoal.magnitude <= goalStopDistance)
            {
                reachedGoal = true;
                currentSpeed = 0f;
                enabled = false;

                var sceneManagement = FindObjectOfType<SceneManagement>();
                if (sceneManagement != null) sceneManagement.OnShowResults();
                else Debug.LogError("[TrainController] SceneManagement not found in scene.");
            }
        }
    }

    public void AddCoal(float amount)
    {
        float before = coal;
        coal = Mathf.Clamp01(coal + amount);
        if (!Mathf.Approximately(before, coal))
            UpdateCoalUI();
    }

    public float GetCoal() => coal;
    public float GetCoalAmount() => coalAmount;

    private void UpdateCoalUI()
    {
        if (coalSlider) coalSlider.value = coal;
        OnCoalChanged?.Invoke(coal);
    }
}
