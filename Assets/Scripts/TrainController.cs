using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Serialization;

public class TrainController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Top Speed when Well-Fueled(m/s)")]
    [SerializeField] private float maxSpeed = 10f;
    [Tooltip("How long it takes for train to ramp up to max speed(s)")]
    [SerializeField] private float accelTime = 5f;
    
    [Header("Coal")]
    [Tooltip("Coal meter drains this fraction per second (0..1 per second")]
    [SerializeField] private float drainPerSecond = 0.05f;
    [Tooltip("Below this coal % the train slows; at 0% it stops completely")]
    [SerializeField] private float slowThreshold = .20f;
    [Tooltip("How much coal a single piece adds (0..1)")]
    [SerializeField] private float coalAmount = .20f;
    
    [Header("Navigation")]
    [Tooltip("If set, train moves toward this Transform and finishes when close")]
    [SerializeField] private Transform goal;
    [Tooltip("Distance at which we consider the goal has been reached")]
    [SerializeField] private float goalStopDistance = 2f;
    
    [Header("UI")]
    [SerializeField] private Slider coalSlider;
    // FIX IT TO BE SCENE INSTEAD OF GAMEOBJECT
    [SerializeField] private GameObject resultsScreen;
    
    [Header("Debug")]
    [SerializeField, Range(0f, 1f)] private float coal = 1f;
    [SerializeField] private float currentSpeed = 0f;
    
    public event Action<float> OnCoalChanged;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        UpdateCoalUI();
        if (resultsScreen)
        {
            resultsScreen.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // 1) Drain coal over time
        if (coal > 0f)
        {
            coal = Mathf.Max(0f,coal - drainPerSecond * Time.deltaTime);
            UpdateCoalUI();
        }

        // 2) Pick a target speed from coal level
        float targetSpeed = 0f;
        if (coal <= 0f)
        {
            targetSpeed = 0f;
        } else if (coal < slowThreshold)
        {
            // RENAME T!!!
            float t = coal / slowThreshold;
            targetSpeed = Mathf.Lerp(0f, maxSpeed * 0.4f, t);
        }
        else
        {
            targetSpeed = maxSpeed;
        }
        
        // 3) Smooth acceleration towards goal (and accelTime to settle)
        float accelRate = (maxSpeed / Mathf.Max(0.01f, accelTime));
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);
        
        // 4) Move the train
        if (goal)
        {
            Vector3 toGoal = goal.position - transform.position;
            Vector3 direction = toGoal.sqrMagnitude > 0.01f ? toGoal.normalized : transform.forward;
            transform.position += direction * currentSpeed * Time.deltaTime;
            
            // Rotates to face goal
            if (direction.sqrMagnitude > 0f)
            {
                Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 2f);
            }
            
            // 5) Check if goal was reached
            if (toGoal.magnitude <= goalStopDistance)
            {
                currentSpeed = 0f;
                if (resultsScreen) resultsScreen.SetActive(true);
                {
                    enabled = false;
                }
            }
        }
    }

    public void AddCoal(float amount)
    {
        float before = coal;
        coal = Mathf.Clamp01(coal + amount);
        if (!Mathf.Approximately(before, coal))
        {
            UpdateCoalUI();
        }
    }
    
    public float GetCoal() => coal;
    public float GetCoalAmount() => coalAmount;
    
    private void UpdateCoalUI()
    {
        if (coalSlider) coalSlider.value = coal;
        OnCoalChanged?.Invoke(coal);
    }
}
