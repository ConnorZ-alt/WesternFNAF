using UnityEngine;
using System;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class TrainPathFollower : MonoBehaviour
{
    [Header("Path Reference")]
    [SerializeField] private CinemachinePathBase path;
    [SerializeField] public CinemachinePathBase.PositionUnits units = CinemachinePathBase.PositionUnits.Distance;
    [SerializeField] private bool loopPath = false;

    [Header("Motion")]
    [SerializeField] public float pathSpeed = 5f;
    [SerializeField] private float turnLerp = 6f;

    [Header("Debug")]
    [SerializeField] private float currentUnits = 0f;
    [SerializeField] private float lookAheadUnits = 2f;
    [SerializeField] private float maxTurnDegreesPerSecond = 90f;
    
    private Quaternion lastRotation;
    public Quaternion RotationDelta { get; private set; }
    
    public event Action OnReachedPathEnd;
    
    public Vector3 FrameDelta;

    private Rigidbody rb;
    private bool endedFired;
    private Vector3 lastPosition;

    public float GetCurrentUnits() => currentUnits;

    public void SetCurrentUnitsNoSnap(float newUnits)
    {
        currentUnits = Mathf.Max(0f, newUnits);
    }
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        if (!path)
        {
            Debug.LogError("[TrainPathFollower] No path assigned.", this);
            enabled = false;
        }
        
        // SnapToPathNow();
    }

    public void SetSpeed(float newSpeed) => pathSpeed = Mathf.Max(0f, newSpeed);

    public void SetPathUnits(float newUnits)
    {
        currentUnits = Mathf.Max(0f, newUnits);
        endedFired = false;              // allow end event again if we restart
        
        SnapToPathNow();
    }

    private void FixedUpdate()
    {
        if (SceneManagement.isPaused) return;
        if (!path) return;

        float maxU = GetMaxUnits();
        if (maxU <= 0.001f) return;

        // If we already ended and we are not looping, don't keep processing.
        if (!loopPath && endedFired)
            return;

        currentUnits += pathSpeed * Time.fixedDeltaTime;

        if (currentUnits >= maxU)
        {
            if (loopPath)
            {
                currentUnits = (maxU > 0f) ? (currentUnits % maxU) : 0f;
                endedFired = false; // we wrapped, so allow end to fire again later
            }
            else
            {
                currentUnits = maxU;

                if (!endedFired)
                {
                    endedFired = true;
                    OnReachedPathEnd?.Invoke();
                }

                // Stop here. TrainController will also set speed to 0 and show results.
                // Disabling prevents tiny jitter from re-evaluating every frame.
                enabled = false;
                return;
            }
        }

        Vector3 pos = path.EvaluatePositionAtUnit(currentUnits, units);
        
        Vector3 rawDelta = pos - lastPosition;
        rawDelta.y = 0f;          // Remove vertical movement

        FrameDelta = rawDelta;
        lastPosition = pos;
        
        rb.MovePosition(pos);
        
        // -------- REPLACE ROTATION SECTION WITH THIS --------
        float ahead = currentUnits + lookAheadUnits;
        float maxUnits = maxU;

        // Wrap/clamp the ahead sample
        if (loopPath && maxUnits > 0f)
            ahead %= maxUnits;
        else
            ahead = Mathf.Min(ahead, maxUnits);

        // Look direction = where the path will be shortly
        Vector3 posAhead = path.EvaluatePositionAtUnit(ahead, units);
        Vector3 forward = (posAhead - pos);
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(forward.normalized, Vector3.up);
            float maxStep = maxTurnDegreesPerSecond * Time.fixedDeltaTime;
            Quaternion limited = Quaternion.RotateTowards(rb.rotation, targetRot, maxStep);
            rb.MoveRotation(limited);
            RotationDelta = rb.rotation * Quaternion.Inverse(lastRotation);
            lastRotation = rb.rotation;
        }
    }

    private float GetMaxUnits()
    {
        if (units == CinemachinePathBase.PositionUnits.Distance)
            return Mathf.Max(0f, path.PathLength);

        return 1f;
    }

    private void SnapToPathNow()
    {
        if (!path) return;
        lastRotation = rb.rotation;
        RotationDelta = Quaternion.identity;

        // Current position on the path
        Vector3 posCurrent = path.EvaluatePositionAtUnit(currentUnits, units);
        rb.position = posCurrent;
        lastPosition = posCurrent; 
        FrameDelta = Vector3.zero;
        // Look-ahead sample for forward direction
        float maxUnits = GetMaxUnits();
        float ahead = currentUnits + lookAheadUnits;

        if (loopPath && maxUnits > 0f)
            ahead %= maxUnits;
        else
            ahead = Mathf.Min(ahead, maxUnits);

        Vector3 posAhead = path.EvaluatePositionAtUnit(ahead, units);

        Vector3 forward = (posAhead - posCurrent);
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.0001f)
            rb.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }
}